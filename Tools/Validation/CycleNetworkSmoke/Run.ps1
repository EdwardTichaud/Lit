param(
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '../../..'),
    [string]$UnityExe,
    [switch]$DialogueTestsOnly,
    [ValidateSet('normal','busy','interrupt','disconnect','late','allgone','disable','timeline','timeline_stop','timeline_service','timeline_missing','timeline_pause','timeline_external_stop','sequence','sequence_interrupt')]
    [string[]]$Scenarios = @('normal','busy','interrupt','disconnect','late','allgone','disable')
)

$ErrorActionPreference = 'Stop'
$cycleRepo = [IO.Path]::GetFullPath($ProjectRoot)
$cycleProject = Join-Path $cycleRepo 'Library/DistrictCycleRuntimeProject'
$cycleReports = Join-Path $cycleRepo 'Library/DistrictCycleValidation'
$cycleAssets = Join-Path $cycleProject 'Assets'
$cycleMarker = Join-Path $cycleProject 'CycleSmokeValidationProject.marker'
if (-not (Test-Path -LiteralPath (Join-Path $cycleRepo 'Assets/Narrative/Cycles/CycleController.cs'))) { throw 'Not a Lit project root.' }
if ((Test-Path -LiteralPath $cycleProject) -and -not (Test-Path -LiteralPath $cycleMarker)) { throw 'Existing validation directory is not marked as disposable; refusing to overwrite it.' }
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($cycleProject.Replace('\','/')) }) {
    throw 'The isolated validation editor is already running. Wait for it to finish.'
}
if (-not $UnityExe) {
    $cycleVersionLine = Get-Content (Join-Path $cycleRepo 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }
    $cycleVersion = $cycleVersionLine.Substring('m_EditorVersion: '.Length).Trim()
    $UnityExe = "C:/Program Files/Unity/Hub/Editor/$cycleVersion/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityExe)) { throw 'Specify the matching Unity editor with -UnityExe.' }
New-Item -ItemType Directory -Path $cycleProject,$cycleAssets,$cycleReports,(Join-Path $cycleProject 'Packages'),(Join-Path $cycleProject 'ProjectSettings') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CycleSmokeValidationProject.marker') -Destination $cycleMarker -Force

# Only code copies are pruned, never directories, authored assets or the source project.
# This prevents renamed/deleted runtime classes lingering between validation runs.
$cycleAssetPrefix = [IO.Path]::GetFullPath($cycleAssets) + [IO.Path]::DirectorySeparatorChar
$cycleCodeExtensions = @('.cs','.dll','.asmdef','.asmref','.rsp')
foreach ($cycleCode in Get-ChildItem -LiteralPath $cycleAssets -Recurse -File | Where-Object { $_.Extension -in $cycleCodeExtensions }) {
    $cycleTarget = [IO.Path]::GetFullPath($cycleCode.FullName)
    if (-not $cycleTarget.StartsWith($cycleAssetPrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'A cleanup target escaped the disposable Assets directory.' }
    Remove-Item -LiteralPath $cycleTarget
    if (Test-Path -LiteralPath ($cycleTarget + '.meta')) { Remove-Item -LiteralPath ($cycleTarget + '.meta') }
}
& robocopy (Join-Path $cycleRepo 'Assets') $cycleAssets *.cs *.cs.meta *.dll *.dll.meta *.asmdef *.asmdef.meta *.asmref *.asmref.meta *.rsp *.rsp.meta /S /XD Editor Tests EditorTests 'Tests~' 'Documentation~' 'Samples~' /NFL /NDL /NJH /NJS /R:1 /W:1
if ($LASTEXITCODE -ge 8) { throw 'Runtime source copy failed.' }
& robocopy (Join-Path $cycleRepo 'Packages') (Join-Path $cycleProject 'Packages') /E /XD Tests 'Tests~' 'Documentation~' 'Samples~' /NFL /NDL /NJH /NJS /R:1 /W:1
if ($LASTEXITCODE -ge 8) { throw 'Embedded package copy failed.' }
Copy-Item -Path (Join-Path $cycleRepo 'ProjectSettings/*') -Destination (Join-Path $cycleProject 'ProjectSettings') -Recurse -Force
& robocopy (Join-Path $cycleRepo 'Assets/UI/TextMesh Pro') (Join-Path $cycleAssets 'UI/TextMesh Pro') /E /XD Documentation /NFL /NDL /NJH /NJS /R:1 /W:1
if ($LASTEXITCODE -ge 8) { throw 'Text UI resource copy failed.' }
New-Item -ItemType Directory -Path (Join-Path $cycleAssets 'Editor') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Runtime') -Filter '*.cs' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $cycleAssets $_.Name) -Force }
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Editor') -Filter '*.cs' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $cycleAssets ('Editor/' + $_.Name)) -Force }

$cycleSubjects = @('Assets/Narrative/Cycles/CycleController.cs','Assets/Narrative/Cycles/CycleController.AutoDialogues.cs',
    'Assets/Narrative/Cycles/CycleAutoDialogueSession.cs','Assets/Narrative/Cycles/CycleProgressionService.cs','Assets/Scripts/DialoguePanelUI.cs',
    'Assets/Scripts/Timeline/TimelineManager.cs','Assets/Scripts/Timeline/TimelineBindingContract.cs',
    'Assets/Scripts/Timeline/TimelineDissolveTarget.cs','Assets/Scripts/Timeline/TimelinePlaybackParticipant.cs')
$cycleHashes = foreach ($cycleSubject in $cycleSubjects) {
    $cycleOriginalHash = (Get-FileHash -LiteralPath (Join-Path $cycleRepo $cycleSubject)).Hash
    $cycleCopyHash = (Get-FileHash -LiteralPath (Join-Path $cycleProject $cycleSubject)).Hash
    if ($cycleOriginalHash -ne $cycleCopyHash) { throw "Runtime subject differs from source: $cycleSubject" }
    [PSCustomObject]@{Subject=$cycleSubject;Hash=$cycleOriginalHash}
}
$cycleRunDirectory = Join-Path $cycleReports ('Run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $cycleRunDirectory | Out-Null
$cycleHashes | Export-Csv -LiteralPath (Join-Path $cycleRunDirectory 'SourceHashes.csv') -NoTypeInformation
if ($DialogueTestsOnly) {
    # Test exact runtime copies without closing the developer's source editor.
    foreach ($cycleTestName in @('CycleAutoDialogueTests.cs','CycleAutoDialogueControllerTests.cs')) {
        Copy-Item -LiteralPath (Join-Path $cycleRepo ('Assets/Narrative/Cycles/Editor/' + $cycleTestName)) -Destination (Join-Path $cycleAssets ('Editor/' + $cycleTestName)) -Force
    }
    $cycleTestRun = Start-Process -FilePath $UnityExe -ArgumentList @('-batchmode','-nographics','-projectPath',('"'+$cycleProject+'"'),
        '-runTests','-testPlatform','EditMode','-testFilter','"^(CycleAutoDialogueTests|CycleAutoDialogueControllerTests)"',
        '-testResults',('"'+(Join-Path $cycleRunDirectory 'DialogueTests.xml')+'"'),
        '-logFile',('"'+(Join-Path $cycleRunDirectory 'DialogueTests.log')+'"')) -WindowStyle Hidden -PassThru
    $cycleTestRun.WaitForExit()
    if ($cycleTestRun.ExitCode -ne 0) { throw "Dialogue tests failed; inspect $cycleRunDirectory/DialogueTests.log" }
    foreach ($cycleSubjectHash in $cycleHashes) {
        if ((Get-FileHash -LiteralPath (Join-Path $cycleRepo $cycleSubjectHash.Subject)).Hash -ne $cycleSubjectHash.Hash) { throw 'A tested source changed during the run; rerun against the current worktree.' }
    }
    Write-Output "Isolated dialogue tests passed. Reports: $cycleRunDirectory"
    return
}
$cycleBuild = Start-Process -FilePath $UnityExe -ArgumentList @('-batchmode','-nographics','-projectPath',('"'+$cycleProject+'"'),
    '-executeMethod','DistrictCycleRuntimeBuild.Build','-logFile',('"'+(Join-Path $cycleRunDirectory 'Build.log')+'"'),'-quit') -WindowStyle Hidden -PassThru
$cycleBuild.WaitForExit()
if ($cycleBuild.ExitCode -ne 0) { throw "Isolated Unity build failed; inspect $cycleRunDirectory/Build.log" }
$cyclePlayer = Join-Path $cycleReports 'Player/CycleSmoke.exe'
if (-not (Test-Path -LiteralPath $cyclePlayer)) { throw 'The isolated build produced no player.' }

$cycleSummary = @()
foreach ($cycleScenario in $Scenarios) {
    # Ask the OS for a currently free loopback UDP port, then release it for UTP.
    $cycleSocket = [Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback,0))
    $cyclePort = $cycleSocket.Client.LocalEndPoint.Port.ToString()
    $cycleSocket.Dispose()
    $cyclePeers = if ($cycleScenario.StartsWith('timeline')) { @(@{Role='single';Name='single'}) }
        elseif ($cycleScenario -eq 'allgone') { @(@{Role='server';Name='server'},@{Role='client';Name='clientA'},@{Role='client';Name='clientB'}) }
        elseif ($cycleScenario -eq 'late') { @(@{Role='host';Name='host'},@{Role='client';Name='client'},@{Role='late';Name='late'}) }
        else { @(@{Role='host';Name='host'},@{Role='client';Name='client'}) }
    $cycleProcesses = @()
    try {
        foreach ($cyclePeer in $cyclePeers) {
            $cycleBase = Join-Path $cycleRunDirectory ($cycleScenario + '.' + $cyclePeer.Name)
            $cycleProcess = Start-Process -FilePath $cyclePlayer -ArgumentList @('-batchmode','-nographics','-role',$cyclePeer.Role,
                '-scenario',$cycleScenario,'-port',$cyclePort,'-result',('"'+$cycleBase+'.txt"'),'-logFile',('"'+$cycleBase+'.log"')) -WindowStyle Hidden -PassThru
            $cycleProcesses += [PSCustomObject]@{Process=$cycleProcess;Result=$cycleBase+'.txt';Scenario=$cycleScenario;Peer=$cyclePeer.Name}
        }
        foreach ($cyclePeerRun in $cycleProcesses) {
            $cycleExited = $cyclePeerRun.Process.WaitForExit(45000)
            $cyclePassed = $cycleExited -and $cyclePeerRun.Process.ExitCode -eq 0 -and (Test-Path -LiteralPath $cyclePeerRun.Result) -and
                [bool](Select-String -LiteralPath $cyclePeerRun.Result -Pattern '^PASS ' -Quiet)
            $cycleSummary += [PSCustomObject]@{Scenario=$cycleScenario;Peer=$cyclePeerRun.Peer;Passed=$cyclePassed;Result=$cyclePeerRun.Result}
        }
    }
    finally {
        # Only processes started by this invocation are eligible for cleanup.
        foreach ($cyclePeerRun in $cycleProcesses) { if (-not $cyclePeerRun.Process.HasExited) { $cyclePeerRun.Process.Kill(); $cyclePeerRun.Process.WaitForExit() } }
        $cycleSummary | Export-Csv -LiteralPath (Join-Path $cycleRunDirectory 'Results.csv') -NoTypeInformation
    }
}
foreach ($cycleSubjectHash in $cycleHashes) {
    if ((Get-FileHash -LiteralPath (Join-Path $cycleRepo $cycleSubjectHash.Subject)).Hash -ne $cycleSubjectHash.Hash) { throw 'A tested source changed during the run; rerun against the current worktree.' }
}
$cycleSummary | Format-Table Scenario,Peer,Passed
if ($cycleSummary.Where({-not $_.Passed}).Count -gt 0) { throw "Network assertions failed; inspect $cycleRunDirectory" }
Write-Output "Protocol checks passed. Reports: $cycleRunDirectory"
