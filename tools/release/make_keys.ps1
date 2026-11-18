# Makes the update signing key pair ONCE. The PRIVATE key stays on your PC, outside the repo (back it up!).
# The PUBLIC key goes into src/UpdateKey.cs (the mod and the updater check every release against it).
param([string]$Dir = (Join-Path $env:USERPROFILE '.after_hours_keys'))
$priv = Join-Path $Dir 'update_private.xml'
if (Test-Path $priv) { Write-Output "A private key already exists at $priv (not replaced)."; exit 0 }
New-Item -ItemType Directory -Force $Dir | Out-Null
$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider 2048
[System.IO.File]::WriteAllText($priv, $rsa.ToXmlString($true), (New-Object System.Text.UTF8Encoding $false))
$pub = $rsa.ToXmlString($false)
[System.IO.File]::WriteAllText((Join-Path $Dir 'update_public.xml'), $pub, (New-Object System.Text.UTF8Encoding $false))
Write-Output "Private key: $priv  (KEEP IT SECRET AND BACKED UP; if it is lost, players need the installer again)"
Write-Output "Public key:"
Write-Output $pub
