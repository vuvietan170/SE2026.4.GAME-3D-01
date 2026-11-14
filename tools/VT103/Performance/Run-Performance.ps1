param([string]$DotNetPath='C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/DotNetSdk/dotnet.exe')
$ErrorActionPreference='Stop'
$worktree=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if((git -C $worktree branch --show-current) -ne 'individual/23001853-vt103-autosave' -or (git -C $worktree rev-parse HEAD) -ne 'f933595ef7d1aa810032ddd54cafaad4a438aea4'){throw 'Unexpected branch/baseline'}
$run=Join-Path $worktree ('Evidence/VT103/performance-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if(Test-Path -LiteralPath $run){throw 'Never overwrite measurement evidence'}
$cache=Join-Path $worktree 'HUS_Escape/Library/VT103PerformanceCache'
if([IO.Path]::GetPathRoot($cache) -ne 'D:\'){throw 'D: output/cache only'}
New-Item -ItemType Directory -Path $run -Force|Out-Null
$names=@('TEMP','TMP','DOTNET_CLI_HOME','NUGET_PACKAGES','NUGET_HTTP_CACHE_PATH','NUGET_SCRATCH','DOTNET_BUNDLE_EXTRACT_BASE_DIR','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_SKIP_FIRST_TIME_EXPERIENCE','DOTNET_GENERATE_ASPNET_CERTIFICATE','MSBUILDDISABLENODEREUSE')
$previous=@{};foreach($name in $names){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
function Invoke-Recorded([string]$Label,[string[]]$Arguments,[int]$TimeoutSeconds){
 $out=Join-Path $run ($Label+'.stdout.log');$err=Join-Path $run ($Label+'.stderr.log')
 ('& "'+$DotNetPath+'" '+($Arguments -join ' '))|Set-Content -LiteralPath (Join-Path $run ($Label+'.command.txt')) -Encoding UTF8
 $p=New-Object Diagnostics.Process
 $p.StartInfo=New-Object Diagnostics.ProcessStartInfo
 $p.StartInfo.FileName=$DotNetPath;$p.StartInfo.Arguments=($Arguments -join ' ')
 $p.StartInfo.UseShellExecute=$false;$p.StartInfo.CreateNoWindow=$true
 $p.StartInfo.RedirectStandardOutput=$true;$p.StartInfo.RedirectStandardError=$true
 $null=$p.Start();$readOut=$p.StandardOutput.ReadToEndAsync();$readErr=$p.StandardError.ReadToEndAsync()
 $watch=[Diagnostics.Stopwatch]::StartNew();$timeout=$false
 while(!$p.WaitForExit(1000)){if($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds){$timeout=$true;$p.Kill();$p.WaitForExit();break}}
 [IO.File]::WriteAllText($out,$readOut.GetAwaiter().GetResult(),(New-Object Text.UTF8Encoding($false)))
 [IO.File]::WriteAllText($err,$readErr.GetAwaiter().GetResult(),(New-Object Text.UTF8Encoding($false)))
 @{exitCode=$p.ExitCode;timeout=$timeout;pid=$p.Id;seconds=$watch.Elapsed.TotalSeconds}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run ($Label+'.process.json')) -Encoding UTF8
 if($timeout){throw ('Process timeout: '+$Label+'; no performance acceptance claim')}
 return $p.ExitCode
}
try {
 foreach($entry in @{TEMP='temp';TMP='temp';DOTNET_CLI_HOME='dotnet-home';NUGET_PACKAGES='nuget';NUGET_HTTP_CACHE_PATH='nuget-http';NUGET_SCRATCH='nuget-scratch';DOTNET_BUNDLE_EXTRACT_BASE_DIR='bundle'}.GetEnumerator()){
  $value=Join-Path $cache $entry.Value;New-Item -ItemType Directory -Path $value -Force|Out-Null;[Environment]::SetEnvironmentVariable($entry.Key,$value,'Process')
 }
 $env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false';$env:MSBUILDDISABLENODEREUSE='1'
 $sdkInfo=& $DotNetPath --info;$sdkInfo|Set-Content -LiteralPath (Join-Path $run 'dotnet-info.txt') -Encoding UTF8
 $hardware=@{}
 try{$hardware.cpu=Get-CimInstance Win32_Processor|Select-Object Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed}catch{$hardware.cpu='NOT AVAILABLE'}
 try{$hardware.disk=Get-PhysicalDisk|Select-Object FriendlyName,MediaType,BusType}catch{$hardware.disk='NOT AVAILABLE'}
 $hardware.otherUnityProcesses=@(Get-Process Unity -ErrorAction SilentlyContinue|Select-Object Id,ProcessName)
 $hardware|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $run 'hardware.json') -Encoding UTF8
 $envs=@{};foreach($name in $names){$envs[$name]=[Environment]::GetEnvironmentVariable($name,'Process')};$envs|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'cache-environment.json') -Encoding UTF8
 $reference=Get-Content (Join-Path $worktree 'Evidence/VT103/mutation-20261113-175040/guarded-with-seam/hashes.json') -Raw|ConvertFrom-Json
 foreach($property in $reference.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $worktree $property.Name)).Hash -ne $property.Value){throw 'Production guards differ from verified restored reference'}}
 $sources=@('HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/DraftStore.cs','HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/DraftContract.cs','HUS_Escape/Assets/_Project/Scripts/Utilities/Autosave/DraftIoHooks.cs','tools/VT103/Performance/Program.cs','tools/VT103/Performance/VT103.Performance.proj','tools/VT103/Performance/NuGet.Config','tools/VT103/Performance/Run-Performance.ps1')
 $hashes=@{};foreach($rel in $sources){$source=Join-Path $worktree $rel;$target=Join-Path (Join-Path $run 'source') $rel;New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force|Out-Null;Copy-Item -LiteralPath $source -Destination $target;$hashes[$rel]=(Get-FileHash -LiteralPath $source).Hash};$hashes|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'source-hashes.json') -Encoding UTF8
 $project=Join-Path $PSScriptRoot 'VT103.Performance.proj';$obj=(Join-Path $cache 'obj/').Replace('\','/');$bin=Join-Path $cache 'bin'
 $common=@('-p:BaseIntermediateOutputPath="'+$obj+'"','-p:MSBuildProjectExtensionsPath="'+$obj+'"','-p:UseSharedCompilation=false','-nodeReuse:false')
 Write-Output ('Evidence: '+$run)
 $restore=Invoke-Recorded 'restore' (@('restore',('"'+$project+'"'),'--configfile',('"'+(Join-Path $PSScriptRoot 'NuGet.Config')+'"'))+$common) 120
 if($restore -ne 0){throw 'Offline SDK restore failed; inspect restore logs'}
 $build=Invoke-Recorded 'build' (@('build',('"'+$project+'"'),'--no-restore','-c','Release','-o',('"'+$bin+'"'))+$common) 120
 if($build -ne 0){throw 'Harness build failed; do not interpret as benchmark result'}
 $exit=Invoke-Recorded 'benchmark' @(('"'+(Join-Path $bin 'VT103.Performance.dll')+'"'),('"'+$run+'"')) 300
 Get-Content -LiteralPath (Join-Path $run 'benchmark.stdout.log')
 if(!(Test-Path -LiteralPath (Join-Path $run 'run-result.json'))){throw 'Missing benchmark completion report'}
 if($exit -ne 0){Write-Output ('Benchmark exit '+$exit+': retain all data and report NOT MET/incomplete; no automatic optimization or retry')}
}
finally {foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}}