# Converts an mp3/mp4/m4a/wma to a PCM .wav using Windows' built-in media transcoder (no ffmpeg needed).
# Usage: powershell -File tools\convert_to_wav.ps1 -In "C:\path\song.mp3" -Out "C:\path\birthday.wav" [-SampleRate 32000] [-Channels 1]
# Mono is fine for music that plays from a 3D speaker in the room; lower rates keep the file small.
param(
  [Parameter(Mandatory=$true)][string]$In,
  [Parameter(Mandatory=$true)][string]$Out,
  [int]$SampleRate = 32000,
  [int]$Channels = 1
)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, $type) { $t = $asTaskGeneric.MakeGenericMethod($type).Invoke($null, @($op)); $t.Wait(-1) | Out-Null; return $t.Result }
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Storage.StorageFolder,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Media.Transcoding.MediaTranscoder,Windows.Media.Transcoding,ContentType=WindowsRuntime] | Out-Null
[Windows.Media.MediaProperties.MediaEncodingProfile,Windows.Media.MediaProperties,ContentType=WindowsRuntime] | Out-Null
$outDir = Split-Path -Parent (Resolve-Path -LiteralPath (New-Item -ItemType File -Force -Path $Out)).Path
$inFile = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync((Resolve-Path -LiteralPath $In).Path)) ([Windows.Storage.StorageFile])
$folder = Await ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync($outDir)) ([Windows.Storage.StorageFolder])
$outFile = Await ($folder.CreateFileAsync((Split-Path -Leaf $Out), [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])
$profile = [Windows.Media.MediaProperties.MediaEncodingProfile]::CreateWav([Windows.Media.MediaProperties.AudioEncodingQuality]::High)
$profile.Audio.SampleRate = $SampleRate
$profile.Audio.ChannelCount = $Channels
$profile.Audio.BitsPerSample = 16
$profile.Audio.Bitrate = $SampleRate * $Channels * 16
$tc = New-Object Windows.Media.Transcoding.MediaTranscoder
$prep = Await ($tc.PrepareFileTranscodeAsync($inFile, $outFile, $profile)) ([Windows.Media.Transcoding.PrepareTranscodeResult])
if (-not $prep.CanTranscode) { throw "Cannot transcode: $($prep.FailureReason)" }
$action = $prep.TranscodeAsync()

# The transcode runs in the background and dies if this script exits early, so wait here until the
# output file has stopped growing (no reliable status property is exposed to PowerShell).
$last = -1; $stable = 0
while ($stable -lt 8) {
  Start-Sleep -Milliseconds 1000
  $len = (Get-Item -LiteralPath $Out).Length
  if ($len -gt 0 -and $len -eq $last) { $stable++ } else { $stable = 0 }
  $last = $len
}
Write-Output "Done: $Out ($last bytes)"
