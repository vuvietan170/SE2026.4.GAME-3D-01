param(
    [string]$ProjectPath='D:/Game3D-01/TempTests/Floor01Save_20261113_001225/ExactProject',
    [string]$OutputPath=('D:/Game3D-01/TempTests/Campus_'+(Get-Date -Format yyyyMMdd_HHmmss)),
    [string]$Method='CampusPlayValidation.Run',
    [string]$CheckpointDirectory,
    [switch]$Render
)
$ErrorActionPreference='Stop'
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
$original=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../HUS_Escape'))
if($project.TrimEnd('\') -eq $original.TrimEnd('\')){throw 'Use the D: test copy, never install probes in the main project.'}
if([IO.Path]::GetPathRoot($project) -ne 'D:\'){throw 'Test project must stay on D.'}
if(Test-Path -LiteralPath (Join-Path $project 'Temp/UnityLockfile')){throw 'Verification project already open.'}
$probeRoot=Join-Path $project 'Assets/PlayerFixValidation'
New-Item -ItemType Directory -Path (Join-Path $probeRoot 'Editor') -Force | Out-Null
foreach($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs'){
    $target=if($file.Name.EndsWith('Probe.cs')){Join-Path $probeRoot $file.Name}else{Join-Path (Join-Path $probeRoot 'Editor') $file.Name}
    Copy-Item -LiteralPath $file.FullName -Destination $target
}
& (Join-Path $PSScriptRoot '../Floor01Save/Run-FloorSave.ps1') -ProjectPath $project -OutputPath $OutputPath -Method $Method -CheckpointDirectory $CheckpointDirectory -Render:$Render
