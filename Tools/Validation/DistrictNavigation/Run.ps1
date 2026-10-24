param([string]$ProjectRoot = (Join-Path $PSScriptRoot '../../..'), [string]$SnapshotPath)
$ErrorActionPreference = 'Stop'
$navRepo = [IO.Path]::GetFullPath($ProjectRoot)
$navMini = Join-Path $navRepo 'Library/DistrictCycleRuntimeProject'
$navSnapshot = Join-Path $navRepo 'Library/DistrictCycleValidation/NavigationProbe.json'
if ($SnapshotPath) { $navSnapshot = [IO.Path]::GetFullPath($SnapshotPath) }
if (-not (Test-Path -LiteralPath (Join-Path $navMini 'CycleSmokeValidationProject.marker'))) { throw 'Prepare the marked validation project with CycleNetworkSmoke first.' }
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($navMini.Replace('\','/')) }) { throw 'The validation editor is still running.' }
$navData = Get-Content -LiteralPath $navSnapshot -Raw | ConvertFrom-Json
$navRelative = $navData.navAssetPath.Replace('\','/')
if (-not $navRelative.StartsWith('Assets/Navigation/NavMeshData/',[StringComparison]::OrdinalIgnoreCase) -or -not $navRelative.EndsWith('.asset')) { throw 'Unexpected navigation data path.' }
$navSource = [IO.Path]::GetFullPath((Join-Path $navRepo $navRelative))
$navExpectedRoot = [IO.Path]::GetFullPath((Join-Path $navRepo 'Assets/Navigation/NavMeshData')) + [IO.Path]::DirectorySeparatorChar
if (-not $navSource.StartsWith($navExpectedRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Navigation source escaped the expected folder.' }
$navDestination = Join-Path $navMini $navRelative
New-Item -ItemType Directory -Path (Split-Path $navDestination) -Force | Out-Null
Copy-Item -LiteralPath $navSource -Destination $navDestination -Force
Copy-Item -LiteralPath ($navSource + '.meta') -Destination ($navDestination + '.meta') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Editor/DistrictNavigationAudit.cs') -Destination (Join-Path $navMini 'Assets/Editor/DistrictNavigationAudit.cs') -Force
$navReport = Join-Path $navRepo ('Library/DistrictCycleValidation/NavigationAudit-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json')
$navVersionLine = Get-Content (Join-Path $navRepo 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }
$navVersion = $navVersionLine.Substring('m_EditorVersion: '.Length).Trim()
$navUnity = "C:/Program Files/Unity/Hub/Editor/$navVersion/Editor/Unity.exe"
$navProcess = Start-Process -FilePath $navUnity -ArgumentList @('-batchmode','-nographics','-projectPath',('"'+$navMini+'"'),'-executeMethod','DistrictNavigationAudit.Run','-navSnapshot',('"'+$navSnapshot+'"'),'-navReport',('"'+$navReport+'"'),'-logFile',('"'+$navReport+'.log"'),'-quit') -WindowStyle Hidden -PassThru
Write-Output ('Native navigation audit process: ' + $navProcess.Id)
$navProcess.WaitForExit()
if ($navProcess.ExitCode -ne 0) { throw "Navigation audit failed to execute; inspect $navReport.log" }
Write-Output ('Navigation audit report: ' + $navReport)
