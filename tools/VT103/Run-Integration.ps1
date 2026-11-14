param([string]$UnityPath='C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe', [int]$ExpectedTests=12, [string]$Phase="gameplay-green", [string]$Platform="PlayMode", [string]$TestFilter="HUSEscape.Persistence.Autosave.Tests.Floor01AutosaveIntegrationTests")
$ErrorActionPreference='Stop'
$worktree=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project=Join-Path $worktree 'HUS_Escape'
$filter=$TestFilter
if([IO.Path]::GetPathRoot($project) -ne 'D:\'){throw 'Project and cache must remain on D.'}
if((git -C $worktree branch --show-current) -ne 'individual/23001853-vt103-autosave'){throw 'Wrong branch; never run against the main project.'}
if((git -C $worktree rev-parse HEAD) -ne 'f933595ef7d1aa810032ddd54cafaad4a438aea4'){throw 'Unexpected baseline.'}
if(Test-Path -LiteralPath (Join-Path $project 'Temp/UnityLockfile')){throw 'Worktree Editor is open or has an unreviewed stale lock.'}
$root=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'current-integration.txt') -Raw).Trim()
$run=Join-Path $root ($Phase+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$cache=Join-Path $project 'Library/VT103RunnerCache'
New-Item -ItemType Directory -Path $run,(Join-Path $cache 'Temp'),(Join-Path $cache 'GI'),(Join-Path $cache 'UPM') -Force|Out-Null
$argsUnity=@('-batchmode','-nographics','-projectPath',('"'+$project+'"'),'-giCustomCacheLocation',('"'+(Join-Path $cache 'GI')+'"'),'-runTests','-testPlatform',$Platform,'-testFilter',$filter,'-testResults',('"'+(Join-Path $run 'results.xml')+'"'),'-logFile',('"'+(Join-Path $run 'editor.log')+'"'))
# Preserve only known editor-normalized configuration files, including any existing bytes.
# Copies of post-import changes are retained; never reset/checkout the worktree.
$configBefore=@{}
foreach($rel in @('HUS_Escape/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset','HUS_Escape/ProjectSettings/EditorBuildSettings.asset','HUS_Escape/ProjectSettings/ProjectAuditorSettings.asset','HUS_Escape/ProjectSettings/ShaderGraphSettings.asset','HUS_Escape/ProjectSettings/ProjectSettings.asset')){
 $file=Join-Path $worktree $rel
 if(Test-Path -LiteralPath $file){$configBefore[$rel]=[IO.File]::ReadAllBytes($file)}
}
$envNames=@('TEMP','TMP','UPM_CACHE_ROOT','VT103_TEST_DATA_ROOT');$previous=@{}
foreach($name in $envNames){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try {
 $env:TEMP=Join-Path $cache 'Temp';$env:TMP=$env:TEMP;$env:UPM_CACHE_ROOT=Join-Path $cache 'UPM';$testData=Join-Path 'D:/Game3D-01/TempTests/VT103' ([Guid]::NewGuid().ToString('N')); New-Item -ItemType Directory -Path $testData -Force|Out-Null; $env:VT103_TEST_DATA_ROOT=$testData
 ('& "'+$UnityPath+'" '+($argsUnity -join ' '))|Set-Content -LiteralPath (Join-Path $run 'command.txt') -Encoding UTF8
 @{baseline=(git -C $worktree rev-parse HEAD);branch=(git -C $worktree branch --show-current);project=$project;filter=$filter;unity=$UnityPath;testData=$env:VT103_TEST_DATA_ROOT;temp=$env:TEMP;upmCache=$env:UPM_CACHE_ROOT}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'environment.json') -Encoding UTF8
 Write-Output ('Evidence: '+$run)
 $process=Start-Process -FilePath $UnityPath -ArgumentList $argsUnity -WindowStyle Hidden -PassThru
 $watch=[Diagnostics.Stopwatch]::StartNew();$timedOut=$false
 while(!$process.WaitForExit(1000)){if($watch.Elapsed.TotalSeconds -gt 180){$timedOut=$true;$process.Kill();$process.WaitForExit();break}}
 $process.Refresh()
 @{exitCode=$process.ExitCode;pid=$process.Id;timeout=$timedOut;seconds=$watch.Elapsed.TotalSeconds}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'process.json') -Encoding UTF8
 if($timedOut){throw 'Timeout is BLOCKED, not GREEN.'}
 $xmlPath=Join-Path $run 'results.xml'
 if(!(Test-Path -LiteralPath $xmlPath)){throw 'Missing NUnit XML: compilation/licensing/startup failure is not GREEN.'}
 [xml]$xml=Get-Content -LiteralPath $xmlPath -Raw
 $cases=@($xml.SelectNodes('//test-case'))
 $summary=@($cases|ForEach-Object{[pscustomobject]@{name=$_.fullname;result=$_.result;label=$_.label;message=$_.failure.message.'#cdata-section';stack=$_.failure.'stack-trace'.'#cdata-section'}})
 $summary|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $run 'assertions.json') -Encoding UTF8
 $valid=$process.ExitCode -eq 0 -and $cases.Count -eq $ExpectedTests -and @($cases|Where-Object{$_.result -ne 'Passed'}).Count -eq 0
 if($Phase -eq 'red'){$valid=$process.ExitCode -eq 2 -and $cases.Count -eq $ExpectedTests -and @($cases|Where-Object{$_.result -eq 'Failed' -and $_.label -ne 'Error'}).Count -eq $ExpectedTests}
 if(!$valid){throw 'Expected the selected VT103 count to pass with exit 0. Inspect XML; no GREEN claim otherwise.'}
 Write-Output ($Phase+': '+$cases.Count+' executed; inspect assertions.json and process.json for actual results.')
 Write-Output ('Unity exit code: '+$process.ExitCode)
 # Return success only after validating the requested outcome; raw Unity exit is in process.json.
 exit 0
}
finally {
 if($testData -and (Test-Path -LiteralPath $testData)){Copy-Item -LiteralPath $testData -Destination (Join-Path $run 'test-data') -Recurse}
 foreach($rel in $configBefore.Keys){
  $file=Join-Path $worktree $rel
  $bytes=[IO.File]::ReadAllBytes($file)
  if([Convert]::ToBase64String($bytes) -ne [Convert]::ToBase64String($configBefore[$rel])){
   $backup=Join-Path (Join-Path $run 'editor-generated-before-restoration') $rel
   New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($backup)) -Force|Out-Null
   [IO.File]::WriteAllBytes($backup,$bytes)
   [IO.File]::WriteAllBytes($file,$configBefore[$rel])
  }
 }
 foreach($name in $envNames){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
}