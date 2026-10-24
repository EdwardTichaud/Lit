$ErrorActionPreference = 'Stop'
$sourceRoot = 'C:\Users\pierr\git\Lit'
$validationRoot = 'C:\Users\pierr\git\Lit\Library\MovingLegsValidationProject'
if (!(Test-Path -LiteralPath "$validationRoot/ProjectSettings/ProjectVersion.txt") -or
    (Test-Path -LiteralPath "$validationRoot/.git")) { throw 'Invalid isolated validation project.' }
$guidPaths = @{}
foreach ($line in (& rg -uu --no-heading '^guid: [a-f0-9]{32}' "$sourceRoot/Assets" -g '*.meta')) {
    if ($line -match '^(.*)\.meta:guid:\s*([a-f0-9]{32})') { $guidPaths[$Matches[2]] = $Matches[1].Substring($sourceRoot.Length + 1) }
}
$queue = [Collections.Generic.Queue[string]]::new()
foreach ($path in @('Assets/Scenes/Bootstrap.unity','Assets/Scenes/MainMenu/MainMenu.unity',
    'Assets/Scenes/Maison/ZoneManifest_Maison.asset','Assets/Scenes/Maison/ZoneManifest_District_1.asset',
    'Assets/Core/System/GameplaySessionRoot.prefab')) { $queue.Enqueue($path) }
foreach ($file in (Get-ChildItem -LiteralPath "$sourceRoot/Assets/Resources" -Recurse -File)) {
    $queue.Enqueue($file.FullName.Substring($sourceRoot.Length + 1))
}
foreach ($path in (& rg --files -uu "$sourceRoot/Assets" -g '*.cs' -g '*.cs.meta' -g '*.asmdef*' -g '*.asmref*' -g '*.shader' -g '*.hlsl' -g '*.cginc' -g '*.compute' -g '*.dll' -g '*.uxml' -g '*.uss' -g '*.tss')) {
    $queue.Enqueue($path.Substring($sourceRoot.Length + 1))
}
$visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$unresolved = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
while ($queue.Count -gt 0) {
    $relative = $queue.Dequeue().Replace('/','\')
    if (!$visited.Add($relative)) { continue }
    $absolute = Join-Path $sourceRoot $relative
    if (!(Test-Path -LiteralPath $absolute -PathType Leaf)) { continue }
    $files.Add($relative) | Out-Null
    $ancestor = Split-Path -Parent $relative
    while ($ancestor -and $ancestor -ne 'Assets') {
        if (Test-Path -LiteralPath (Join-Path $sourceRoot ($ancestor + '.meta')) -PathType Leaf) {
            $queue.Enqueue($ancestor + '.meta')
        }
        $ancestor = Split-Path -Parent $ancestor
    }
    if (!$relative.EndsWith('.meta') -and (Test-Path -LiteralPath ($absolute + '.meta'))) { $queue.Enqueue($relative + '.meta') }
    $extension = [IO.Path]::GetExtension($relative)
    if ($extension -in @('.meta','.unity','.prefab','.asset','.mat','.controller','.overrideController','.playable','.vfx','.vfxoperator','.vfxblock','.shadergraph','.shadersubgraph','.renderTexture','.lighting','.json','.uxml','.uss','.tss')) {
        $reader = [IO.StreamReader]::new($absolute)
        try {
            $header = [char[]]::new(5)
            $read = $reader.ReadBlock($header,0,5)
            $prefix = -join $header
            if ($extension -in @('.meta','.uxml','.uss','.tss') -or $prefix -eq '%YAML' -or $prefix.StartsWith('{') -or $prefix.StartsWith('[')) {
                $content = $prefix + $reader.ReadToEnd()
                foreach ($match in [regex]::Matches($content, '(?i)(?:guid:\s*|"guid"\s*:\s*"|"m_AssetGUID"\s*:\s*")([a-f0-9]{32})')) {
                    $guid = $match.Groups[1].Value
                    if ($guidPaths.ContainsKey($guid)) { $queue.Enqueue($guidPaths[$guid]) }
                    elseif ($guid -notmatch '^0{16}') { $unresolved.Add($guid) | Out-Null }
                }
                # ShaderGraph embeds asset references inside escaped JSON
                # strings. Follow only GUIDs known to be actual source assets;
                # node/property IDs must not become fake missing dependencies.
                foreach ($match in [regex]::Matches($content, '(?i)(?<![a-f0-9])[a-f0-9]{32}(?![a-f0-9])')) {
                    if ($guidPaths.ContainsKey($match.Value)) { $queue.Enqueue($guidPaths[$match.Value]) }
                }
            }
        } finally { $reader.Dispose() }
    }
}
$copied = 0
$mismatches = 0
$report = foreach ($relative in ($files | Sort-Object)) {
    $source = Join-Path $sourceRoot $relative
    $target = Join-Path $validationRoot $relative
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if (!(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $source -Destination $target -Force
        $copied++
    }
    $matches = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $hash
    if (!$matches) { $mismatches++ }
    "$relative`t$hash`t$matches"
}
$report | Tee-Object -FilePath "$sourceRoot/Library/PersistenceContinuityValidation/production-dependency-hashes.log" | Out-Null
$unresolved | Sort-Object | Tee-Object -FilePath "$sourceRoot/Library/PersistenceContinuityValidation/production-unresolved-guids.log" | Out-Null
foreach ($relative in @('ProjectSettings/EditorBuildSettings.asset','ProjectSettings/ProjectSettings.asset')) {
    Copy-Item -LiteralPath (Join-Path $sourceRoot $relative) -Destination (Join-Path $validationRoot $relative) -Force
}
"Dependencies verified=$($files.Count), copied=$copied, mismatches=$mismatches, unresolved source GUIDs=$($unresolved.Count)"
if ($mismatches -gt 0) { throw 'Validation dependencies differ from source.' }
