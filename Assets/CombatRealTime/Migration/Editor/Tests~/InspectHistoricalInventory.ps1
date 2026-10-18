param(
    [string]$ProjectRoot = (Get-Location).Path,
    [string]$ReportPath = 'Library/ActorAnimationSandbox/Library/ProductionCombatMigration/dry-run.json',
    [string]$OutputPath = 'Library/ProductionCombatMigration/lot1-guid-provenance.json'
)
$ErrorActionPreference = 'Stop'
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
Add-Type -Path (Join-Path $PSScriptRoot '../CombatInventoryReference.cs')
function Index-Metadata([string]$Root) {
    $index = @{}
    $directories = @('Assets', 'Packages', 'Library/PackageCache') | ForEach-Object { Join-Path $Root $_ } | Where-Object { Test-Path -LiteralPath $_ }
    & rg --no-heading -n '^guid: [a-fA-F0-9]{32}\s*$' @directories -g '*.meta' | ForEach-Object {
        if ($_ -match '^(.*):\d+:guid: (\w+)\s*$') {
            $guid = $matches[2]
            if (!$index.ContainsKey($guid)) { $index[$guid] = [Collections.Generic.List[string]]::new() }
            $index[$guid].Add($matches[1])
        }
    }
    return $index
}
function Inspect-Guid([string]$Root, $Index, [string]$Guid) {
    $metas = @($Index[$Guid] | Where-Object { $_ })
    $proof = @($metas | ForEach-Object {
        $asset = $_ -replace '\.meta$', ''
        $text = [IO.File]::ReadAllText($_)
        [ordered]@{
            meta = [IO.Path]::GetRelativePath($Root, $_).Replace('\', '/')
            metaSha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
            payloadExists = [IO.File]::Exists($asset)
            importer = [regex]::Match($text, '(?m)^(\w+Importer):').Groups[1].Value
        }
    })
    $classification = if ($metas.Count -gt 1) { 'ambiguous-guid-metadata' } else {
        [CombatInventoryReference]::ClassifyGuid($Guid, $(if ($metas.Count) { $metas[0] -replace '\.meta$', '' } else { '' }), ($metas.Count -eq 1 -and $proof[0].payloadExists))
    }
    [ordered]@{
        classification = $classification
        proof = $proof
        reason = switch ($classification) {
            'asset-guid' { 'Exact metadata GUID and payload exist in this root; imported object/fileID availability still requires Unity import.' }
            'missing-guid-payload' { 'Exact metadata exists but its asset payload is absent; not an import-generated exemption.' }
            'ambiguous-guid-metadata' { 'Multiple metadata owners; cannot choose an identity.' }
            default { 'No matching metadata/payload in this root; no prefix, vendor or parent-project exemption.' }
        }
    }
}
function Call-Hash([string]$Value) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))).Replace('-', '').ToLowerInvariant().Substring(0, 16)) }
    finally { $hash.Dispose() }
}
$reportFile = Join-Path $ProjectRoot $ReportPath
$report = Get-Content -LiteralPath $reportFile -Raw | ConvertFrom-Json
$sourceIndex = Index-Metadata $ProjectRoot
$sandboxRoot = Join-Path $ProjectRoot 'Library/ActorAnimationSandbox'
$sandboxIndex = Index-Metadata $sandboxRoot
$references = @($report.blockers | Where-Object category -eq 'unresolved-reachable-guid' | ForEach-Object {
    $guid = [regex]::Match($_.detail, '[a-fA-F0-9]{32}').Value
    if (!$guid) { throw "Unrecognized historical GUID finding: $($_.detail)" }
    [ordered]@{
        owner = $_.path
        guid = $guid
        source = Inspect-Guid $ProjectRoot $sourceIndex $guid
        sandbox = Inspect-Guid $sandboxRoot $sandboxIndex $guid
    }
})
$resourceFiles = @(& rg --files (Join-Path $ProjectRoot 'Assets') | Where-Object { $_ -notlike '*.meta' -and $_.Replace('\', '/').Contains('/Resources/') })
$dynamic = @($report.blockers | Where-Object category -eq 'unknown-dynamic-reference' | ForEach-Object {
    $finding = $_
    $line = [int][regex]::Match($finding.detail, '^load:.*:(\d+):[a-f0-9]{16}').Groups[1].Value
    $sourcePath = Join-Path $ProjectRoot $finding.path
    if (!(Test-Path -LiteralPath $sourcePath) -and $finding.path.StartsWith('Packages/')) {
        $segments = $finding.path.Split('/')
        $packages = @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'Library/PackageCache') -Directory | Where-Object Name -like ($segments[1] + '@*'))
        if ($packages.Count -ne 1) { throw "Cannot map package source $($finding.path)" }
        $sourcePath = Join-Path $packages[0].FullName (($segments | Select-Object -Skip 2) -join '/')
    }
    $source = [IO.File]::ReadAllText($sourcePath)
    $code = [CombatInventoryReference]::WithoutComments($source)
    $callHash = [regex]::Match($finding.detail, '^load:.*:\d+:([a-f0-9]{16})').Groups[1].Value
    $calls = @([CombatInventoryReference]::Loads.Matches($code) | Where-Object {
        (Call-Hash $_.Value) -eq $callHash
    })
    # Repeated identical calls (healing server/result) are distinguished by the original line when available.
    if ($calls.Count -gt 1) { $calls = @($calls | Where-Object { $code.Substring(0, $_.Index).Split("`n").Count -eq $line }) }
    if ($calls.Count -gt 1) { throw "Ambiguous historical call site: $($finding.detail)" }
    $expression = if ($calls.Count) { $calls[0].Groups[2].Value.Trim() } else { '' }
    $key = ''
    $prefix = $false
    $classification = if (!$calls.Count) {
        if (@([CombatInventoryReference]::Loads.Matches($source) | Where-Object { (Call-Hash $_.Value) -eq $callHash }).Count) { 'comment-only' }
        else { 'source-changed-requires-review' }
    } elseif ([CombatInventoryReference]::TryConstant($code, $expression, [ref]$key)) { 'literal-or-constant' }
        elseif ([CombatInventoryReference]::TryResourcePrefix($expression, [ref]$key)) { $prefix = $true; 'bounded-resource-namespace' } else { 'mutable-candidate-only-blocker' }
    $keys = if ($key) { @($key) } else { @([CombatInventoryReference]::ConstantChoices($code, $expression)) }
    if ($finding.path -eq 'Assets/Scripts/AudioManager.cs') { $keys += 'Audio/ActionAudioLibrary_Default' }
    $candidates = @($resourceFiles | Where-Object {
        $path = $_.Replace('\', '/')
        $resourceKey = $path.Substring($path.IndexOf('/Resources/') + 11)
        $resourceKey = $resourceKey.Substring(0, $resourceKey.Length - [IO.Path]::GetExtension($resourceKey).Length)
        ($prefix -and $resourceKey.StartsWith($key, [StringComparison]::Ordinal)) -or (!$prefix -and $resourceKey -cin $keys)
    } | ForEach-Object { [IO.Path]::GetRelativePath($ProjectRoot, $_).Replace('\', '/') })
    [ordered]@{
        owner = $finding.path; historicalLine = $line; currentLine = $(if ($calls.Count) { $code.Substring(0, $calls[0].Index).Split("`n").Count } else { $null }); classification = $classification; expression = $expression; discoveredKeys = $keys
        sourceCandidates = $candidates
        sandboxCandidatesPresent = @($candidates | Where-Object { Test-Path -LiteralPath (Join-Path $sandboxRoot $_) })
        reason = 'Current-source lexical/key inventory only; no runtime-load, generic type or import success inferred. Mutable expressions retain blockers.'
    }
})
$evidence = [ordered]@{
    schema = 'combat-lot1-historical-guid-inspection-v1'
    reportSha256 = (Get-FileHash -LiteralPath $reportFile -Algorithm SHA256).Hash.ToLowerInvariant()
    report = $ReportPath
    sourceRoot = $ProjectRoot
    sandboxRoot = $sandboxRoot
    scope = 'Exhaustive historical path/GUID pairs, not a new Unity dry-run, import validation or readiness attestation.'
    count = $references.Count
    uniqueGuids = @($references.guid | Sort-Object -Unique).Count
    sourceSummary = @($references | Group-Object { $_.source.classification } | Select-Object Count, Name)
    sandboxSummary = @($references | Group-Object { $_.sandbox.classification } | Select-Object Count, Name)
    dynamicReferences = $dynamic
    references = $references
}
$output = Join-Path $ProjectRoot $OutputPath
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
[IO.File]::WriteAllText($output, ($evidence | ConvertTo-Json -Depth 12))
$evidence.sourceSummary | Format-Table
$evidence.sandboxSummary | Format-Table
Write-Output "Inspected $($references.Count) pairs / $($evidence.uniqueGuids) GUIDs. Evidence: $output"
