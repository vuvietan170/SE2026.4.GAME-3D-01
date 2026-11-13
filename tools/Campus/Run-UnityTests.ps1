param(
    [ValidateSet('EditMode','PlayMode')][string]$Platform='EditMode',
    [string]$ProjectPath='D:/Game3D-01/TempTests/Floor01Save_20261113_001225/ExactProject',
    [Parameter(Mandatory=$true)][string]$OutputPath
)
$ErrorActionPreference='Stop'
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
$original=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../HUS_Escape'))
if($project.TrimEnd('\') -eq $original.TrimEnd('\')){throw 'Do not run a second Editor against the main project.'}
if([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($OutputPath)) -ne 'D:\'){throw 'Reports and cache must stay on D.'}
if(Test-Path -LiteralPath (Join-Path $project 'Temp/UnityLockfile')){throw 'The verification Editor is still open.'}
New-Item -ItemType Directory -Path "$OutputPath/Temp" -Force | Out-Null
$output=[IO.Path]::GetFullPath($OutputPath);$oldTemp=$env:TEMP;$oldTmp=$env:TMP
try {
    $env:TEMP=Join-Path $output 'Temp';$env:TMP=$env:TEMP
    $arguments=@('-batchmode','-projectPath',('"'+$project+'"'),'-giCustomCacheLocation',('"'+(Join-Path $output 'GICache')+'"'),'-runTests','-testPlatform',$Platform,'-testResults',('"'+(Join-Path $output 'results.xml')+'"'),'-logFile',('"'+(Join-Path $output 'editor.log')+'"'))
    $p=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $clock=[Diagnostics.Stopwatch]::StartNew();$timeout=$false
    while(!$p.WaitForExit(1000)){if($clock.Elapsed.TotalSeconds -gt 420){$timeout=$true;$p.Kill();$p.WaitForExit();break}}
    $p.Refresh();@{platform=$Platform;pid=$p.Id;exitCode=$p.ExitCode;timeout=$timeout;seconds=$clock.Elapsed.TotalSeconds;project=$project}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'process.json')
    if($timeout){throw 'Unity test timeout is not a pass.'}
    if(!(Test-Path -LiteralPath (Join-Path $output 'results.xml'))){throw 'No XML test evidence.'}
    [xml]$xml=Get-Content -LiteralPath (Join-Path $output 'results.xml') -Raw
    $xml.'test-run'|Select-Object result,total,passed,failed,skipped
    if($p.ExitCode -ne 0 -or $xml.'test-run'.failed -ne '0'){throw ('Unity tests failed, exit '+$p.ExitCode)}
} finally {$env:TEMP=$oldTemp;$env:TMP=$oldTmp}
