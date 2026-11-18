# Builds tools/installer/payload.zip (and the installer icon) for the one-click installer.
#   powershell -ExecutionPolicy Bypass -File tools/installer/make_payload.ps1 -Repo <repo folder> -Game <Tattletail folder> -Dll <built KaleidoscopeEndless.dll> -Out <payload.zip> -X64 <folder with the 64-bit winhttp.dll and .doorstop_version>
#   then: dotnet build tools/installer/AfterHoursInstaller.csproj -c Release
param([string]$Repo, [string]$Game, [string]$Dll, [string]$Out, [string]$X64 = '')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing

if (Test-Path $Out) { Remove-Item $Out -Force }
$zip = [System.IO.Compression.ZipFile]::Open($Out, 'Create')
$script:added = @{}
function AddFile($zip, [string]$path, [string]$entry) {
    if ($script:added.ContainsKey($entry)) { return }   # (the game folder may already hold some of the same files as the repo)
    $script:added[$entry] = $true
    $e = $zip.CreateEntry($entry, [System.IO.Compression.CompressionLevel]::Optimal)   # (forward slashes, as written here)
    $in = [System.IO.File]::OpenRead($path)
    $o = $e.Open()
    $in.CopyTo($o); $o.Dispose(); $in.Dispose()
}

# the loader and BepInEx (32-bit Steam copy), without the dev config, log or cache
foreach ($f in @('winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'CHANGELOG.txt', 'README - HOW TO INSTALL.txt')) {
    AddFile $zip (Join-Path $Game $f) $f
}
Get-ChildItem (Join-Path $Game 'BepInEx\core') -File | ForEach-Object { AddFile $zip $_.FullName ('BepInEx/core/' + $_.Name) }
AddFile $zip (Join-Path $Game 'BepInEx\config\BepInEx.cfg') 'BepInEx/config/BepInEx.cfg'
Get-ChildItem (Join-Path $Game 'ShopPreviews') -File | ForEach-Object { AddFile $zip $_.FullName ('ShopPreviews/' + $_.Name) }

# the mod: the freshly built DLL, the old reaction sounds, and Recalled's recorded music
AddFile $zip $Dll 'BepInEx/plugins/KaleidoscopeEndless/KaleidoscopeEndless.dll'
# the auto-updater (a BepInEx patcher: it swaps in a downloaded update before the mod loads); same file for the 32- and 64-bit game
$updater = Join-Path $Repo 'tools\updater\bin\Release\net46\AfterHoursUpdater.dll'
if (-not (Test-Path $updater)) { throw 'build tools/updater first (dotnet build tools/updater/AfterHoursUpdater.csproj -c Release)' }
AddFile $zip $updater 'BepInEx/patchers/AfterHoursUpdater.dll'
Get-ChildItem (Join-Path $Game 'BepInEx\plugins\KaleidoscopeEndless\Sounds') -File | ForEach-Object { AddFile $zip $_.FullName ('BepInEx/plugins/KaleidoscopeEndless/Sounds/' + $_.Name) }
Get-ChildItem (Join-Path $Repo 'Sounds') -Filter 'rc_*.wav' -File | ForEach-Object { AddFile $zip $_.FullName ('BepInEx/plugins/KaleidoscopeEndless/Sounds/' + $_.Name) }
# the 64-bit (itch.io) game needs a different loader: the installer picks x64/ by the game exe's CPU type
if ($X64 -ne '') {
    AddFile $zip (Join-Path $X64 'winhttp.dll') 'x64/winhttp.dll'
    AddFile $zip (Join-Path $X64 '.doorstop_version') 'x64/.doorstop_version'
}
$zip.Dispose()

$check = [System.IO.Compression.ZipFile]::OpenRead($Out)
$names = $check.Entries | ForEach-Object { $_.FullName }
$check.Dispose()
if ($names -notcontains 'BepInEx/plugins/KaleidoscopeEndless/KaleidoscopeEndless.dll') { throw 'the DLL is not in the payload' }
if ($names -notcontains 'BepInEx/patchers/AfterHoursUpdater.dll') { throw 'the updater is not in the payload' }
if ($names -notcontains 'BepInEx/plugins/KaleidoscopeEndless/Sounds/children_yay.wav') { throw 'the installer needs children_yay.wav' }
if ($X64 -ne '' -and ($names -notcontains 'x64/winhttp.dll' -or $names -notcontains 'x64/.doorstop_version')) { throw 'the 64-bit loader is not in the payload' }
if ($names | Where-Object { $_ -like '*com.kaleidoscope*' -or $_ -like '*\*' }) { throw 'a dev config or a backslash path is in the payload' }
Write-Output ("payload: " + $names.Count + " files, " + [math]::Round((Get-Item $Out).Length / 1MB, 1) + " MB")

# the icon: Tattletail's face from the logo on a round purple badge (one 256 px PNG image in an .ico)
$logo = [System.Drawing.Image]::FromFile((Join-Path $Repo 'tools\installer\logo.png'))
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
$g.Clear([System.Drawing.Color]::Transparent)
$g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 58, 16, 84))), 4, 4, 248, 248)
$clip = New-Object System.Drawing.Drawing2D.GraphicsPath
$clip.AddEllipse(4, 4, 248, 248)
$g.SetClip($clip)
$g.DrawImage($logo, (New-Object System.Drawing.Rectangle 0, 0, 256, 256), (New-Object System.Drawing.Rectangle 220, 50, 420, 420), 'Pixel')
$g.Dispose(); $logo.Dispose()
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$bmp.Dispose()
$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ico
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]1)                         # an icon file with one image
$w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0)            # 256 x 256, no palette
$w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$png.Length); $w.Write([uint32]22)
$w.Write($png); $w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $Repo 'tools\installer\installer.ico'), $ico.ToArray())
Write-Output 'icon: done'
