#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HUSEscape.Core;
using HUSEscape.Persistence;
using HUSEscape.Player;
using HUSEscape.Quests;
using HUSEscape.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public sealed class TransactionProbe : MonoBehaviour
{
    public bool Finished { get; private set; }
    public bool Passed { get; private set; }
    const string Floor = "Assets/_Project/Scenes/Floor01_Prototype.unity";
    const string Campus = "Assets/_Project/Scenes/Campus_Prototype.unity";
    const string Office = "Assets/_Project/Scenes/TrainingOffice_Prototype.unity";
    readonly List<string> results = new List<string>();
    readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
    readonly List<Func<IEnumerator>> tests = new List<Func<IEnumerator>>();
    JourneySceneEntry entry;
    JourneySession session;
    GameBootstrap core;
    object backend;
    int next, failures;
    string activeTest, storage;
    float deadline;
    bool expectedLoadFailure;
    readonly List<string> runtimeErrors = new List<string>(), editorErrors = new List<string>();
    void Log(string message, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (expectedLoadFailure && type == LogType.Error && message.Contains("without activating")) return;
        if (type == LogType.Exception && trace.Contains("UnityEditor.Search.SearchDatabase") && trace.Contains("IndexationOnStartup")) editorErrors.Add(message + "\n" + trace);
        else runtimeErrors.Add(activeTest + ": " + message + "\n" + trace);
    }
    void Awake()
    {
        DontDestroyOnLoad(gameObject); Application.logMessageReceived += Log; Time.captureDeltaTime = 1f / 60f;
        storage = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TransactionSaves");
        Directory.CreateDirectory(storage);
        tests.Add(ContinueRejectedKeepsCommittedState);
        tests.Add(NewGameRejectedKeepsMemories);
        tests.Add(AcceptedFailureKeepsCommittedState);
        tests.Add(SameSceneRestoreFailureKeepsSession);
        tests.Add(ObserverExceptionDoesNotUndoCommittedRestore);
        tests.Add(MissingVersionIsRejected);
        tests.Add(MissingBooleanIsRejected);
        tests.Add(UnloadedSourceExposesRecovery);
        tests.Add(AcceptedNewGameFailureKeepsState);
        tests.Add(ModalFailurePreservesCursorAndPause);
        tests.Add(PendingRequestsCannotReplaceProposal);
        tests.Add(RepeatedContinueIsSilentAndUnique);
        tests.Add(LegacyCampusV1StillLoads);
        tests.Add(MissingDestinationKeepsSurvivingSource);
        tests.Add(BadDestinationBindingKeepsSource);
        tests.Add(LostSceneCanActuallyRecover);
        tests.Add(DestructiveAttachFailureCanRecover);
        tests.Add(FloorUnsafePoseFallsBack);
        tests.Add(FloorFailedRestoreRollsBackMetadataAndPose);
        tests.Add(FloorDoubleFailureRequiresRecovery);
        tests.Add(FloorRepeatedContinueDoesNotReplayRewards);
        tests.Add(ObserverReentryCannotStartSecondRestore);
        tests.Add(MalformedInputsAndIoDoNotChangeState);
        tests.Add(WriteFailureKeepsExistingFloorSlot);
        tests.Add(WriteFailureKeepsExistingCampusSlot);
        tests.Add(SessionObserverCannotTravelDuringLocalCommit);
        tests.Add(BadUiBindingDoesNotCommitDestination);
        tests.Add(NativeSingleTravelAndRecovery);
        tests.Add(UnsafeDestinationKeepsSourceInput);
        tests.Add(NativeNewGameCommitsOnlyAfterFreshScene);
        tests.Add(InitialEntryWaitsForCoreCompletion);
        tests.Add(ClosingModalDuringFailedLoadRestoresControl);
        tests.Add(NativeFloorContinueNotifiesOnce);
        tests.Add(OfficeFactsRequireCompletedCampus);
        var args = Environment.GetCommandLineArgs(); int filter = Array.IndexOf(args, "-transactionTest");
        if (filter >= 0) tests.RemoveAll(test => test.Method.Name != args[filter + 1]);

    }
    void Update()
    {
        if (Finished) return;
        if (work.Count == 0)
        {
            if (next == tests.Count) { Finish(); return; }
            var test = tests[next++]; activeTest = test.Method.Name; deadline = Time.realtimeSinceStartup + 90;
            work.Push(Test(test));
        }
        try
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(activeTest);
            while (work.Count != 0)
            {
                var task = work.Peek();
                if (!task.MoveNext()) { work.Pop(); continue; }
                if (task.Current is IEnumerator nested) { work.Push(nested); continue; }
                return;
            }
            results.Add("PASS " + activeTest);
        }
        catch (Exception exception)
        {
            failures++; results.Add("FAIL " + activeTest + ": " + exception);
            work.Clear();
        }
    }
    IEnumerator Test(Func<IEnumerator> test) { yield return Setup(); yield return test(); }
    IEnumerator Setup()
    {
        foreach (var old in FindObjectsByType<JourneySession>()) Destroy(old.gameObject);
        foreach (var old in FindObjectsByType<GameBootstrap>()) Destroy(old.gameObject);
        yield return Frames(2);
        var host = SceneManager.GetSceneByName("TransactionFixture");
        if (!host.IsValid()) host = SceneManager.CreateScene("TransactionFixture");
        SceneManager.SetActiveScene(host);
        foreach (string path in new[] { Floor, Campus, Office })
        { var old = SceneManager.GetSceneByPath(path); if (old.IsValid() && old.isLoaded) yield return Await(SceneManager.UnloadSceneAsync(old)); }
        foreach (var old in FindObjectsByType<BoxCollider>()) if (old.name == "TransactionBlocker") Destroy(old.gameObject);
        yield return Frames(2);
        Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        foreach (var directory in Directory.GetDirectories(storage)) Directory.Delete(directory, true);
        foreach (var file in Directory.GetFiles(storage)) File.Delete(file);
        yield return Await(SceneManager.LoadSceneAsync(Floor, LoadSceneMode.Additive));
        yield return Frames(20);
        entry = FindObjectsByType<JourneySceneEntry>().Single(); session = entry.Session;
        core = FindObjectsByType<GameBootstrap>().Single(b => b.IsInitialized);
        Check(session != null, "Floor fixture bound");
        session.StorageDirectoryOverride = storage; entry.floorSave.StorageDirectoryOverride = storage;
        backend = TransactionBackendControl.Install(core.Loader);
    }
    Floor01SaveData CompleteFloor()
    {
        return new Floor01SaveData {
            version = 1, scene = "Floor01_Prototype", savedUtc = DateTime.UtcNow.ToString("O"),
            questIds = entry.quests.Definitions.Select(q => q.Id).ToArray(), facts = entry.quests.Definitions.Select(q => q.CompletionEvent).ToArray(),
            inventory = new[] { "Key_A101", "Key_A102", "Key_Lab" },
            doors = new[] { "Key_A101", "Key_A102", "Key_Lab" }.Select(k => new SavedDoor { keyId = k, open = true }).ToArray(),
            cabinetUnlocked = true, sequenceSolved = true,
            lab = new SavedLab { power = true, instructions = true, connected = 3, data = true, complete = true },
            playerPosition = new Vector3(0, 1.08f, 8), playerYaw = 37
        };
    }
    IEnumerator AtCampus()
    {
        var data = CompleteFloor(); entry.floorSave.RestoreForTravel(data); yield return Frames(20);
        // The source survives in this controlled additive fixture. Move its controller
        // away from the destination spawn so it cannot obstruct Campus restore.
        entry.player.enabled = false; entry.player.transform.position = new Vector3(0, 1.08f, 68); entry.player.enabled = true;
        Physics.SyncTransforms(); yield return Frames(20);
        Check(session.TryLeaveFloor(), "Travel fixture accepted");
        yield return Await(SceneManager.LoadSceneAsync(Campus, LoadSceneMode.Additive));
        TransactionBackendControl.Complete(backend, true); yield return Frames(20);
        entry = FindObjectsByType<JourneySceneEntry>().Single(e => e.isCampus);
        Check(Get(session, "current") == (object)entry, "Campus fixture failed: " + session.LastMessage);
        foreach (var fact in new[] { "campus:courtyard", "campus:map", "campus:direction" }) entry.quests.Signal(fact);
        Set(session, "campusMemory", entry.quests.CaptureCampusFacts());
        Set(session, "officeMemory", new[] { "office:notice" });
    }
    string Committed() => JsonUtility.ToJson(session.FloorSnapshot) + "|" + string.Join(",", (string[])Get(session, "campusMemory")) + "|" + string.Join(",", (string[])Get(session, "officeMemory"));
    void OfficeSave()
    {
        var data = new JourneyCheckpoint { scenePath = Office, floor = session.FloorSnapshot,
            campusFacts = new[] { "campus:courtyard", "campus:map", "campus:direction" }, officeFacts = Array.Empty<string>(), position = new Vector3(0, 1.08f, 4), yaw = 72 };
        File.WriteAllText(session.OfficeSavePath, JsonUtility.ToJson(data));
    }
    IEnumerator ContinueRejectedKeepsCommittedState()
    {
        yield return AtCampus(); OfficeSave(); string before = Committed();
        TransactionBackendControl.Reject(backend, true);
        Check(!session.TryContinue(JourneyArea.TrainingOffice), "Rejected request returns false");
        Check(Committed() == before, "Rejected Continue mutated committed floor/memories");
        yield return Frames(8); Controls();
    }
    IEnumerator NewGameRejectedKeepsMemories()
    {
        yield return AtCampus(); string before = Committed(); TransactionBackendControl.Reject(backend, true);
        Check(!session.TryNewGame(true), "New Game rejected");
        Check(Committed() == before, "Rejected New Game erased committed memories");
        yield return Frames(8); Controls();
    }
    IEnumerator AcceptedFailureKeepsCommittedState()
    {
        yield return AtCampus(); OfficeSave(); string before = Committed();
        Check(session.TryContinue(JourneyArea.TrainingOffice), "Request accepted");
        FailLoad();
        Check(Committed() == before, "Load failure retained proposed memories");
        yield return Frames(8); Controls();
    }
    IEnumerator SameSceneRestoreFailureKeepsSession()
    {
        yield return AtCampus(); string before = Committed(); var pose = entry.player.transform.position;
        var data = new JourneyCheckpoint { scenePath = Campus, floor = session.FloorSnapshot, campusFacts = Array.Empty<string>(), officeFacts = Array.Empty<string>(), position = new Vector3(0,1.08f,8) };
        File.WriteAllText(session.CampusSavePath, JsonUtility.ToJson(data));
        var blockers = new List<GameObject>();
        foreach (var p in new[] { data.position, entry.spawn.position }) { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "TransactionBlocker"; go.transform.position = p; go.transform.localScale = Vector3.one * 3; blockers.Add(go); }
        Physics.SyncTransforms();
        Check(!session.TryContinue(JourneyArea.Campus), "Unsafe restore must fail");
        Check(Committed() == before && Vector3.Distance(pose, entry.player.transform.position) < .01f, "Failed Attach committed candidate");
        Check(Get(session, "current") == (object)entry, "Failed Attach lost current scene binding");
        foreach (var go in blockers) Destroy(go); yield return Frames(8); Controls();
    }
    IEnumerator ObserverExceptionDoesNotUndoCommittedRestore()
    {
        var data = CompleteFloor(); File.WriteAllText(entry.floorSave.SavePath, JsonUtility.ToJson(data));
        entry.floorSave.Restored += _ => throw new InvalidOperationException("Test observer throws after commit");
        Check(entry.floorSave.TryContinue(), "Committed restore reported failure due to observer");
        Check(entry.quests.IsComplete && entry.inventory.ItemIds.Count == 3, "Committed data missing");
        yield return Frames(8); Controls();
    }
    IEnumerator MissingVersionIsRejected()
    {
        yield return AtCampus(); OfficeSave(); string before = Committed();
        var json = File.ReadAllText(session.OfficeSavePath);
        int version = json.IndexOf("\"version\":1,", StringComparison.Ordinal);
        json = json.Remove(version, "\"version\":1,".Length);
        File.WriteAllText(session.OfficeSavePath, json);
        Check(!session.TryContinue(JourneyArea.TrainingOffice), "Missing Journey version accepted");
        Check(Committed() == before, "Malformed input changed state");
    }
    IEnumerator MissingBooleanIsRejected()
    {
        var data = CompleteFloor(); data.lab.complete = false; data.facts = data.facts.Where(f => f != "lab:complete").ToArray();
        string json = JsonUtility.ToJson(data).Replace("\"complete\":false,", "");
        // Keep syntactically valid JSON while removing a required false-valued field.
        json = json.Replace(",}", "}");
        File.WriteAllText(entry.floorSave.SavePath, json);
        Check(!entry.floorSave.TryContinue(), "Missing required LAB boolean accepted as false");
        yield return Frames(8); Controls();
    }
    IEnumerator UnloadedSourceExposesRecovery()
    {
        yield return AtCampus(); OfficeSave(); Check(session.TryContinue(JourneyArea.TrainingOffice), "Accepted before unload");
        yield return Await(SceneManager.UnloadSceneAsync(entry.gameObject.scene));
        FailLoad(); yield return Frames(8);
        var property = typeof(JourneySession).GetProperty("RecoveryRequired");
        Check(property != null && (bool)property.GetValue(session), "Missing explicit recoverable error after source unload");
        Check(!session.IsLoading, "Failure left request busy");
    }
    IEnumerator AcceptedNewGameFailureKeepsState()
    {
        yield return AtCampus(); string before = Committed();
        Check(session.TryNewGame(true), "New Game must start");
        Check(Committed() == before, "New Game changed committed state while loading");
        FailLoad();
        Check(Committed() == before, "Failed New Game changed committed state"); yield return Frames(8); Controls();
    }
    IEnumerator ModalFailurePreservesCursorAndPause()
    {
        yield return AtCampus(); OfficeSave();
        typeof(QuestPuzzleUI).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(entry.ui, new object[] { QuestModal.Progress });
        Check(entry.Gate.IsBlocked && Time.timeScale == 0, "Fixture progress modal paused");
        Check(session.TryContinue(JourneyArea.TrainingOffice), "Modal Continue accepted"); FailLoad();
        yield return Frames(8);
        Check(entry.ui.Mode == QuestModal.Progress && entry.Gate.IsBlocked && Time.timeScale == 0 && Cursor.visible, "Failure lost previous modal/cursor/pause");
        entry.ui.CloseModal(); yield return Frames(8); Controls();
    }
    IEnumerator PendingRequestsCannotReplaceProposal()
    {
        yield return AtCampus(); OfficeSave(); string before = Committed();
        Check(session.TryContinue(JourneyArea.TrainingOffice), "Office Continue accepted");
        Check(!session.TryContinue(JourneyArea.Campus) && !session.TryNewGame(true), "Repeated request replaced in-flight checkpoint");
        Check(Committed() == before, "Accepted load committed before restore");
        yield return Await(SceneManager.LoadSceneAsync(Office, LoadSceneMode.Additive));
        TransactionBackendControl.Complete(backend, true); yield return Frames(12);
        entry = FindObjectsByType<JourneySceneEntry>().Single(e => e.isTrainingOffice);
        Check(session.Area == JourneyArea.TrainingOffice && !session.RecoveryRequired, "Original proposal did not commit: " + session.LastMessage);
        Check(entry.quests.CampusIndex == 3 && entry.quests.OfficeIndex == 0, "Wrong proposal facts restored"); Controls();
    }
    IEnumerator RepeatedContinueIsSilentAndUnique()
    {
        yield return AtCampus();
        var data = new JourneyCheckpoint { scenePath = Campus, floor = session.FloorSnapshot, campusFacts = entry.quests.CaptureCampusFacts(), officeFacts = new[] { "office:notice" }, position = entry.player.transform.position };
        File.WriteAllText(session.CampusSavePath, JsonUtility.ToJson(data));
        int progress = 0, notices = entry.ui.NotificationCount;
        entry.quests.ProgressRecorded += (_, __, ___) => progress++;
        for (int i = 0; i < 4; i++) Check(session.TryContinue(JourneyArea.Campus), "Repeated same-scene Continue failed: " + session.LastMessage);
        Check(progress == 0 && entry.ui.NotificationCount == notices, "Continue replayed rewards or notices");
        Check(entry.inventory.ItemIds.Count == 3 && entry.inventory.ItemIds.Distinct().Count() == 3 && entry.quests.OfficeIndex == 1, "Continue duplicated items or lost facts");
        var exposed = session.FloorSnapshot; exposed.inventory = Array.Empty<string>();
        Check(session.FloorSnapshot.inventory.Length == 3, "Public snapshot mutated committed state");
        yield return Frames(8); Controls();
    }
    IEnumerator LegacyCampusV1StillLoads()
    {
        yield return AtCampus();
        var data = new JourneyCheckpoint { scenePath = Campus, floor = session.FloorSnapshot, campusFacts = entry.quests.CaptureCampusFacts(), officeFacts = Array.Empty<string>(), position = entry.player.transform.position };
        string legacy = JsonUtility.ToJson(data).Replace(",\"officeFacts\":[]", "");
        Check(!legacy.Contains("\"officeFacts\""), "Legacy fixture still contains Office facts");
        File.WriteAllText(session.CampusSavePath, legacy);
        Check(session.TryContinue(JourneyArea.Campus), "Pre-Office Campus V1 rejected: " + session.LastMessage);
        Check(entry.quests.OfficeIndex == 0 && entry.quests.CampusIndex == 3, "Legacy facts invalid"); yield return Frames(8); Controls();
    }
    IEnumerator MissingDestinationKeepsSurvivingSource()
    {
        yield return AtCampus(); OfficeSave(); string before = Committed();
        Check(session.TryContinue(JourneyArea.TrainingOffice), "Accepted before missing destination entry");
        TransactionBackendControl.Complete(backend, true); yield return Frames(8);
        Check(Get(session, "current") == (object)entry && Committed() == before, "Missing destination destroyed surviving session"); Controls();
    }
    IEnumerator BadDestinationBindingKeepsSource()
    {
        yield return AtCampus(); OfficeSave(); string before = Committed();
        Check(session.TryContinue(JourneyArea.TrainingOffice), "Accepted before bad binding");
        yield return Await(SceneManager.LoadSceneAsync(Office, LoadSceneMode.Additive));
        var bad = FindObjectsByType<JourneySceneEntry>().Single(e => e.isTrainingOffice); bad.spawn = null;
        TransactionBackendControl.Complete(backend, true); yield return Frames(8);
        Check(Get(session, "current") == (object)entry && Committed() == before, "Bad destination modified current/source data");
        Check(!bad.actorRoot.activeSelf, "Bad destination was not quarantined");
        Check(!bad.enabled, "Rejected scene entry can still run Start"); Controls();
    }
    IEnumerator LostSceneCanActuallyRecover()
    {
        yield return AtCampus(); OfficeSave(); string before = Committed();
        Check(session.TryContinue(JourneyArea.TrainingOffice), "Accepted before destructive failure");
        yield return Await(SceneManager.UnloadSceneAsync(entry.gameObject.scene)); FailLoad(); yield return Frames(8);
        Check(RecoveryFlag() && !session.IsLoading && Time.timeScale == 1 && Cursor.visible, "No usable recovery state");
        Check(Committed() == before, "Destructive failure committed proposed data");
        Check(InvokeRecover(), "Recovery load was rejected");
        yield return Await(SceneManager.LoadSceneAsync(Campus, LoadSceneMode.Additive));
        TransactionBackendControl.Complete(backend, true); yield return Frames(12);
        entry = FindObjectsByType<JourneySceneEntry>().Single(e => e.isCampus);
        Check(!RecoveryFlag() && session.Area == JourneyArea.Campus && entry.quests.CampusIndex == 3, "Committed scene did not recover: " + session.LastMessage);
        Check(FindObjectsByType<JourneySession>().Length == 1, "Recovery duplicated session"); Controls();
    }
    IEnumerator DestructiveAttachFailureCanRecover()
    {
        yield return AtCampus(); OfficeSave(); Check(session.TryContinue(JourneyArea.TrainingOffice), "Accepted before destructive Attach failure");
        yield return Await(SceneManager.LoadSceneAsync(Office, LoadSceneMode.Additive));
        var bad = FindObjectsByType<JourneySceneEntry>().Single(e => e.isTrainingOffice); bad.spawn = null;
        yield return Await(SceneManager.UnloadSceneAsync(entry.gameObject.scene));
        TransactionBackendControl.Complete(backend, true); yield return Frames(8);
        Check(RecoveryFlag() && Time.timeScale == 1 && Cursor.visible && !bad.actorRoot.activeSelf, "Destructive Attach left unusable actors/input");
        Check(InvokeRecover(), "Cannot recover after Attach error");
        yield return Await(SceneManager.LoadSceneAsync(Campus, LoadSceneMode.Additive)); TransactionBackendControl.Complete(backend, true); yield return Frames(12);
        entry = FindObjectsByType<JourneySceneEntry>().Single(e => e.isCampus); Check(!RecoveryFlag(), "Recovery still active"); Controls();
    }
    GameObject Block(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "TransactionBlocker";
        go.transform.position = position; go.transform.localScale = Vector3.one * 3; Physics.SyncTransforms(); return go;
    }
    IEnumerator FloorUnsafePoseFallsBack()
    {
        var data = CompleteFloor(); data.playerPosition = new Vector3(0,1.08f,20); var blocker = Block(data.playerPosition);
        File.WriteAllText(entry.floorSave.SavePath, JsonUtility.ToJson(data));
        Check(session.TryContinue(JourneyArea.Floor01), "Safe spawn fallback rejected: " + session.LastMessage);
        Check(entry.floorSave.LastLoadUsedFallback && Vector3.Distance(entry.player.transform.position, entry.spawn.position) < .3f, "Fallback pose incorrect");
        Destroy(blocker); yield return Frames(8); Controls();
    }
    IEnumerator FloorFailedRestoreRollsBackMetadataAndPose()
    {
        var blocker = Block(new Vector3(0,1.08f,20));
        var original = entry.floorSave.CaptureForRestore(); original.playerPosition = new Vector3(0,1.08f,20);
        entry.floorSave.RestoreForTravel(original); yield return Frames(8);
        Check(entry.floorSave.LastLoadUsedFallback, "Fixture fallback required");
        entry.player.enabled = false; entry.player.transform.position = new Vector3(0,1.08f,12); entry.player.enabled = true;
        Physics.SyncTransforms(); yield return Frames(12);
        Vector3 before = entry.player.transform.position; int count = entry.floorSave.RestoreCount;
        var data = CompleteFloor(); data.playerPosition = new Vector3(0,1.08f,20); Block(entry.spawn.position);
        File.WriteAllText(entry.floorSave.SavePath, JsonUtility.ToJson(data));
        Check(!session.TryContinue(JourneyArea.Floor01), "Unsafe requested/spawn poses accepted");
        Check(entry.floorSave.LastLoadUsedFallback && entry.floorSave.RestoreCount == count, "Failed restore changed metadata/counter");
        Check(Vector3.Distance(before, entry.player.transform.position) < .01f && entry.inventory.ItemIds.Count == 0 && entry.quests.CurrentIndex == 0, "Rollback changed pose/items/facts");
        foreach (var old in FindObjectsByType<BoxCollider>()) if (old.name == "TransactionBlocker") Destroy(old.gameObject);
        yield return Frames(8); Controls();
    }
    IEnumerator FloorDoubleFailureRequiresRecovery()
    {
        var data = CompleteFloor(); File.WriteAllText(entry.floorSave.SavePath, JsonUtility.ToJson(data));
        var lab = FindObjectsByType<LabRestoreSystem>().Single(); Set(lab, "screen", null);
        Check(!session.TryContinue(JourneyArea.Floor01), "Broken restore target accepted");
        Check(RecoveryFlag() && entry.floorSave.RecoveryRequired, "Failed rollback pretended session was restored");
        Check(Time.timeScale == 1 && Cursor.visible, "Recovery cursor/time inaccessible"); yield return Frames(4);
    }
    IEnumerator FloorRepeatedContinueDoesNotReplayRewards()
    {
        var data = CompleteFloor(); File.WriteAllText(entry.floorSave.SavePath, JsonUtility.ToJson(data));
        int calls = 0; entry.quests.ProgressRecorded += (_, __, ___) => calls++;
        for (int i=0;i<3;i++) { Check(session.TryContinue(JourneyArea.Floor01), "Floor Continue failed: " + session.LastMessage); yield return Frames(8); }
        Check(calls == 0 && entry.inventory.ItemIds.Distinct().Count() == 3 && entry.inventory.ItemIds.Count == 3, "Floor replayed pickup or reward");
        Check(FindObjectsByType<PickupItem>(FindObjectsInactive.Include).Where(p => p.gameObject.scene == entry.gameObject.scene).All(p => p.IsCollected), "Collected key respawned"); Controls();
    }
    IEnumerator ObserverReentryCannotStartSecondRestore()
    {
        var data = CompleteFloor(); File.WriteAllText(entry.floorSave.SavePath, JsonUtility.ToJson(data));
        bool? nested = null; entry.floorSave.Restored += _ => { if (nested == null) { nested = false; nested = entry.floorSave.TryContinue(); } };
        Check(session.TryContinue(JourneyArea.Floor01), "Outer restore failed");
        Check(nested == false && entry.floorSave.RestoreCount == 1, "Observer re-entered restoration"); yield return Frames(8); Controls();
    }
    IEnumerator MalformedInputsAndIoDoNotChangeState()
    {
        var data = CompleteFloor(); string json = JsonUtility.ToJson(data);
        foreach (var invalid in new[] { "{", "null", json + " trailing", json.Replace("\"version\":1", "\"version\":2"), json.Replace("\"version\":1", "\"version\":1,\"version\":1"), json.Replace("\"x\":0.0", "\"x\":1e999"), json.Replace("\"inventory\":[\"Key_A101\",\"Key_A102\",\"Key_Lab\"]", "\"inventory\":null") })
        {
            File.WriteAllText(entry.floorSave.SavePath, invalid);
            Check(!session.TryContinue(JourneyArea.Floor01), "Malformed save accepted: " + invalid.Substring(0, Math.Min(60,invalid.Length)));
            Check(entry.inventory.ItemIds.Count == 0 && entry.quests.CurrentIndex == 0, "Malformed data modified live state");
        }
        File.WriteAllBytes(entry.floorSave.SavePath, new byte[] { 0xff, 0xfe }); Check(!session.TryContinue(JourneyArea.Floor01), "Invalid UTF8 accepted");
        File.WriteAllText(entry.floorSave.SavePath, new string(' ',1024*1024+1)); Check(!session.TryContinue(JourneyArea.Floor01), "Oversized file accepted");
        Check(!session.TryContinue((JourneyArea)999), "Undefined JourneyArea accepted"); yield return Frames(8); Controls();
    }
    IEnumerator WriteFailureKeepsExistingFloorSlot()
    {
        string original = JsonUtility.ToJson(CompleteFloor()); File.WriteAllText(entry.floorSave.SavePath, original);
        Directory.CreateDirectory(entry.floorSave.SavePath + ".pending");
        Check(!session.TrySave(true), "Write to directory must fail");
        Check(session.LastMessage.Contains("Không thể lưu:"), "Floor test did not reach the IO error path: " + session.LastMessage);
        Check(File.ReadAllText(entry.floorSave.SavePath) == original && entry.inventory.ItemIds.Count == 0, "Write failure changed old slot/session");
        Directory.Delete(entry.floorSave.SavePath + ".pending"); yield return Frames(8); Controls();
    }
    IEnumerator WriteFailureKeepsExistingCampusSlot()
    {
        yield return AtCampus(); string before = Committed(); const string original = "original slot bytes"; File.WriteAllText(session.CampusSavePath, original);
        Directory.CreateDirectory(session.CampusSavePath + ".pending");
        Check(!session.TrySave(true), "Campus write must fail");
        Check(session.LastMessage.Contains("Không thể lưu Campus:"), "Campus test did not reach the IO error path: " + session.LastMessage);
        Check(File.ReadAllText(session.CampusSavePath) == original && Committed() == before, "Campus write failure modified old slot/session");
        Directory.Delete(session.CampusSavePath + ".pending"); yield return Frames(8); Controls();
    }
    IEnumerator SessionObserverCannotTravelDuringLocalCommit()
    {
        var data = CompleteFloor(); File.WriteAllText(entry.floorSave.SavePath, JsonUtility.ToJson(data));
        var office = new JourneyCheckpoint { scenePath = Office, floor = data, campusFacts = new[] { "campus:courtyard", "campus:map", "campus:direction" }, officeFacts = Array.Empty<string>(), position = new Vector3(0,1.08f,4) };
        File.WriteAllText(session.OfficeSavePath, JsonUtility.ToJson(office));
        bool? nested = null; entry.floorSave.Restored += _ => { if (nested == null) { nested = false; nested = session.TryContinue(JourneyArea.TrainingOffice); } };
        Check(session.TryContinue(JourneyArea.Floor01), "Local outer Continue failed");
        Check(nested == false && !session.IsLoading, "Observer started travel inside an uncommitted local restore");
        yield return Frames(8); Controls();
    }
    IEnumerator BadUiBindingDoesNotCommitDestination()
    {
        yield return AtCampus(); Set(session, "campusMemory", Array.Empty<string>()); string before = Committed();
        Check(session.TryEnterOffice(), "Office travel accepted");
        yield return Await(SceneManager.LoadSceneAsync(Office, LoadSceneMode.Additive));
        var bad = FindObjectsByType<JourneySceneEntry>().Single(e => e.isTrainingOffice); Set(bad.ui, "quests", null);
        TransactionBackendControl.Complete(backend, true); yield return Frames(8);
        Check(Committed() == before && Get(session, "current") == (object)entry, "Invalid UI binding committed destination before presentation failed");
        Controls();
    }
    IEnumerator NativeSingleTravelAndRecovery()
    {
        var data = CompleteFloor(); entry.floorSave.RestoreForTravel(data); yield return Frames(20);
        TransactionBackendControl.UseNative(core.Loader);
        var owner = core; var journey = session;
        Check(session.TryLeaveFloor(), "Native Campus request rejected");
        while (session.IsLoading) yield return null;
        yield return Frames(12); entry = FindObjectsByType<JourneySceneEntry>().Single();
        Check(session.Area == JourneyArea.Campus && entry.inventory.ItemIds.Count == 3, "Native Campus restore failed"); Controls();
        foreach (var fact in new[] { "campus:courtyard", "campus:map", "campus:direction" }) entry.quests.Signal(fact);
        // Corrupt only the first in-memory Office binding after activation. The authored asset is untouched.
        bool inject = true;
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler = (scene, mode) => {
            if (inject && scene.path == Office) { inject = false; FindObjectsByType<JourneySceneEntry>().Single(e => e.isTrainingOffice).spawn = null; }
        };
        SceneManager.sceneLoaded += handler;
        Check(session.TryEnterOffice(), "Native Office request rejected");
        while (session.IsLoading) yield return null;
        SceneManager.sceneLoaded -= handler;
        Check(RecoveryFlag() && !SceneManager.GetSceneByPath(Campus).isLoaded, "Native destructive Attach did not enter recovery");
        Check(Time.timeScale == 1 && Cursor.visible, "Native recovery input unavailable");
        Check(InvokeRecover(), "Native retry rejected");
        while (session.IsLoading) yield return null;
        yield return Frames(12); entry = FindObjectsByType<JourneySceneEntry>().Single();
        Check(!RecoveryFlag() && session.Area == JourneyArea.Campus && entry.quests.CampusIndex == 3, "Native recovery lost source facts"); Controls();
        Check(session.TryEnterOffice(), "Native Office retry rejected");
        while (session.IsLoading) yield return null;
        yield return Frames(12); entry = FindObjectsByType<JourneySceneEntry>().Single();
        foreach (var fact in new[] { "office:notice", "office:document", "office:computer" }) entry.quests.Signal(fact);
        Check(session.TryReturnCampus(), "Native return rejected");
        while (session.IsLoading) yield return null;
        yield return Frames(12); entry = FindObjectsByType<JourneySceneEntry>().Single();
        Check(entry.quests.OfficeIndex == 3 && entry.quests.CampusIndex == 3 && entry.inventory.ItemIds.Count == 3, "Native return lost confirmed progress");
        Check(owner == core && journey == session && FindObjectsByType<GameBootstrap>().Length == 1 && FindObjectsByType<JourneySession>().Length == 1, "Native travel duplicated owner");
        Check(Vector3.Distance(entry.player.transform.position, new Vector3(16,1.08f,51)) < .4f, "Native return spawn changed"); Controls();
    }
    IEnumerator UnsafeDestinationKeepsSourceInput()
    {
        yield return AtCampus(); OfficeSave(); string before = Committed();
        Check(session.TryContinue(JourneyArea.TrainingOffice), "Office request accepted");
        yield return Await(SceneManager.LoadSceneAsync(Office, LoadSceneMode.Additive));
        var bad = FindObjectsByType<JourneySceneEntry>().Single(e => e.isTrainingOffice);
        Block(bad.spawn.position); Block(new Vector3(0,1.08f,4));
        TransactionBackendControl.Complete(backend, true); yield return Frames(8);
        Check(Committed() == before && Get(session, "current") == (object)entry, "Unsafe destination modified source"); Controls();
    }
    IEnumerator NativeNewGameCommitsOnlyAfterFreshScene()
    {
        yield return AtCampus(); string before = Committed();
        const string saved = "untouched user save"; File.WriteAllText(session.CampusSavePath, saved);
        TransactionBackendControl.UseNative(core.Loader);
        var old = session;
        Check(session.TryNewGame(true), "Native New Game rejected");
        Check(Committed() == before, "New Game cleared confirmed state before activation");
        while (session.IsLoading) yield return null;
        yield return Frames(12); entry = FindObjectsByType<JourneySceneEntry>().Single();
        Check(session == old && session.Area == JourneyArea.Floor01 && !RecoveryFlag(), "New Game did not reuse session on Floor");
        Check(entry.inventory.ItemIds.Count == 0 && entry.quests.CurrentIndex == 0 && ((string[])Get(session,"campusMemory")).Length == 0 && ((string[])Get(session,"officeMemory")).Length == 0,
            "New Game did not reset live/confirmed progress");
        Check(File.ReadAllText(session.CampusSavePath) == saved, "New Game overwrote existing slot"); Controls();
    }
    IEnumerator InitialEntryWaitsForCoreCompletion()
    {
        Destroy(session.gameObject); yield return Frames(2);
        Check(core.Loader.TryLoadScene(Floor, GameState.Playing, out _), "Core Loading fixture rejected");
        var result = typeof(JourneySceneEntry).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(entry, null);
        Check(result is IEnumerator, "Entry Start does not wait for pending Core load");
        var start = (IEnumerator)result;
        Check(start.MoveNext() && FindObjectsByType<JourneySession>().Length == 0, "Entry initialized Session before Core completed");
        TransactionBackendControl.Complete(backend, true);
        Check(!start.MoveNext(), "Entry did not finish after Core completion");
        session = entry.Session;
        Check(session != null && Get(session, "current") == (object)entry && FindObjectsByType<GameBootstrap>().Length == 1, "Entry did not bind stable owner exactly once");
        session.StorageDirectoryOverride = storage;
        yield return Frames(8); Controls();
    }
    IEnumerator ClosingModalDuringFailedLoadRestoresControl()
    {
        yield return AtCampus(); OfficeSave();
        typeof(QuestPuzzleUI).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(entry.ui, new object[] { QuestModal.Progress });
        Check(session.TryContinue(JourneyArea.TrainingOffice), "Modal travel rejected");
        entry.ui.CloseModal(); yield return Frames(8);
        Check(entry.Gate.IsBlocked && Time.timeScale == 0, "Closing a modal unlocked gameplay while loading");
        FailLoad(); yield return Frames(8);
        Check(entry.ui.Mode == QuestModal.None, "Closed modal reopened"); Controls();
    }
    IEnumerator NativeFloorContinueNotifiesOnce()
    {
        yield return AtCampus();
        File.WriteAllText(session.FloorSavePath, JsonUtility.ToJson(CompleteFloor()));
        int? noticesAfterFloorRestore = null;
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler = (scene, mode) => {
            if (scene.path == Floor)
            {
                var destination = FindObjectsByType<JourneySceneEntry>().Single(e => !e.isCampus && !e.isTrainingOffice);
                destination.floorSave.Restored += _ => noticesAfterFloorRestore = destination.ui.NotificationCount;
            }
        };
        SceneManager.sceneLoaded += handler;
        TransactionBackendControl.UseNative(core.Loader);
        Check(session.TryContinue(JourneyArea.Floor01), "Native Floor Continue rejected");
        while (session.IsLoading) yield return null;
        SceneManager.sceneLoaded -= handler;
        yield return Frames(12); entry = FindObjectsByType<JourneySceneEntry>().Single();
        Check(noticesAfterFloorRestore.HasValue && entry.ui.NotificationCount == noticesAfterFloorRestore.Value,
            "Continue performed a second UI restore/new-objective notice after Floor restore");
        Check(entry.floorSave.RestoreCount == 1 && entry.inventory.ItemIds.Count == 3, "Floor checkpoint applied more than once"); Controls();
    }
    IEnumerator OfficeFactsRequireCompletedCampus()
    {
        yield return AtCampus(); string before = Committed();
        var data = new JourneyCheckpoint { scenePath = Campus, floor = session.FloorSnapshot, campusFacts = Array.Empty<string>(), officeFacts = new[] { "office:notice" }, position = entry.player.transform.position };
        File.WriteAllText(session.CampusSavePath, JsonUtility.ToJson(data));
        Check(!session.TryContinue(JourneyArea.Campus), "Save accepted Office progress without the three required Campus facts");
        Check(Committed() == before && entry.quests.CampusIndex == 3, "Inconsistent save modified confirmed/live progress");
        yield return Frames(8); Controls();
    }
    bool RecoveryFlag() => (bool)(typeof(JourneySession).GetProperty("RecoveryRequired")?.GetValue(session) ?? false);
    bool InvokeRecover() => (bool)(typeof(JourneySession).GetMethod("TryRecover")?.Invoke(session, null) ?? false);
    void FailLoad() { expectedLoadFailure = true; try { TransactionBackendControl.Complete(backend, false); } finally { expectedLoadFailure = false; } }

    void Controls()
    {
        Check(!entry.Gate.IsBlocked && Time.timeScale == 1, "Input/time lease leaked");
        Check(entry.player.enabled && entry.player.gameObject.activeInHierarchy && entry.player.GetComponent<PlayerController1>().isActiveAndEnabled, "Player/controller remained disabled");
        Check(entry.viewCamera.isActiveAndEnabled, "View camera remained inactive");
        Check(entry.viewCamera.GetComponent<ThirdPersonCameraFollow>().enabled, "Camera remained disabled");
        Check(!float.IsNaN(entry.viewCamera.transform.position.x), "Camera became NaN");
        Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Cursor state leaked");
        var follow = entry.viewCamera.GetComponent<ThirdPersonCameraFollow>();
        Check(((InputAction)Get(follow, "lookAction")).enabled, "Look input remained disabled");
        var controller = entry.player.GetComponent<PlayerController1>();
        Check(((InputAction)Get(controller, "moveAction")).enabled, "Move input remained disabled");
    }
    static IEnumerator Frames(int count) { for (int i=0;i<count;i++) yield return null; }
    static IEnumerator Await(AsyncOperation op) { while (!op.isDone) yield return null; }
    static object Get(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    static void Set(object value, string name, object data) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    void Finish()
    {
        Finished = true; Passed = failures == 0 && runtimeErrors.Count == 0;
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "transaction-results.txt"),
            $"{(Passed ? "PASS" : "FAIL")} {tests.Count-failures}/{tests.Count}\n" + string.Join("\n", results) + "\nRUNTIME ERRORS\n" + string.Join("\n", runtimeErrors) + "\nKNOWN EDITOR ERRORS\n" + string.Join("\n", editorErrors));
        Application.logMessageReceived -= Log;
    }
}
#endif
