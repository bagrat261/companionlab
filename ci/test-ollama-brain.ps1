param(
    [Parameter(Mandatory = $true)][string]$Model,
    [ValidateSet(16384, 32768)][int]$ContextTokens = 16384,
    [string]$OutputDirectory = 'out/ollama-probe'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $root $OutputDirectory }))
New-Item -ItemType Directory -Path $out -Force | Out-Null
$loaded = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/ps' -TimeoutSec 5
if (@($loaded.models | Where-Object { $_.name -ne $Model }).Count -gt 0) { throw 'Another model is loaded. Stop it yourself before this probe to avoid loading two heavy models.' }
$wasLoaded = @($loaded.models | Where-Object { $_.name -eq $Model }).Count -gt 0
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'out/windows/CompanionLab.exe'))
$flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$brainType = $assembly.GetType('CapriDesktop.Brain', $true)
$handsType = $assembly.GetType('CapriDesktop.Hands', $true)
$brain = [Activator]::CreateInstance($brainType, $true)
$brainType.GetField('Model', $flags).SetValue($brain, $Model)
$brainType.GetField('OwnSteps', $flags).SetValue($brain, $true)
$brainType.GetField('OllamaSteps', $flags).SetValue($brain, $true)
$brainType.GetField('Keeping', $flags).SetValue($null, $false)
$brainType.GetField('Language', $flags).SetValue($null, 'Russian')
$brainType.GetField('MemorySize', $flags).SetValue($null, $ContextTokens)
$brainType.GetField('Recall', $flags).SetValue($null, 16)
$brainType.GetField('LogPath', $flags).SetValue($null, (Join-Path $out 'brain-errors.txt'))
$useCharacter = $brainType.GetMethod('UseCharacter', $flags)
$ask = $brainType.GetMethod('Ask', $flags, $null, [Type[]]@([string]), $null)
$history = $brainType.GetProperty('History', $flags)
try {
    $useCharacter.Invoke($null, [object[]]@('Capri', $null, $null))
    $first = [string]$ask.Invoke($brain, [object[]]@('Introduce yourself with your name in one short Russian sentence.'))
    $before = $history.GetValue($brain, $null).Count
    $useCharacter.Invoke($null, [object[]]@('Luna', 'Your current name is Luna. You are a calm desktop companion. Speak Russian when asked.', $null))
    $second = [string]$ask.Invoke($brain, [object[]]@('Your character just changed. Introduce yourself with your current name in one short Russian sentence.'))
    $after = $history.GetValue($brain, $null).Count
    $identityMatched = $first -match '(?i)Capri|\u041a[\u0430\u044d]\u043f\u0440\u0438' -and $second -match '(?i)Luna|\u041b\u0443\u043d\u0430'
    if ([string]::IsNullOrWhiteSpace($first) -or [string]::IsNullOrWhiteSpace($second) -or $first -notmatch '[\u0400-\u04ff]' -or $second -notmatch '[\u0400-\u04ff]' -or $before -ne 2 -or $after -ne 4) {
        @{ First = $first; Second = $second; HistoryBefore = $before; HistoryAfter = $after } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'conversation-failure.json') -Encoding UTF8
        throw 'Real Brain conversation, character identity or history probe failed; see conversation-failure.json.'
    }
    $calls = New-Object 'Collections.Generic.List[string]'
    $perform = $handsType.GetMethod('Do', $flags)
    $callback = [Func[object,object,string]]{
        param($tool, $arguments)
        if ($tool -ne 'list_windows') { throw ('Probe refused a non-whitelisted tool: ' + $tool) }
        $result = [string]$perform.Invoke($null, [object[]]@($tool, $arguments))
        if ([string]::IsNullOrWhiteSpace($result) -or $result.StartsWith("Couldn't")) { throw 'The real Windows read-only tool failed.' }
        $calls.Add([string]$tool)
        return $result
    }.GetNewClosure()
    $act = $brainType.GetField('Act', $flags)
    $signature = $act.FieldType.GetMethod('Invoke').GetParameters()
    $parameters = [Linq.Expressions.ParameterExpression[]]@(
        [Linq.Expressions.Expression]::Parameter($signature[0].ParameterType, 'tool'),
        [Linq.Expressions.Expression]::Parameter($signature[1].ParameterType, 'arguments')
    )
    $invoke = [Linq.Expressions.Expression]::Invoke([Linq.Expressions.Expression]::Constant($callback), [Linq.Expressions.Expression[]]@(
        [Linq.Expressions.Expression]::Convert($parameters[0], [object]),
        [Linq.Expressions.Expression]::Convert($parameters[1], [object])
    ))
    $delegate = [Linq.Expressions.Expression]::Lambda($act.FieldType, $invoke, $parameters).Compile()
    $act.SetValue($brain, $delegate)
    $preflight = [string]$delegate.DynamicInvoke([object[]]@('list_windows', [Activator]::CreateInstance($signature[1].ParameterType, $true)))
    if ($calls.Count -ne 1) { throw 'Windows tool delegate preflight failed.' }
    $calls.Clear()
    $plannerBefore = $brainType.GetMethod('WorksOut', $flags).Invoke($brain, @())
    $third = [string]$ask.Invoke($brain, [object[]]@('Show me which windows are open using list_windows. Use no other tools and change nothing. Reply in one short Russian sentence without window titles.'))
    if ($calls.Count -lt 1) {
        @{ PlannerEnabled = $plannerBefore; ToolReply = $third } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'tool-failure.json') -Encoding UTF8
        throw 'Brain did not call the real list_windows tool; see tool-failure.json.'
    }
    $runtime = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/ps' -TimeoutSec 5
    $activeModel = @($runtime.models | Where-Object { $_.name -eq $Model }) | Select-Object -First 1
    $result = [ordered]@{ Model = $Model; ContextTokens = $ContextTokens; RuntimeContextTokens = $activeModel.context_length; ModelVramBytes = $activeModel.size_vram; CapriReply = $first; LunaReply = $second; CharacterNamesMatched = $identityMatched; HistoryBeforeSwitch = $before; HistoryAfterSwitch = $after; ToolCalls = @($calls.ToArray()); WindowsToolExecuted = $true; ComputerClickingTested = $false; MultiStepPlanningTested = $false; ToolReplyPresent = -not [string]::IsNullOrWhiteSpace($third) }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
    Write-Output 'PASS real Ollama Brain: Russian character replies, preserved history and real read-only Windows tool execution.'
    Write-Output ('Results: ' + (Join-Path $out 'result.json'))
    if (-not $identityMatched) { Write-Warning 'LIMIT: this model changed a character name in its reply; inspect the saved replies. This is separate from transport, history and tool execution.' }
}
finally {
    if (-not $wasLoaded) {
        $body = @{ model = $Model; keep_alive = 0 } | ConvertTo-Json
        Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/generate' -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 15 | Out-Null
    }
}
