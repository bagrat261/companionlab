param(
    [Parameter(Mandatory = $true)][string]$Model,
    [string]$DataDirectory = 'out/ollama-profile',
    [ValidateSet(16384, 32768)][int]$ContextTokens = 16384,
    [switch]$PrepareOnly,
    [switch]$ProbeOnly
)
$ErrorActionPreference = 'Stop'
if ($PrepareOnly -and $ProbeOnly) { throw 'Choose PrepareOnly or ProbeOnly.' }
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'out/windows/CompanionLab.exe'
if (-not (Test-Path -LiteralPath $app)) { throw 'Build CompanionLab first; see COMPANIONLAB.md.' }
$data = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($DataDirectory)) { $DataDirectory } else { Join-Path $root $DataDirectory }))
$original = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Capri'
if ($data -eq $original -or $data.StartsWith($original + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a data directory separate from original Capri.' }
try { $tags = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/tags' -TimeoutSec 5 }
catch { throw 'Ollama is not reachable at 127.0.0.1:11434. Start the installed Ollama application first. No models were downloaded.' }
if ($Model -notin @($tags.models.name)) { throw ('This model is not installed in Ollama: ' + $Model + '. Run ollama list and use its exact name.') }
$details = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/show' -Method Post -ContentType 'application/json' -Body (@{ model = $Model } | ConvertTo-Json) -TimeoutSec 10
if ('tools' -notin @($details.capabilities)) { throw 'This launcher enables the local agent and requires a model advertising tools support.' }
New-Item -ItemType Directory -Path $data -Force | Out-Null
if ($ProbeOnly) { & (Join-Path $PSScriptRoot 'test-ollama-brain.ps1') -Model $Model -ContextTokens $ContextTokens -OutputDirectory (Join-Path $data 'probe'); return }
if (Get-Process -Name CompanionLab -ErrorAction SilentlyContinue) { throw 'Close CompanionLab before changing its launch profile.' }
$settingsPath = Join-Path $data 'settings.json'
if (Test-Path -LiteralPath $settingsPath) { $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json }
else {
    $settings = [PSCustomObject]@{ Size = 240; OnTop = $true; VoiceSpeed = 100; Silent = $true; Language = 'Russian'; MemorySize = $ContextTokens; Recall = 16; Permissions = '1110000000'; CheckIns = $false; FollowUps = $false; CaughtUp = $false; NoDiscord = $true; StartupSet = $true; Watch = $false; HeyCapri = $false }
}
if ($PSBoundParameters.ContainsKey('ContextTokens')) { $settings | Add-Member -NotePropertyName MemorySize -NotePropertyValue $ContextTokens -Force }
foreach ($entry in @{ Model = $Model; OllamaAgent = $true; BrainPort = 0; BrainAddress = $null; LocalModel = $null; LocalAddress = $null; ProviderOn = $false; Tier = 0; Tiered = $true }.GetEnumerator()) {
    $settings | Add-Member -NotePropertyName $entry.Key -NotePropertyValue $entry.Value -Force
}
$temporary = $settingsPath + '.new'
$settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $temporary -Encoding UTF8
Move-Item -LiteralPath $temporary -Destination $settingsPath -Force
Write-Output ('Prepared CompanionLab with Ollama model: ' + $Model)
Write-Output ('Separate data directory: ' + $data)
if ($PrepareOnly) { return }
Start-Process -FilePath $app -WorkingDirectory $root -ArgumentList @('--data-dir', ('"' + $data + '"')) | Out-Null
