param([string]$OutputDirectory, [switch]$SmokeTest, [int]$SmokeSeconds = 60, [string]$MetadataDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$wpf = Join-Path $framework 'WPF'
$winmd = Get-ChildItem -LiteralPath (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\UnionMetadata') -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^\d+(\.\d+)+$' } | Sort-Object { [version]$_.Name } -Descending | ForEach-Object { Join-Path $_.FullName 'Windows.winmd' } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not (Test-Path -LiteralPath $compiler)) { throw 'csc.exe not found. Capri needs the .NET Framework 4.8 that comes with Windows 10 and 11.' }
if ($MetadataDirectory) {
    $winmd = (Get-ChildItem -LiteralPath $MetadataDirectory -Filter *.winmd | ForEach-Object { $_.FullName }) -join ','
}
if (-not $winmd) { throw 'Windows metadata not found. Install the Windows 10/11 SDK or pass -MetadataDirectory with Microsoft.Windows.SDK.Contracts references.' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $root 'out\windows' }
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
function Invoke-Compiler([object[]]$Arguments, [string]$Name) {
    $response = Join-Path $out ($Name + '.rsp')
    [IO.File]::WriteAllLines($response, @($Arguments | ForEach-Object { '"' + $_ + '"' }))
    & $compiler ('@' + $response)
    if ($LASTEXITCODE -ne 0) { throw ($Name + ' compilation failed.') }
}
$app = Join-Path $out 'CompanionLab.exe'
$setup = Join-Path $out 'CompanionLab-Setup.exe'
$icon = Join-Path $root 'assets\chip.ico'
$legal = @(
    ('/resource:' + (Join-Path $root 'LICENSE.md') + ',CompanionLab.LICENSE.md'),
    ('/resource:' + (Join-Path $root 'NOTICE.md') + ',CompanionLab.NOTICE.md'),
    ('/resource:' + (Join-Path $root 'assets\fonts\OFL-ShareTechMono.txt') + ',CompanionLab.OFL-ShareTechMono.txt')
)
$shared = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output', '/warn:4', ('/win32manifest:' + (Join-Path $root 'app\app.manifest')), ('/win32icon:' + $icon), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll')
$references = @('/reference:System.Runtime.Serialization.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll', '/reference:System.Security.dll', ('/reference:' + (Join-Path $wpf 'UIAutomationClient.dll')), ('/reference:' + (Join-Path $wpf 'UIAutomationTypes.dll')), ('/reference:' + (Join-Path $wpf 'WindowsBase.dll')), ('/reference:' + (Join-Path $wpf 'System.Speech.dll')), ('/reference:' + $winmd), ('/reference:' + (Join-Path $framework 'System.Runtime.dll')), ('/reference:' + (Join-Path $framework 'System.Runtime.WindowsRuntime.dll')), ('/reference:' + (Join-Path $framework 'System.Runtime.InteropServices.WindowsRuntime.dll')))
$resources = @(('/resource:' + $icon + ',Capri.Chip.ico'), ('/resource:' + (Join-Path $root 'assets\fonts\ShareTechMono-Regular.ttf') + ',Capri.Font.Mono.ttf'))
$resources += $legal
$resources += ('/resource:' + (Join-Path $root 'characters\capri\character.json') + ',CompanionLab.Character.capri.json')
$resources += ('/resource:' + (Join-Path $root 'assets\cursor.png') + ',Capri.Cursor.png')
$resources += ('/resource:' + (Join-Path $root 'assets\vbcable\VBCABLE_Driver_Pack45.zip') + ',Capri.VBCABLE.zip')
$resources += Get-ChildItem -LiteralPath (Join-Path $root 'assets\sprites') -Filter *.png | ForEach-Object { '/resource:' + $_.FullName + ',Capri.Sprite.' + $_.Name }
$resources += Get-ChildItem -LiteralPath (Join-Path $root 'assets\voice') -Filter *.wav | ForEach-Object { '/resource:' + $_.FullName + ',Capri.Voice.' + $_.Name }
$sources = @('Capri', 'Characters', 'CharacterHost', 'CharacterChecks', 'ActionJournal', 'Asides', 'Bolts', 'Commentary', 'Lookout', 'Brain', 'Talk', 'Mainframe', 'MainframeTips', 'TipChecks', 'MediumChecks', 'FreeKeyChecks', 'Neon', 'Notice', 'Engine', 'Ledges', 'Voice', 'Ears', 'MicMix', 'CableSetup', 'Dictation', 'HearingStatus', 'Agent', 'Hands', 'GameHud', 'Computer', 'Arrange', 'Mixer', 'Usage', 'Network', 'Play', 'VoicePack', 'Startup', 'Presence', 'Player', 'Ocr', 'GameEars', 'GameNotes', 'GameKeys', 'Frames', 'Reminders', 'GameSense', 'GameWatch', 'SelfKnow', 'Care', 'Clips', 'ClipSettings', 'ClipChecks', 'Minis', 'Chess', 'ChessBoard', 'Files', 'FileFinder', 'Websites', 'Ink', 'Clones', 'ChaosScreen', 'Icons', 'FollowUps', 'WatchAlong', 'CallMode', 'CallEars', 'CallPanel', 'Reactions', 'ToolChecks', 'SmokeRun', 'AssemblyInfo') | ForEach-Object { Join-Path $root ('app\' + $_ + '.cs') }
Invoke-Compiler ($shared + $references + $resources + @('/unsafe+', ('/out:' + $app)) + $sources) 'application'
if ($LASTEXITCODE -ne 0) { throw 'Capri compilation failed.' }
Invoke-Compiler ($shared + $legal + @(('/out:' + $setup), '/reference:Microsoft.CSharp.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll', ('/resource:' + $app + ',CompanionLab.App.exe'), ('/resource:' + $icon + ',CompanionLab.Chip.ico'), ('/resource:' + (Join-Path $root 'assets\chip.png') + ',CompanionLab.Chip.png'), (Join-Path $root 'app\Setup.cs'), (Join-Path $root 'app\Neon.cs'), (Join-Path $root 'app\Notice.cs'), (Join-Path $root 'app\VoicePack.cs'), (Join-Path $root 'app\Startup.cs'), (Join-Path $root 'app\AssemblyInfo.cs'))) 'installer'
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Write-Output "Built $app and $setup"
Copy-Item -LiteralPath (Join-Path $root 'LICENSE.md'), (Join-Path $root 'NOTICE.md'), (Join-Path $root 'assets\fonts\OFL-ShareTechMono.txt') -Destination $out -Force
if (-not $SmokeTest) { return }
$runtime = Join-Path $out 'smoke\runtime'
$package = Join-Path $out 'smoke\package'
$check = Start-Process -FilePath $app -ArgumentList @('--smoke-test', ('"' + $runtime + '"')) -PassThru -WindowStyle Hidden
if (-not $check.WaitForExit($SmokeSeconds * 1000)) { $check.Kill(); throw 'Runtime smoke test timed out.' }
if (Test-Path -LiteralPath (Join-Path $runtime 'smoke-test.txt')) { Get-Content -LiteralPath (Join-Path $runtime 'smoke-test.txt') }
elseif ($check.ExitCode -eq 0) { throw 'Runtime smoke test wrote no results. Another smoke test probably held the lock; rerun it when that one ends.' }
if ($check.ExitCode -ne 0) {
    if (Test-Path -LiteralPath (Join-Path $runtime 'failure.txt')) { Get-Content -LiteralPath (Join-Path $runtime 'failure.txt') }
    throw 'Runtime smoke test failed.'
}
$verify = Start-Process -FilePath $setup -ArgumentList @('--verify', ('"' + $package + '"')) -PassThru -WindowStyle Hidden
if (-not $verify.WaitForExit($SmokeSeconds * 1000)) { $verify.Kill(); throw 'Installer check timed out.' }
if ($verify.ExitCode -ne 0) {
    if (Test-Path -LiteralPath (Join-Path $package 'failure.txt')) { Get-Content -LiteralPath (Join-Path $package 'failure.txt') }
    throw 'Installer check failed.'
}
if ((Get-FileHash -LiteralPath (Join-Path $package 'CompanionLab.exe')).Hash -ne (Get-FileHash -LiteralPath $app).Hash) { throw 'Installer payload differs from the app executable.' }
Get-Content -LiteralPath (Join-Path $package 'packaging-test.txt')
Write-Output 'PASS installer payload matches the app'
