param(
    [ValidateSet('EditMode','PlayMode')][string]$Platform='PlayMode',
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Phase='verification',
    [string]$TestFilter='',
    [int]$ExpectedTests=0
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($workspace -ne 'D:\Game3D-01\VT103_Work'){throw 'Only VT103_Work is authorized.'}
$project=Join-Path $workspace 'Temp/VT103Verify'
if(Test-Path (Join-Path $project 'Temp/UnityLockfile')){throw 'Verification Editor is already running.'}
$run=Join-Path $workspace ('Evidence/VT103/repair-20261114/'+$Phase)
if(Test-Path $run){throw 'Evidence already exists; choose a new phase.'}
$data=Join-Path $workspace ('Evidence/VT103/r14/'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$cache=Join-Path $project 'Library/VT103RepairCache'
New-Item -ItemType Directory -Path $run,$data,"$cache/Temp","$cache/GI","$cache/UPM" -Force | Out-Null
$unity='C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe'
$arguments=@('-batchmode','-nographics','-projectPath',('"'+$project+'"'),'-giCustomCacheLocation',('"'+$cache+'/GI"'),'-runTests','-testPlatform',$Platform,'-testResults',('"'+$run+'/results.xml"'),'-logFile',('"'+$run+'/editor.log"'))
if($TestFilter){$arguments+=@('-testFilter',$TestFilter)}
$previous=@{}
foreach($name in @('TEMP','TMP','UPM_CACHE_ROOT','VT103_TEST_DATA_ROOT')){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try {
    $env:TEMP="$cache/Temp"; $env:TMP=$env:TEMP; $env:UPM_CACHE_ROOT="$cache/UPM"; $env:VT103_TEST_DATA_ROOT=$data
    @{project=$project;platform=$Platform;filter=$TestFilter;data=$data;branch=(git -C $workspace branch --show-current);head=(git -C $workspace rev-parse HEAD)} | ConvertTo-Json | Set-Content "$run/environment.json"
    ($unity+' '+($arguments -join ' ')) | Set-Content "$run/command.txt"
    $process=Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    @{pid=$process.Id;startedUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content "$run/started.json"
    $watch=[Diagnostics.Stopwatch]::StartNew(); $timedOut=$false
    while(!$process.WaitForExit(1000)){if($watch.Elapsed.TotalSeconds -gt 600){$timedOut=$true; $process.Kill(); $process.WaitForExit(); break}}
    $process.Refresh()
    @{exitCode=$process.ExitCode;timeout=$timedOut;pid=$process.Id;seconds=$watch.Elapsed.TotalSeconds} | ConvertTo-Json | Set-Content "$run/process.json"
    if($timedOut -or !(Test-Path "$run/results.xml")){throw 'No completed XML evidence; not a pass.'}
    [xml]$xml=Get-Content "$run/results.xml" -Raw
    $cases=@($xml.SelectNodes('//test-case'))
    $cases | ForEach-Object { [pscustomobject]@{name=$_.fullname;result=$_.result;message=$_.failure.message.InnerText;stack=$_.failure.'stack-trace'.InnerText} } | ConvertTo-Json -Depth 4 | Set-Content "$run/assertions.json"
    $xml.'test-run' | Select-Object result,total,passed,failed,skipped
    if($process.ExitCode -ne 0 -or $cases.Count -eq 0 -or @($cases | Where-Object result -ne 'Passed').Count -gt 0 -or ($ExpectedTests -gt 0 -and $cases.Count -ne $ExpectedTests)){throw 'Tests did not all pass; inspect preserved XML and log.'}
} finally {
    foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
}
