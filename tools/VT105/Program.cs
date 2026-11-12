using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using CampusEscape.SaveSystem;

// No NUnit replacement layer: executable assertions over real production C#.
internal static class Program
{
    private static readonly DateTime Now = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Type PolicyType = typeof(SaveSlotService).Assembly.GetType("CampusEscape.SaveSystem.SaveSlotPolicy");
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    // Baseline fallback is deliberate: RED executes old behavior, without adding API stubs.
    private static SaveSlotService Service(Repo repo, int hours = 24)
    {
        if (PolicyType == null) return new SaveSlotService(repo);
        try {
            var policy = Activator.CreateInstance(PolicyType, hours);
            return (SaveSlotService)Activator.CreateInstance(typeof(SaveSlotService), repo, policy, (Func<DateTime>)(() => Now));
        }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    private static Repo Seed(TimeSpan? age = null)
    {
        var repo = new Repo();
        repo.Storage[1] = new SaveSlot { Id = 1, UserId = "owner", SaveName = "Lab T3",
            GameDataJson = "{\"room\":3}", IsDeleted = true, DeletedAt = Now - (age ?? TimeSpan.FromHours(2)),
            UpdatedAt = Now.AddDays(-2) };
        return repo;
    }
    private static string Snapshot(SaveSlot slot) => JsonSerializer.Serialize(slot);
    private static async Task Denied<T>(Repo repo, SaveSlotService service, string user = "owner", int id = 1) where T : Exception
    {
        var before = Snapshot(repo.Storage[1]);
        await Throws<T>(() => service.RestoreAsync(id, user));
        Check(before == Snapshot(repo.Storage[1]) && repo.SaveCalls == 0, "Denied restore changed persisted state or called Save");
    }
    private static async Task Restored(Repo repo, SaveSlotService service)
    {
        Check(await service.RestoreAsync(1, "owner"), "Restore returned false");
        var slot = repo.Storage[1];
        Check(!slot.IsDeleted && slot.DeletedAt == null && slot.UpdatedAt == Now, "Restore state/timestamp wrong");
        Check(slot.Id == 1 && slot.UserId == "owner" && slot.SaveName == "Lab T3" && slot.GameDataJson == "{\"room\":3}", "Payload/identity changed");
        Check(repo.SaveCalls == 1, "Restore must save exactly once");
        Check((await repo.GetActiveByUserIdAsync("owner")).Single().Id == 1, "Restored slot not visible to owner");
        Check(!(await repo.GetActiveByUserIdAsync("intruder")).Any(), "Restored slot leaked to another user");
    }
    private static List<(string Id, Func<Task> Run)> Cases()
    {
        var tests = new List<(string, Func<Task>)>();
        tests.Add(("TC01-default-policy", () => {
            Check(PolicyType != null, "Missing central SaveSlotPolicy");
            var policy = Activator.CreateInstance(PolicyType);
            Check((int)PolicyType.GetProperty("RestoreRetentionHours").GetValue(policy) == 24, "Default must be 24 hours");
            return Task.CompletedTask;
        }));
        foreach (int h in new[] { 0, 1, 2, 719, 720, 721 }) {
            int hours = h;
            tests.Add(($"TC02-config-{hours}", async () => {
                if (hours < 1 || hours > 720) await Throws<ArgumentOutOfRangeException>(() => { Service(Seed(), hours); return Task.CompletedTask; });
                else { var r = Seed(TimeSpan.Zero); await Restored(r, Service(r, hours)); }
            }));
        }
        foreach (long ticks in new[] { -1L, 0L, 1L }) {
            long offset = ticks;
            tests.Add(($"TC03-expiry-{offset}", async () => {
                var r = Seed(TimeSpan.FromHours(24) + TimeSpan.FromTicks(offset));
                if (offset <= 0) await Restored(r, Service(r));
                else await Denied<InvalidOperationException>(r, Service(r));
            }));
        }
        tests.Add(("TC04-config-changes-outcome", async () => {
            var denied = Seed(TimeSpan.FromHours(25)); await Denied<InvalidOperationException>(denied, Service(denied, 24));
            var allowed = Seed(TimeSpan.FromHours(25)); await Restored(allowed, Service(allowed, 48));
        }));
        tests.Add(("TC05-cross-owner", async () => { var r = Seed(); await Denied<UnauthorizedAccessException>(r, Service(r), "intruder"); }));
        tests.Add(("TC06-blank-identity", async () => {
            foreach (string identity in new[] { null, "", " " }) {
                var r = Seed(); r.Storage[1].UserId = identity;
                await Denied<UnauthorizedAccessException>(r, Service(r), identity);
            }
        }));
        tests.Add(("TC07-null-deletion-time", async () => { var r = Seed(); r.Storage[1].DeletedAt = null; await Denied<InvalidOperationException>(r, Service(r)); }));
        tests.Add(("TC08-future-deletion-time", async () => { var r = Seed(TimeSpan.FromTicks(-1)); await Denied<InvalidOperationException>(r, Service(r)); }));
        tests.Add(("TC09-not-deleted", async () => { var r = Seed(); r.Storage[1].IsDeleted = false; await Denied<InvalidOperationException>(r, Service(r)); }));
        tests.Add(("TC10-not-found", async () => { var r = Seed(); await Denied<KeyNotFoundException>(r, Service(r), id: 999); }));
        tests.Add(("TC11-save-false", async () => {
            var r = Seed(); r.FailSave = true; var before = Snapshot(r.Storage[1]);
            await Throws<InvalidOperationException>(() => Service(r).RestoreAsync(1, "owner"));
            Check(before == Snapshot(r.Storage[1]) && r.SaveCalls == 1, "Failed persistence changed stored object");
        }));
        tests.Add(("TC12-save-throws", async () => {
            var r = Seed(); r.ThrowSave = true; var before = Snapshot(r.Storage[1]);
            await Throws<IOException>(() => Service(r).RestoreAsync(1, "owner"));
            Check(before == Snapshot(r.Storage[1]) && r.SaveCalls == 1, "Thrown persistence changed stored object");
        }));
        tests.Add(("TC13-delete-regression", async () => {
            var r = Seed(); r.Storage[1].IsDeleted = false; r.Storage[1].DeletedAt = null;
            var s = new SaveSlotService(r);
            Check(await s.SoftDeleteAsync(1, "owner"), "Delete failed");
            Check(r.Storage[1].IsDeleted && r.Storage[1].DeletedAt != null, "Delete outcome wrong");
            Check(await s.SoftDeleteAsync(1, "owner"), "Idempotent delete failed");
            Check(r.SaveCalls == 1, "Idempotent delete wrote again");
            await Throws<UnauthorizedAccessException>(() => s.SoftDeleteAsync(1, "intruder"));
        }));
        tests.Add(("TC14-config-file", async () => {
            var config = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText("tools/VT105/policy.json"));
            Check(config.Count == 1 && config.ContainsKey("RestoreRetentionHours"), "Unexpected policy configuration");
            var r = Seed(TimeSpan.Zero); await Restored(r, Service(r, config["RestoreRetentionHours"]));
        }));
        tests.Add(("TC15-owner-check-before-conflict", async () => {
            var r = Seed(); r.Storage[1].IsDeleted = false; await Denied<UnauthorizedAccessException>(r, Service(r), "intruder");
        }));
        return tests;
    }
    private static async Task<int> Load()
    {
        // All 150 async operations must reach a common gate before release.
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int ready = 0;
        var tasks = Enumerable.Range(0, 150).Select(async i => {
            var r = Seed(TimeSpan.FromHours(i % 3 == 2 ? 25 : 2));
            r.DelayMs = 10; var s = Service(r);
            Interlocked.Increment(ref ready);
            await gate.Task;
            var timer = Stopwatch.StartNew();
            if (i % 3 == 0) await Restored(r, s);
            else if (i % 3 == 1) await Denied<UnauthorizedAccessException>(r, s, "intruder");
            else await Denied<InvalidOperationException>(r, s);
            return timer.Elapsed.TotalMilliseconds;
        }).ToArray();
        Check(ready == 150, "Not all 150 tasks reached start barrier");
        gate.SetResult(true);
        var elapsed = await Task.WhenAll(tasks); Array.Sort(elapsed);
        double p95 = elapsed[(int)Math.Ceiling(.95 * elapsed.Length) - 1];
        Console.WriteLine(JsonSerializer.Serialize(new { concurrentTasks = 150, successfulRestores = 50, deniedOwnership = 50,
            deniedExpired = 50, fakeIoDelayMs = 10, p95Ms = p95, maxMs = elapsed.Last(), targetMs = 4000, passed = p95 <= 4000 }));
        return p95 <= 4000 ? 0 : 1;
    }
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--load")) return await Load();
        var tests = Cases();
        if (args.Contains("--reverse")) tests.Reverse();
        if (args.Contains("--auth-only")) tests = tests.Where(t => t.Id.StartsWith("TC05") || t.Id.StartsWith("TC06") || t.Id.StartsWith("TC15")).ToList();
        int failed = 0;
        foreach (var test in tests) {
            try { await test.Run(); Console.WriteLine("PASS " + test.Id); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL " + test.Id + ": " + ex.GetType().Name + ": " + ex.Message); }
        }
        Console.WriteLine($"RESULT total={tests.Count} passed={tests.Count - failed} failed={failed}");
        return failed == 0 ? 0 : 1;
    }
    private sealed class Repo : ISaveSlotRepository
    {
        public readonly Dictionary<int, SaveSlot> Storage = new Dictionary<int, SaveSlot>();
        public int SaveCalls; public bool FailSave; public bool ThrowSave; public int DelayMs;
        public async Task<SaveSlot> GetByIdAsync(int id) {
            if (DelayMs > 0) await Task.Delay(DelayMs);
            Storage.TryGetValue(id, out var slot); return slot;
        }
        public async Task<bool> SaveAsync(SaveSlot slot) {
            SaveCalls++; if (DelayMs > 0) await Task.Delay(DelayMs);
            if (ThrowSave) throw new IOException("Injected storage failure");
            if (FailSave) return false;
            Storage[slot.Id] = slot; return true;
        }
        public Task<IEnumerable<SaveSlot>> GetActiveByUserIdAsync(string userId) =>
            Task.FromResult<IEnumerable<SaveSlot>>(Storage.Values.Where(s => s.UserId == userId && !s.IsDeleted).ToArray());
    }
}
