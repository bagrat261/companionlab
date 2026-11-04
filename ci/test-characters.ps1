param([string]$OutputDirectory = 'out/character-tests')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$arguments = @('/nologo', '/target:exe', '/warn:4', '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Runtime.Serialization.dll', ('/out:' + (Join-Path $out 'CharacterTests.exe')), (Join-Path $root 'app/Characters.cs'), (Join-Path $root 'tests/CharacterTests.cs'))
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Character tests did not compile.' }
& (Join-Path $out 'CharacterTests.exe') (Join-Path $out ([Guid]::NewGuid().ToString('N')))
if ($LASTEXITCODE -ne 0) { throw 'Character tests failed.' }
