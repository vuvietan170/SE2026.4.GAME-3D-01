param(
 [Parameter(Mandatory=$true)][string]$EvidenceRoot,
 [Parameter(Mandatory=$true)][string]$CaseName,
 [ValidateSet('None','M1','M2')][string]$Mutation='None',
 [string]$Filter='HUSEscape.Persistence.Autosave.Tests.DraftStoreRedTests',
 [int]$ExpectedTests=4,
 [string]$UnityPath='C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe'
)
$ErrorActionPreference='Stop'
$worktree=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project=Join-Path $worktree 'HUS_Escape'
if([IO.Path]::GetPathRoot($project) -ne 'D:\' -or [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($EvidenceRoot)) -ne 'D:\'){throw 'D: only'}
if((git -C $worktree branch --show-current) -ne 'individual/23001853-vt103-autosave' -or (git -C $worktree rev-parse HEAD) -ne 'f933595ef7d1aa810032ddd54cafaad4a438aea4'){throw 'Wrong worktree/branch/baseline'}
if(Test-Path -LiteralPath (Join-Path $project 'Temp/UnityLockfile')){throw 'Editor already open or stale lock needs review'}
$run=[IO.Path]::GetFullPath((Join-Path $EvidenceRoot $CaseName))
if(!$run.StartsWith([IO.Path]::GetFullPath($EvidenceRoot).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Case must stay inside evidence root'}
if(Test-Path -LiteralPath $run){throw 'Never overwrite experiment evidence; use a new case/root'}
New-Item -ItemType Directory -Path $run -Force|Out-Null
$source=Join-Path $project 'Assets/_Project/Scripts/Utilities/Autosave/DraftStore.cs'
$before=[IO.File]::ReadAllBytes($source);$beforeHash=(Get-FileHash -LiteralPath $source).Hash
[IO.File]::WriteAllBytes((Join-Path $run 'DraftStore.guarded-before.txt'),$before)
$configBefore=@{}
foreach($rel in @('HUS_Escape/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset','HUS_Escape/ProjectSettings/EditorBuildSettings.asset','HUS_Escape/ProjectSettings/ProjectAuditorSettings.asset','HUS_Escape/ProjectSettings/ShaderGraphSettings.asset')){
 $file=Join-Path $worktree $rel;if(Test-Path -LiteralPath $file){$configBefore[$rel]=[IO.File]::ReadAllBytes($file)}
}
$envNames=@('TEMP','TMP','UPM_CACHE_ROOT','VT103_TEST_DATA_ROOT');$previous=@{}
foreach($name in $envNames){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try {
 $text=[Text.Encoding]::UTF8.GetString($before)
 if($Mutation -eq 'M1'){
  # Delete identity-history rejection and same-head identity handling. Keep stale and
  # equal-revision/different-ID rejection; only same-ID retries can now fall into commit.
  $history='(?m)^            if \(state.CommittedIds.TryGetValue[^\r\n]*\r?\n[^\r\n]*\r?\n                return DraftWriteStatus.Conflict;\r?\n'
  $duplicate='(?ms)^                if \(string.Equals\(request.RequestId, head.requestId, StringComparison.Ordinal\)\)\r?\n                \{.*?^                \}\r?\n'
  if([regex]::Matches($text,$history).Count -ne 1 -or [regex]::Matches($text,$duplicate).Count -ne 1){throw 'Idempotency shape changed; inspect before mutation'}
  $text=[regex]::Replace($text,$history,'');$text=[regex]::Replace($text,$duplicate,'')
  $equal='if (request.Revision == head.revision) return DraftWriteStatus.Conflict;'
  if(!$text.Contains($equal)){throw 'Equal revision branch missing'}
  $text=$text.Replace($equal,'if (request.Revision == head.revision && !string.Equals(request.RequestId, head.requestId, StringComparison.Ordinal)) return DraftWriteStatus.Conflict;')
 }
 elseif($Mutation -eq 'M2'){
  if(!$text.Contains('state.Gate.WaitAsync()') -or !$text.Contains('state.Gate.Release();')){throw 'Semaphore shape changed; inspect before mutation'}
  $text=$text.Replace('state.Gate.WaitAsync()','Task.CompletedTask').Replace('state.Gate.Release();','/* M2: semaphore release removed */')
 }
 if($Mutation -ne 'None'){[IO.File]::WriteAllText($source,$text,(New-Object Text.UTF8Encoding($false)))}
 Copy-Item -LiteralPath $source -Destination (Join-Path $run 'DraftStore.executed.txt')
 git -C $worktree diff --no-index -- (Join-Path $run 'DraftStore.guarded-before.txt') (Join-Path $run 'DraftStore.executed.txt') | Set-Content -LiteralPath (Join-Path $run 'mutation.diff') -Encoding UTF8
 $sourceHashes=@{}
 Get-ChildItem -LiteralPath (Join-Path $project 'Assets/_Project/Scripts/Utilities/Autosave') -File -Recurse|Where-Object{$_.Extension -in @('.cs','.asmdef')}|ForEach-Object{$sourceHashes[$_.FullName.Substring($worktree.Length+1)]=(Get-FileHash -LiteralPath $_.FullName).Hash}
 $sourceHashes|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'executed-source-hashes.json') -Encoding UTF8
 $cache=Join-Path $project 'Library/VT103RunnerCache';New-Item -ItemType Directory -Path (Join-Path $cache 'Temp'),(Join-Path $cache 'GI'),(Join-Path $cache 'UPM') -Force|Out-Null
 $env:TEMP=Join-Path $cache 'Temp';$env:TMP=$env:TEMP;$env:UPM_CACHE_ROOT=Join-Path $cache 'UPM';$env:VT103_TEST_DATA_ROOT=Join-Path $run 'test-data'
 $argsUnity=@('-batchmode','-nographics','-projectPath',('"'+$project+'"'),'-giCustomCacheLocation',('"'+(Join-Path $cache 'GI')+'"'),'-runTests','-testPlatform','EditMode','-testFilter',$Filter,'-testResults',('"'+(Join-Path $run 'results.xml')+'"'),'-logFile',('"'+(Join-Path $run 'editor.log')+'"'))
 ('& "'+$UnityPath+'" '+($argsUnity -join ' '))|Set-Content -LiteralPath (Join-Path $run 'command.txt') -Encoding UTF8
 @{mutation=$Mutation;filter=$Filter;expectedTests=$ExpectedTests;baseline=(git -C $worktree rev-parse HEAD);branch=(git -C $worktree branch --show-current);testData=$env:VT103_TEST_DATA_ROOT;temp=$env:TEMP;upm=$env:UPM_CACHE_ROOT}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'environment.json') -Encoding UTF8
 Write-Output ('Experiment: '+$run)
 $process=Start-Process -FilePath $UnityPath -ArgumentList $argsUnity -WindowStyle Hidden -PassThru
 $watch=[Diagnostics.Stopwatch]::StartNew();$timeout=$false
 while(!$process.WaitForExit(1000)){if($watch.Elapsed.TotalSeconds -gt 600){$timeout=$true;$process.Kill();$process.WaitForExit();break}}
 $process.Refresh();@{exitCode=$process.ExitCode;pid=$process.Id;timeout=$timeout;seconds=$watch.Elapsed.TotalSeconds}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'process.json') -Encoding UTF8
 if($timeout -or !(Test-Path -LiteralPath (Join-Path $run 'results.xml'))){throw 'BLOCKED: no test evidence or timeout, not mutation detected'}
 [xml]$xml=Get-Content -LiteralPath (Join-Path $run 'results.xml') -Raw
 $cases=@($xml.SelectNodes('//test-case'));$summary=@($cases|ForEach-Object{[pscustomobject]@{name=$_.fullname;result=$_.result;label=$_.label;message=$_.failure.message.'#cdata-section';stack=$_.failure.'stack-trace'.'#cdata-section'}})
 $summary|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $run 'assertions.json') -Encoding UTF8
 $bad=@($summary|Where-Object{$_.label -in @('Error','Invalid') -or $_.result -notin @('Passed','Failed') -or $_.message -match 'timeout|barrier was not|Harness must complete'})
 $assertions=@($summary|Where-Object{$_.result -eq 'Failed' -and $_.label -notin @('Error','Invalid')})
 $passed=@($summary|Where-Object{$_.result -eq 'Passed'}).Count
 $outcome=if($cases.Count -ne $ExpectedTests -or $bad.Count -gt 0){'INCONCLUSIVE'}elseif($Mutation -ne 'None' -and $assertions.Count -gt 0){'KILLED'}elseif($Mutation -ne 'None' -and $passed -eq $cases.Count -and $process.ExitCode -eq 0){'SURVIVED'}elseif($Mutation -eq 'None' -and $passed -eq $cases.Count -and $process.ExitCode -eq 0){'PASS'}else{'FAIL'}
 @{mutation=$Mutation;outcome=$outcome;executed=$cases.Count;passed=$passed;assertionFailures=$assertions.Count;errorOrHarnessFailures=$bad.Count;unityExitCode=$process.ExitCode}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'outcome.json') -Encoding UTF8
 Write-Output ($outcome+': '+$passed+'/'+$cases.Count+' passed; assertion failures '+$assertions.Count+'; Unity exit '+$process.ExitCode)
 if($outcome -in @('INCONCLUSIVE','FAIL')){throw 'Review evidence; this is not a valid killed/survived/PASS claim'}
}
finally {
 [IO.File]::WriteAllBytes($source,$before)
 $restored=(Get-FileHash -LiteralPath $source).Hash
 @{beforeSHA256=$beforeHash;restoredSHA256=$restored;identical=($beforeHash -eq $restored)}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'restoration.json') -Encoding UTF8
 foreach($rel in $configBefore.Keys){$file=Join-Path $worktree $rel;$bytes=[IO.File]::ReadAllBytes($file);if([Convert]::ToBase64String($bytes) -ne [Convert]::ToBase64String($configBefore[$rel])){$backup=Join-Path (Join-Path $run 'editor-generated-before-restoration') $rel;New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($backup)) -Force|Out-Null;[IO.File]::WriteAllBytes($backup,$bytes);[IO.File]::WriteAllBytes($file,$configBefore[$rel])}}
 foreach($name in $envNames){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
 if($restored -ne $beforeHash){throw 'CRITICAL: source restoration hash mismatch'}
}