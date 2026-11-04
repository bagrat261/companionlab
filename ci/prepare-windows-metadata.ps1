param([string]$OutputDirectory = 'out/sdk')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
New-Item -ItemType Directory -Path $out -Force | Out-Null
$archive = Join-Path $out 'contracts.zip'
$url = 'https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.contracts/10.0.26100.1/microsoft.windows.sdk.contracts.10.0.26100.1.nupkg'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '933D0AEE3BF2EC5AD04D2E19D27609A95EECEC4D3FF0F50B222E2E3931819EF9') { throw 'Microsoft metadata package checksum mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $out 'contracts') -Force
Write-Output (Join-Path $out 'contracts/ref/netstandard2.0')
