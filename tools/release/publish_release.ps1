# Publishes a release made by make_release.ps1 to the PUBLIC releases repo, so players' games pick it up the next time they press Play.
#   powershell -ExecutionPolicy Bypass -File tools/release/publish_release.ps1 -Version 3.9.0 -Dir release_out
# 1. uploads the files (the assets) as a GitHub release v<Version>        (players do not see it yet)
# 2. THEN puts latest.txt + latest.txt.sig on the repo's "public" branch  (from this moment every game that starts will find it)
# Needs the GitHub CLI (gh) signed in. The repo must exist and be public: gh repo create ImPoPzzZ/after-hours-releases --public
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Dir = 'release_out',
    [string]$ReleasesRepo = 'ImPoPzzZ/after-hours-releases',
    [string]$Branch = 'public'
)
$ErrorActionPreference = 'Stop'
$Dir = (Resolve-Path $Dir).Path
foreach ($f in 'latest.txt', 'latest.txt.sig') { if (-not (Test-Path (Join-Path $Dir $f))) { throw "Missing $f in $Dir" } }
if (-not (Select-String -Path (Join-Path $Dir 'latest.txt') -Pattern "^version=$([regex]::Escape($Version))\s*$" -Quiet)) { throw "latest.txt is not version $Version" }

Write-Output "1/2: uploading the files as release v$Version..."
$assets = Get-ChildItem (Join-Path $Dir 'assets') -File | ForEach-Object { $_.FullName }
& gh release create "v$Version" $assets --repo $ReleasesRepo --title $Version --notes ((Get-Content (Join-Path $Dir 'latest.txt') | Where-Object { $_ -like 'notes=*' } | Select-Object -First 1) -replace '^notes=', '')
if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }

Write-Output "2/2: putting the signed manifest on the '$Branch' branch..."
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("ke_pub_" + [guid]::NewGuid().ToString('N').Substring(0, 8))
& git clone --quiet "https://github.com/$ReleasesRepo.git" $tmp
if ($LASTEXITCODE -ne 0) { throw "git clone failed" }
Push-Location $tmp
try {
    $has = (& git ls-remote --heads origin $Branch)
    if ($has) { & git checkout --quiet $Branch } else { & git checkout --quiet -b $Branch }
    Copy-Item (Join-Path $Dir 'latest.txt') . -Force
    Copy-Item (Join-Path $Dir 'latest.txt.sig') . -Force
    & git add latest.txt latest.txt.sig
    & git -c user.name=ImPoPzzZ -c user.email=ImPoPzzZ@users.noreply.github.com commit --quiet -m "Release $Version"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed" }
    & git push --quiet origin $Branch
    if ($LASTEXITCODE -ne 0) { throw "git push failed" }
}
finally { Pop-Location; Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
Write-Output "Done: $Version is public. Games that start from now on will download it."
