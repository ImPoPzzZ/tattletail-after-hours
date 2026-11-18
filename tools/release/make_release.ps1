# Makes one release for the auto-updater: the assets, the manifest (latest.txt) and its signature (latest.txt.sig).
#   powershell -ExecutionPolicy Bypass -File tools/release/make_release.ps1 -Version 3.9.0 -Notes "..." `
#       -Dll bin\Release\net46\KaleidoscopeEndless.dll -Game "D:\SteamLibrary\steamapps\common\Tattletail" -Out release_out
# Then upload (see docs/AUTO_UPDATE.md):  gh release create v3.9.0 release_out\assets\* --repo ImPoPzzZ/after-hours-releases ...
#                                         and commit release_out\latest.txt + latest.txt.sig to that repo's main branch.
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Notes = "",
    [Parameter(Mandatory = $true)][string]$Dll,
    [Parameter(Mandatory = $true)][string]$Game,
    [string]$Repo = '',
    [string]$ReleasesRepo = 'ImPoPzzZ/after-hours-releases',
    [string]$Out = 'release_out',
    [string]$KeyFile = (Join-Path $env:USERPROFILE '.after_hours_keys\update_private.xml')
)
$ErrorActionPreference = 'Stop'
if (-not $Repo) { $Repo = (Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\..')).Path }
if (-not (Test-Path $KeyFile)) { throw "No signing key at $KeyFile (run tools/release/make_keys.ps1 once)." }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 3.9.0" }
$assets = Join-Path $Out 'assets'
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force $assets | Out-Null

# Everything that lives in the mod's own folder (BepInEx\plugins\KaleidoscopeEndless): the DLL and the recorded music.
$files = [ordered]@{}
$files['KaleidoscopeEndless.dll'] = (Resolve-Path $Dll).Path
$plug = Join-Path $Game 'BepInEx\plugins\KaleidoscopeEndless\Sounds'
if (Test-Path $plug) { Get-ChildItem $plug -File | ForEach-Object { $files['Sounds/' + $_.Name] = $_.FullName } }
$repoSounds = Join-Path $Repo 'Sounds'
if (Test-Path $repoSounds) { Get-ChildItem $repoSounds -Filter 'rc_*.wav' -File | ForEach-Object { if (-not $files.Contains('Sounds/' + $_.Name)) { $files['Sounds/' + $_.Name] = $_.FullName } } }

$sha = [System.Security.Cryptography.SHA256]::Create()
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('KEAU1')
$lines.Add("version=$Version")
$lines.Add('notes=' + ($Notes -replace "[\r\n|]", ' '))
$lines.Add("base=https://github.com/$ReleasesRepo/releases/download/v$Version/")
foreach ($rel in $files.Keys) {
    $src = $files[$rel]
    $bytes = [System.IO.File]::ReadAllBytes($src)
    $hash = ([System.BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant()
    $asset = $rel -replace '/', '__'
    Copy-Item $src (Join-Path $assets $asset)
    $lines.Add("file=$hash|$($bytes.Length)|$rel|$asset")
}
$text = ($lines -join "`n") + "`n"
$manifestBytes = (New-Object System.Text.UTF8Encoding $false).GetBytes($text)
[System.IO.File]::WriteAllBytes((Join-Path $Out 'latest.txt'), $manifestBytes)

$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider
$rsa.FromXmlString([System.IO.File]::ReadAllText($KeyFile))
$sig = $rsa.SignData($manifestBytes, [System.Security.Cryptography.SHA256]::Create())
[System.IO.File]::WriteAllText((Join-Path $Out 'latest.txt.sig'), [System.Convert]::ToBase64String($sig), (New-Object System.Text.UTF8Encoding $false))

Write-Output "Release $Version made in $Out ($($files.Count) files):"
Get-Content (Join-Path $Out 'latest.txt') | Select-Object -First 4
Write-Output "Upload: gh release create v$Version `"$assets\*`" --repo $ReleasesRepo --title $Version"
Write-Output "Then put latest.txt and latest.txt.sig in the root of the main branch of $ReleasesRepo."
