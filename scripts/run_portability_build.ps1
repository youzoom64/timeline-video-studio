param([Parameter(Mandatory=$true)][string]$UnityPath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$logFolder=Join-Path $projectRoot 'Logs'
$null=New-Item -ItemType Directory -Force -Path $logFolder
$env:TIMELINE_STUDIO_BUILD_DIR=Join-Path $projectRoot 'Build_portable'
Write-Output 'Running Unity portability checks and Windows build'
$buildArgs=@('-batchmode','-nographics','-quit','-projectPath',('"'+$projectRoot+'"'),'-executeMethod','StudioPortabilityChecks.Build','-logFile',('"'+(Join-Path $logFolder 'portability-build.log')+'"'))
$buildProcess=Start-Process -FilePath $UnityPath -ArgumentList $buildArgs -WindowStyle Hidden -Wait -PassThru
if ($buildProcess.ExitCode -ne 0) { throw "Unity portability build failed: $($buildProcess.ExitCode)" }
Write-Output 'Portability build completed'
