using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HUSEscape.Persistence.Autosave;

internal static class Program
{
    const int Clients = 120, Rounds = 3, PayloadBytes = 32768;
    const double LimitMs = 3500;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static string output;

    sealed record Input(int Client, string Path, DraftRequest Request);
    sealed record Sample(int Client, long Revision, string RequestId, string Path, string Status,
        long StartTick, long EndTick, double StartOffsetMs, double EndOffsetMs, double LatencyMs,
        string ErrorType, string ErrorMessage);
    sealed record Head(string Path, long Revision, string RequestId, string PayloadHash,
        bool Valid, string Error, long Bytes);
    sealed record Observation(int Sequence, string Path, long CandidateRevision, string CandidateId, Head PriorHead);
    sealed record Stats(double MinMs, double MeanMs, double MedianMs, double P95Ms, double MaxMs, int N, int P95Rank);
    sealed record Correctness(bool Passed, bool AllFilesValid, bool MaximumSubmittedPerPath,
        bool FinalAtLeastAllCommitted, bool EveryCommittedWitnessed, bool ObservedHeadsMonotonic,
        bool OnlyExpectedStatuses, bool NoPendingFiles, int FinalFiles, int CommittedWitnesses, string[] Errors);
    sealed record RoundResult(string Scenario, string Phase, int Round, int Prepared, int ReadyAtRelease,
        int Started, int Completed, int PeakInFlight, double StartSpreadMs, double GateToCompletionMs,
        Dictionary<string,int> StatusCounts, Stats Latency, Stats CommitLatency, Correctness Correctness,
        bool P95ThresholdMet, string Acceptance, string SamplesCsv, string SamplesJson);

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json), new UTF8Encoding(false));

    static async Task<int> Main(string[] args)
    {
        output = Path.GetFullPath(args.Single());
        if (!string.Equals(Path.GetPathRoot(output), @"D:\", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("D: evidence only");
        Directory.CreateDirectory(output);
        try
        {
            ThreadPool.GetMinThreads(out int minWorkers,out int minIo);
            ThreadPool.GetMaxThreads(out int maxWorkers,out int maxIo);
            var drive = new DriveInfo(Path.GetPathRoot(output));
            WriteJson(Path.Combine(output,"runtime-environment.json"), new {
                runtime=RuntimeInformation.FrameworkDescription, os=RuntimeInformation.OSDescription,
                architecture=RuntimeInformation.ProcessArchitecture.ToString(), processors=Environment.ProcessorCount,
                serverGC=GCSettings.IsServerGC, stopwatchFrequency=Stopwatch.Frequency,
                threadPool=new { minWorkers,minIo,maxWorkers,maxIo,changedByHarness=false },
                drive=new { drive.Name,drive.DriveType,drive.DriveFormat,drive.TotalSize,drive.AvailableFreeSpace },
                configuration="Release, net8.0; same production C# sources, not Unity Mono",
                clients=Clients, rounds=Rounds, payloadBytes=PayloadBytes, limitMs=LimitMs,
                startGate="120 async clients await one RunContinuationsAsynchronously TaskCompletionSource before calling the store",
                timing="immediately before SaveAsync to observed result/exception; includes semaphore wait, worker scheduling, I/O and audit-hook cost",
                instrumentation="existing instance-scoped BeforeCommit reads the actual prior head; final heads read after all clients; no production changes",
                setupExcluded="payload construction, store construction and fixture directory preparation; gameplay capture is not part of this writer API",
                warmup="one separate 120-client run for each scenario, not included in measured statistics",
                utcStart=DateTimeOffset.UtcNow
            });
            var warm = new List<RoundResult>();
            foreach (string scenario in new[]{"P01","P02"}) warm.Add(await Run(scenario,"warmup",0));
            WriteJson(Path.Combine(output,"warmup-summary.json"),warm);
            var measured = new List<RoundResult>();
            for(int round=1;round<=Rounds;round++)
            {
                // Alternate order; never rerun a round to select a faster measurement.
                foreach(string scenario in round%2==1?new[]{"P01","P02"}:new[]{"P02","P01"})
                {
                    RoundResult result=await Run(scenario,"measured",round);
                    measured.Add(result);
                    WriteJson(Path.Combine(output,"summary.json"),measured);
                    Console.WriteLine($"{scenario} round {round}: {result.Started}/{result.Completed}, peak={result.PeakInFlight}, p95={result.Latency.P95Ms:F3} ms, {result.Acceptance}");
                }
            }
            bool accepted=measured.All(r=>r.Acceptance=="MET");
            WriteJson(Path.Combine(output,"run-result.json"),new { completed=true,accepted,
                measuredRounds=measured.Count,measuredRequests=measured.Sum(r=>r.Completed),
                P01=measured.Where(r=>r.Scenario=="P01").All(r=>r.Acceptance=="MET")?"MET":"NOT MET",
                P02=measured.Where(r=>r.Scenario=="P02").All(r=>r.Acceptance=="MET")?"MET":"NOT MET",
                scope="instrumented synthetic 32KiB draft-writer workload on this .NET/Windows/D: environment only; not Unity gameplay or all payload sizes",
                utcEnd=DateTimeOffset.UtcNow });
            return accepted?0:1;
        }
        catch(Exception e)
        {
            WriteJson(Path.Combine(output,"fatal.json"),new { type=e.GetType().FullName,message=e.Message,stack=e.StackTrace });
            Console.Error.WriteLine(e);
            return 2;
        }
    }

    static Head ReadHead(string path, Dictionary<string,Input> inputs)
    {
        if(!File.Exists(path)) return null;
        try
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            long bytes=stream.Length;
            var doc=(DraftDocument)new DataContractJsonSerializer(typeof(DraftDocument)).ReadObject(stream);
            string hash=doc.payload==null?null:Hash(doc.payload);
            bool valid=doc.formatVersion==1&&doc.revision>0&&hash!=null&&doc.payloadFingerprint==hash&&
                doc.requestId!=null&&inputs.TryGetValue(doc.requestId,out var input)&&input.Request.Revision==doc.revision&&
                string.Equals(doc.payload,input.Request.Payload,StringComparison.Ordinal);
            return new Head(path,doc.revision,doc.requestId,hash,valid,valid?null:"Metadata, fingerprint, identity or expected payload mismatch",bytes);
        }
        catch(Exception e){return new Head(path,-1,null,null,false,e.GetType().Name+": "+e.Message,0);}
    }

    static Stats Statistics(IEnumerable<double> source)
    {
        double[] a=source.OrderBy(x=>x).ToArray();if(a.Length==0)return null;
        int rank=(int)Math.Ceiling(.95*a.Length);
        double median=a.Length%2==1?a[a.Length/2]:(a[a.Length/2-1]+a[a.Length/2])/2;
        return new Stats(a[0],a.Average(),median,a[rank-1],a[^1],a.Length,rank);
    }

    static async Task<RoundResult> Run(string scenario,string phase,int round)
    {
        string folder=Path.Combine(output,phase,scenario,"round-"+round);
        if(Directory.Exists(folder))throw new IOException("Refusing to overwrite a benchmark round");
        Directory.CreateDirectory(folder);
        var inputs=new List<Input>();
        for(int i=1;i<=Clients;i++)
        {
            string path=Path.Combine(folder,"fixtures",scenario=="P01"?"shared":"slot-"+i.ToString("D3"),"floor01-draft-v1.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string id=$"{phase}-{scenario}-r{round}-request-{i:D3}";
            string prefix=$"{{\"scene\":\"Floor01_Prototype\",\"fixtureRevision\":{i},\"requestId\":\"{id}\",\"syntheticPadding\":\"";
            string payload=prefix+new string((char)('a'+i%26),PayloadBytes-prefix.Length-2)+"\"}";
            if(Encoding.UTF8.GetByteCount(payload)!=PayloadBytes)throw new Exception("Payload size mismatch");
            inputs.Add(new Input(i,path,new DraftRequest(i,id,payload)));
        }
        var byId=inputs.ToDictionary(x=>x.Request.RequestId,StringComparer.Ordinal);
        var observations=new ConcurrentQueue<Observation>();
        int sequence=0,ready=0,started=0,completed=0,inFlight=0,peak=0;
        var gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        long release=0;
        // Deterministic preparation shuffle. Actual dispatch order remains scheduler-driven and is recorded.
        var order=inputs.ToArray();var random=new Random(103+round);
        for(int i=order.Length-1;i>0;i--){int j=random.Next(i+1);(order[i],order[j])=(order[j],order[i]);}
        var samples=new ConcurrentQueue<Sample>();
        async Task Client(Input input)
        {
            string path=input.Path;
            // Each client owns an instance; the production registry coordinates shared-path instances.
            var writer=new DraftStore(path,new DraftIoHooks {
                BeforeCommit=request=>observations.Enqueue(new Observation(Interlocked.Increment(ref sequence),path,
                    request.Revision,request.RequestId,ReadHead(path,byId)))
            });
            Interlocked.Increment(ref ready);
            await gate.Task.ConfigureAwait(false);
            int active=Interlocked.Increment(ref inFlight);
            int old;
            do{old=Volatile.Read(ref peak);if(active<=old)break;}while(Interlocked.CompareExchange(ref peak,active,old)!=old);
            string status=null,errorType=null,errorMessage=null;
            long begin=Stopwatch.GetTimestamp();
            Interlocked.Increment(ref started);
            try{status=(await writer.SaveAsync(input.Request).ConfigureAwait(false)).ToString();}
            catch(Exception e){status=e is IOException or UnauthorizedAccessException?"IOError":"OtherError";errorType=e.GetType().FullName;errorMessage=e.Message;}
            long end=Stopwatch.GetTimestamp();
            Interlocked.Decrement(ref inFlight);Interlocked.Increment(ref completed);
            samples.Enqueue(new Sample(input.Client,input.Request.Revision,input.Request.RequestId,path,status,begin,end,
                Ms(begin-release),Ms(end-release),Ms(end-begin),errorType,errorMessage));
        }
        Task[] tasks=order.Select(Client).ToArray();
        int readyAtRelease=Volatile.Read(ref ready);
        if(readyAtRelease!=Clients)throw new Exception("Not all clients prepared at start gate");
        release=Stopwatch.GetTimestamp();gate.SetResult(true);
        try{await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(180));}
        catch {WriteJson(Path.Combine(folder,"partial-samples.json"),samples.ToArray());throw;}
        Sample[] rows=samples.OrderBy(x=>x.Client).ToArray();
        var audit=observations.OrderBy(x=>x.Sequence).ToArray();
        Head[] finals=inputs.Select(x=>x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Select(p=>ReadHead(p,byId)).ToArray();
        var errors=new List<string>();
        var counts=Enum.GetNames<DraftWriteStatus>().Concat(new[]{"IOError","OtherError"}).ToDictionary(s=>s,s=>rows.Count(r=>r.Status==s));
        bool allValid=finals.All(h=>h!=null&&h.Valid)&&audit.All(o=>o.PriorHead==null||o.PriorHead.Valid);
        var witnesses=audit.Select(o=>o.PriorHead).Concat(finals).Where(h=>h!=null).ToArray();
        bool witnessed=rows.Where(r=>r.Status=="Committed").All(r=>witnesses.Any(h=>h.Valid&&h.Path.Equals(r.Path,StringComparison.OrdinalIgnoreCase)&&h.Revision==r.Revision&&h.RequestId==r.RequestId));
        bool monotonic=true,maxPerPath=true,finalAtLeast=true;
        foreach(var path in inputs.Select(x=>x.Path).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            long prior=0;
            foreach(var h in audit.Where(o=>o.Path.Equals(path,StringComparison.OrdinalIgnoreCase)).Select(o=>o.PriorHead)
                .Concat(finals.Where(h=>h!=null&&h.Path.Equals(path,StringComparison.OrdinalIgnoreCase))).Where(h=>h!=null))
            {if(h.Revision<prior)monotonic=false;prior=h.Revision;}
            var final=finals.SingleOrDefault(h=>h!=null&&h.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
            long submitted=inputs.Where(i=>i.Path.Equals(path,StringComparison.OrdinalIgnoreCase)).Max(i=>i.Request.Revision);
            long acknowledged=rows.Where(r=>r.Path.Equals(path,StringComparison.OrdinalIgnoreCase)&&r.Status=="Committed").Select(r=>r.Revision).DefaultIfEmpty(0).Max();
            maxPerPath &= final!=null&&final.Revision==submitted;
            finalAtLeast &= final!=null&&final.Revision>=acknowledged;
        }
        bool expectedStatuses=rows.All(r=>r.Status=="Committed"||(scenario=="P01"&&r.Status=="RejectedStale"));
        bool noPending=!Directory.EnumerateFiles(Path.Combine(folder,"fixtures"),"*.pending",SearchOption.AllDirectories).Any();
        if(!allValid)errors.Add("An actual loaded head is invalid or has the wrong payload/fingerprint");
        if(!witnessed)errors.Add("A Committed acknowledgment has no matching on-disk read-back witness");
        if(!monotonic)errors.Add("Observed durable head revisions decreased");
        if(!maxPerPath||!finalAtLeast)errors.Add("Final revision is not the maximum submitted/committed per path");
        if(!expectedStatuses)errors.Add("Unexpected status/exception: inspect every raw sample; none excluded");
        if(!noPending)errors.Add("Staging artifacts remain");
        bool correct=errors.Count==0&&started==Clients&&completed==Clients&&rows.Length==Clients;
        var correctness=new Correctness(correct,allValid,maxPerPath,finalAtLeast,witnessed,monotonic,expectedStatuses,noPending,finals.Length,
            rows.Count(r=>r.Status=="Committed"&&witnesses.Any(h=>h.Valid&&h.RequestId==r.RequestId)),errors.ToArray());
        string csv=Path.Combine(folder,"samples.csv"),json=Path.Combine(folder,"samples.json");
        WriteJson(json,rows);
        var text=new StringBuilder("client,revision,requestId,path,status,startTick,endTick,startOffsetMs,endOffsetMs,latencyMs,errorType,errorMessage\n");
        static string Quote(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
        foreach(var r in rows)text.AppendLine(string.Join(",",r.Client,r.Revision,Quote(r.RequestId),Quote(r.Path),Quote(r.Status),r.StartTick,r.EndTick,
            r.StartOffsetMs.ToString("R",CultureInfo.InvariantCulture),r.EndOffsetMs.ToString("R",CultureInfo.InvariantCulture),r.LatencyMs.ToString("R",CultureInfo.InvariantCulture),Quote(r.ErrorType),Quote(r.ErrorMessage)));
        File.WriteAllText(csv,text.ToString(),new UTF8Encoding(false));
        WriteJson(Path.Combine(folder,"audit.json"),new{preCommitObservations=audit,finalHeads=finals});
        WriteJson(Path.Combine(folder,"correctness.json"),correctness);
        Stats stats=Statistics(rows.Select(r=>r.LatencyMs));bool threshold=stats!=null&&stats.P95Ms<=LimitMs;
        var result=new RoundResult(scenario,phase,round,inputs.Count,readyAtRelease,started,completed,peak,
            rows.Max(r=>r.StartOffsetMs)-rows.Min(r=>r.StartOffsetMs),rows.Max(r=>r.EndOffsetMs),counts,stats,
            Statistics(rows.Where(r=>r.Status=="Committed").Select(r=>r.LatencyMs)),correctness,threshold,
            correct&&threshold?"MET":"NOT MET",Path.GetRelativePath(output,csv),Path.GetRelativePath(output,json));
        WriteJson(Path.Combine(folder,"summary.json"),result);
        return result;
    }
}