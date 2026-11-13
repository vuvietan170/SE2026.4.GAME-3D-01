param(
    [string]$ProjectPath = 'D:\Game3D-01\QuestV1_Backup\HUS_PlayerDuplicateFix\Project',
    [string]$OutputPath = ('D:\Game3D-01\TempTests\Floor01Save_' + (Get-Date -Format yyyyMMdd_HHmmss)),
    [string]$Method = 'FloorSavePlayValidation.Run',
    [string]$CheckpointDirectory,
    [switch]$Render,
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$original = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../HUS_Escape'))
if ($project.TrimEnd('\') -eq $original.TrimEnd('\')) { throw 'Use the existing verification copy, not the original open project.' }
if ([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($OutputPath)) -ne 'D:\') { throw 'Test output and temporary files must stay on D.' }
if (Test-Path -LiteralPath (Join-Path $project 'Temp/UnityLockfile')) { throw 'The verification project is already open; close only that Editor first.' }
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputPath).Path
$temp = Join-Path $output 'Temp'
New-Item -ItemType Directory -Path $temp -Force | Out-Null
# Install only validation harness files into the disposable copy.
$probeRoot = Join-Path $project 'Assets/PlayerFixValidation'
New-Item -ItemType Directory -Path (Join-Path $probeRoot 'Editor') -Force | Out-Null
foreach ($name in @('FloorSavePlayProbe.cs', 'FloorSavePlayValidation.cs', 'FloorColdProbe.cs', 'FloorColdValidation.cs', 'FloorGuiReadback.cs', 'FloorGuiValidation.cs', 'CameraCollisionProbe.cs', 'CameraCollisionValidation.cs')) {
    $destination = if ($name.EndsWith('Validation.cs')) { Join-Path $probeRoot ('Editor/' + $name) } else { Join-Path $probeRoot $name }
    if (Test-Path -LiteralPath $destination) { Copy-Item -LiteralPath $destination -Destination (Join-Path $output ($name + '.before')) }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $destination
}
$oldTemp = $env:TEMP
$oldTmp = $env:TMP
try {
    $env:TEMP = $temp
    $env:TMP = $temp
    $arguments = @('-projectPath', ('"' + $project + '"'), '-giCustomCacheLocation', ('"' + (Join-Path $output 'GICache') + '"'), '-logFile', ('"' + (Join-Path $output 'floor-save-route.log') + '"'), '-executeMethod', $Method, '-validationReport', ('"' + (Join-Path $output 'floor-save-route.json') + '"'))
    if (-not $Render) { $arguments = @('-batchmode') + $arguments }
    if ($CheckpointDirectory) { $arguments += @('-checkpointDirectory', ('"' + $CheckpointDirectory + '"')) }
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $timedOut = $false
    while (-not $process.WaitForExit(1000)) {
        if ($timer.Elapsed.TotalSeconds -gt 420) { $timedOut = $true; $process.Kill(); $process.WaitForExit(); break }
    }
    $process.Refresh()
    @{ pid = $process.Id; exitCode = $process.ExitCode; timeout = $timedOut; seconds = $timer.Elapsed.TotalSeconds; project = $project } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'floor-save-route-process.json')
    if ($timedOut) { throw 'Unity timed out; this is not a successful test.' }
    if ($process.ExitCode -ne 0) { throw ('Unity failed: exit ' + $process.ExitCode + '. Read floor-save-route.log.') }
    $report = Get-Content -LiteralPath (Join-Path $output 'floor-save-route.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $report.passed) { throw 'The native quest report failed.' }
    Write-Output ('PASS: ' + @($report.checks).Count + ' checks. Report: ' + (Join-Path $output 'floor-save-route.json'))
} finally {
    $env:TEMP = $oldTemp
    $env:TMP = $oldTmp
}
