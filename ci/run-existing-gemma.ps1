param(
    [Parameter(Mandatory = $true)][string]$ModelDirectory,
    [string]$DataDirectory = 'out/existing-gemma',
    [string]$EngineArchive,
    [switch]$ProbeOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$data = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($DataDirectory)) { $DataDirectory } else { Join-Path $root $DataDirectory }))
$models = (Resolve-Path -LiteralPath $ModelDirectory).Path
$original = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Capri'
foreach ($protectedFolder in @($models, $original)) {
    if ($data -eq $protectedFolder -or $data.StartsWith($protectedFolder.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'CompanionLab data must be separate from the original model and Capri directories.' }
}
if (Get-Process -Name Capri,CompanionLab,llama-server,ollama_llama_server -ErrorAction SilentlyContinue) { throw 'A companion or local model process is already running. Use its existing API or close it yourself before starting another model.' }
$app = Join-Path $root 'out/windows/CompanionLab.exe'
if (-not (Test-Path -LiteralPath $app)) { throw 'Build CompanionLab first.' }
New-Item -ItemType Directory -Path $data -Force | Out-Null
$model = Join-Path $models 'gemma-4-E4B-it-Q4_0.gguf'
$vision = Join-Path $models 'mmproj-gemma-4-E4B-it-Q8_0.gguf'
$required = @(
    @($model, 'A555B900214B477D8880E7832E0B8925E139B0159640036B09FE472B6F2097F2'),
    @($vision, '197F49A93027F9843772BD24A6A9E0BE2A32A788DE5A3DEF330E9C585D86EDD1')
)
foreach ($entry in $required) {
    if ((Get-FileHash -LiteralPath $entry[0] -Algorithm SHA256).Hash -ne $entry[1]) { throw ('Model checksum mismatch: ' + [IO.Path]::GetFileName($entry[0])) }
}
Write-Output 'PASS existing Gemma weights and vision projector match the pinned hashes; original files are read only'
if (-not $EngineArchive) {
    $cache = Join-Path $root 'out/runtime'
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $EngineArchive = Join-Path $cache 'llama-b11669.zip'
    if (-not (Test-Path -LiteralPath $EngineArchive)) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/ggml-org/llama.cpp/releases/download/b11669/llama-b11669-bin-win-vulkan-x64.zip' -OutFile $EngineArchive
    }
}
if ((Get-FileHash -LiteralPath $EngineArchive -Algorithm SHA256).Hash -ne '2C07574E7BDE71321CB785282B20CC821E951132149EECEEEA32DDEBDBF0598E') { throw 'Inference runtime checksum mismatch; it will not run.' }
$runtime = Join-Path $data 'runtime-b11669'
Expand-Archive -LiteralPath $EngineArchive -DestinationPath $runtime -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'llama-LICENSE.txt') -Destination (Join-Path $runtime 'LICENSE-llama.cpp.txt') -Force
$listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$address = 'http://127.0.0.1:' + $port
$arguments = @('-m', ('"' + $model + '"'), '--mmproj', ('"' + $vision + '"'), '--host', '127.0.0.1', '--port', $port, '-c', '4096', '-np', '1', '-a', 'companionlab-gemma', '--jinja', '--reasoning', 'off', '--no-webui', '--log-disable', '-t', '4', '-tb', '4', '-b', '256', '-ub', '128', '--poll', '0', '--poll-batch', '0')
$server = $null
try {
    $server = Start-Process -FilePath (Join-Path $runtime 'llama-server.exe') -ArgumentList $arguments -WorkingDirectory $runtime -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $data 'server-output.txt') -RedirectStandardError (Join-Path $data 'server-errors.txt')
    $server.PriorityClass = 'BelowNormal'
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(120)
    while ([DateTime]::UtcNow -lt $deadline -and -not $server.HasExited) {
        try { $response = Invoke-WebRequest -UseBasicParsing -Uri ($address + '/health') -TimeoutSec 2; if ($response.StatusCode -eq 200) { $ready = $true; break } } catch { }
        Start-Sleep -Milliseconds 400
    }
    if (-not $ready) { throw ('The verified local runtime did not become ready. See ' + (Join-Path $data 'server-errors.txt')) }
    Write-Output ('PASS one verified local model is ready at ' + $address)
    if ($ProbeOnly) {
        $assembly = [Reflection.Assembly]::LoadFrom($app)
        $flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
        $brainType = $assembly.GetType('CapriDesktop.Brain', $true)
        $cloudType = $assembly.GetType('CapriDesktop.Cloud', $true)
        $brain = [Activator]::CreateInstance($brainType, $true)
        $cloud = $cloudType.GetConstructor($flags, $null, [Type[]]@([int], [string], [string]), $null).Invoke([object[]]@([int]$port, 'companionlab-gemma', $address))
        $brainType.GetField('Remote', $flags).SetValue($brain, $cloud)
        $brainType.GetField('Model', $flags).SetValue($brain, 'companionlab-gemma')
        $brainType.GetField('Keeping', $flags).SetValue($null, $false)
        $brainType.GetField('Language', $flags).SetValue($null, 'Russian')
        $brainType.GetField('LogPath', $flags).SetValue($null, (Join-Path $data 'brain-errors.txt'))
        $useCharacter = $brainType.GetMethod('UseCharacter', $flags)
        $ask = $brainType.GetMethod('Ask', $flags, $null, [Type[]]@([string]), $null)
        $useCharacter.Invoke($null, [object[]]@('Capri', $null, $null))
        $first = $ask.Invoke($brain, [object[]]@('Introduce yourself with your own name in one short Russian sentence.'))
        $historyProperty = $brainType.GetProperty('History', $flags)
        $before = $historyProperty.GetValue($brain, $null).Count
        $useCharacter.Invoke($null, [object[]]@('Luna', 'Your name is Luna. You are a calm desktop companion. Use Russian when requested.', $null))
        $second = $ask.Invoke($brain, [object[]]@('Your character has just changed. Introduce yourself with your current name in one short Russian sentence.'))
        $after = $historyProperty.GetValue($brain, $null).Count
        if ($first -notmatch '(?i)Capri|\u041a\u0430\u043f\u0440\u0438' -or $second -notmatch '(?i)Luna|\u041b\u0443\u043d\u0430') { throw 'Gemma did not identify the requested character correctly.' }
        if ([string]::IsNullOrWhiteSpace($first) -or [string]::IsNullOrWhiteSpace($second) -or $before -lt 2 -or $after -lt 4 -or $brainType.GetField('Model', $flags).GetValue($brain) -ne 'companionlab-gemma') { throw 'The real Brain request or history-preservation probe failed.' }
        $result = [ordered]@{ Model = 'Gemma 4 E4B Q4_0'; Runtime = 'llama.cpp b11669 Vulkan'; CapriReply = $first; LunaReply = $second; HistoryBeforeSwitch = $before; HistoryAfterSwitch = $after; SameModel = $true; Transport = 'loopback OpenAI-compatible API'; VisionInferenceTested = $false }
        $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $data 'live-brain-result.json') -Encoding UTF8
        Write-Output ($result | ConvertTo-Json -Depth 4)
        Write-Output 'PASS real CompanionLab Brain answered through existing Gemma and retained history across a personality switch'
    }
    else {
        $settingsPath = Join-Path $data 'settings.json'
        if (Test-Path -LiteralPath $settingsPath) { $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json }
        else { $settings = [PSCustomObject]@{ Size = 240; OnTop = $true; VoiceSpeed = 100; Silent = $true; Language = 'Russian'; Permissions = '1110000000'; CheckIns = $false; FollowUps = $false; CaughtUp = $false; NoDiscord = $true; StartupSet = $true } }
        foreach ($setting in @{ BrainPort = $port; BrainAddress = $address; LocalModel = 'companionlab-gemma'; LocalAddress = $address; Tier = 0; Tiered = $true }.GetEnumerator()) { $settings | Add-Member -NotePropertyName $setting.Key -NotePropertyValue $setting.Value -Force }
        $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
        $companion = Start-Process -FilePath $app -ArgumentList @('--data-dir', ('"' + $data + '"')) -PassThru
        while (-not $companion.WaitForExit(1000)) { }
    }
}
finally {
    if ($server) {
        if (-not $server.HasExited) { $server.Kill(); $server.WaitForExit(5000) | Out-Null }
        $server.Dispose()
    }
}
