// Runtime probe, staged only in a disposable validation project.
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
using HUSEscape.UI;
using HUSEscape.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class JourneyIntegrationProbe : MonoBehaviour
{
    public bool Finished { get; private set; }
    public bool Passed { get; private set; }
    private readonly List<string> checks = new List<string>(), errors = new List<string>();
    private readonly List<string> editorErrors = new List<string>();
    private readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
    private GameBootstrap owner;
    private JourneySession session;
    private JourneySceneEntry entry;
    private bool forcedEarlyStart;
    private float deadline;
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Time.captureDeltaTime = 1f / 60f; // Deterministic physics progress in headless runs.
        Application.logMessageReceived += Log;
        SceneManager.sceneLoaded += BeforeCompletion;
        deadline = Time.realtimeSinceStartup + 120;
        work.Push(Run());
    }
    private void Log(string message, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        string detail = message + "\n" + trace;
        // Keep the documented Editor Search startup defect visible separately.
        // Every other error, including runtime script errors, fails the probe.
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal) &&
            trace.Contains("UnityEditor.Search.SearchDatabase") && trace.Contains("IndexationOnStartup"))
            editorErrors.Add(detail);
        else errors.Add(detail);
    }
    private void BeforeCompletion(Scene scene, LoadSceneMode mode)
    {
        if (!scene.path.EndsWith("Floor01_Prototype.unity") || forcedEarlyStart) return;
        try
        {
            var candidate = FindObjectsByType<JourneySceneEntry>().Single();
            Check(owner.Loader.IsLoading, "Forced destination Start while native loader is Loading");
            var start = (IEnumerator)typeof(JourneySceneEntry).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(candidate, null);
            Check(start.MoveNext() && candidate.Session == null && candidate.Gate.IsBlocked,
                "Early Start waits and gates input without creating a session");
            forcedEarlyStart = true;
        }
        catch (Exception exception) { Finish(exception); }
    }
    private void Update()
    {
        if (Finished) return;
        try
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Native journey timed out");
            while (work.Count != 0)
            {
                var task = work.Peek();
                if (!task.MoveNext()) { work.Pop(); continue; }
                if (task.Current is IEnumerator nested) { work.Push(nested); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }
    private IEnumerator Run()
    {
        while (FindAnyObjectByType<MainMenuView>() == null) yield return null;
        owner = FindObjectsByType<GameBootstrap>().Single(b => b.IsInitialized);
        var view = FindAnyObjectByType<MainMenuView>();
        while (!view.CanStart) yield return null;
        Check(owner.Manager.CurrentState == GameState.Ready, "Bootstrap reaches a bound Ready menu");
        int completed = 0; owner.Loader.SceneLoaded += _ => completed++;
        var button = view.GetComponentInChildren<Button>(true);
        button.onClick.Invoke(); button.onClick.Invoke(); button.onClick.Invoke();
        yield return AwaitArea(JourneyArea.Floor01);
        session = entry.Session;
        Check(completed == 1 && forcedEarlyStart, "Repeated Start produces one Floor01 load");
        Check(!session.TryLeaveFloor(), "Incomplete Floor01 cannot leave");
        // Validated complete checkpoint fixture isolates travel from puzzle walkthrough.
        // No save file is read or written and no real player data is touched.
        var data = new Floor01SaveData {
            version = 1, scene = "Floor01_Prototype", savedUtc = DateTime.UtcNow.ToString("O"),
            questIds = entry.quests.Definitions.Select(q => q.Id).ToArray(),
            facts = entry.quests.Definitions.Select(q => q.CompletionEvent).ToArray(),
            inventory = new[] { "Key_A101", "Key_A102", "Key_Lab" },
            doors = new[] { "Key_A101", "Key_A102", "Key_Lab" }.Select(k => new SavedDoor { keyId = k, open = true }).ToArray(),
            cabinetUnlocked = true, sequenceSolved = true,
            lab = new SavedLab { power = true, instructions = true, connected = 3, data = true, complete = true },
            playerPosition = entry.spawn.position, playerYaw = entry.spawn.eulerAngles.y
        };
        entry.floorSave.RestoreForTravel(data);
        yield return Frames(12);
        var destination = typeof(JourneySession).GetField("campusPath", BindingFlags.Instance | BindingFlags.NonPublic);
        destination.SetValue(session, "Assets/_Project/Scenes/Missing.unity");
        Check(!session.TryLeaveFloor(), "Invalid destination is rejected before native load");
        yield return Frames(12);
        Check(!entry.Gate.IsBlocked && Time.timeScale == 1 && !session.IsLoading, "Rejected travel releases source input");
        destination.SetValue(session, entry.campusPath);
        Check(session.TryLeaveFloor(), "Completed Floor01 starts native Campus travel: " + session.LastMessage);
        Check(entry.Gate.IsBlocked && !session.TryLeaveFloor(), "Travel gates source input and rejects duplicates");
        yield return AwaitArea(JourneyArea.Campus);
        Check(!session.TryEnterOffice(), "Office requires three Campus discoveries");
        foreach (var fact in new[] { "campus:courtyard", "campus:map", "campus:direction" }) entry.quests.Signal(fact);
        Check(session.TryEnterOffice() && !session.TryEnterOffice(), "Office entry accepted once");
        yield return AwaitArea(JourneyArea.TrainingOffice);
        foreach (var fact in new[] { "office:notice", "office:document", "office:computer" }) entry.quests.Signal(fact);
        Check(session.TryReturnCampus() && !session.TryReturnCampus(), "Return Campus accepted once");
        yield return AwaitArea(JourneyArea.Campus);
        Check(entry.quests.CampusIndex == 3 && entry.quests.OfficeIndex == 3, "Campus and Office facts survive return");
        Check(Vector3.Distance(entry.player.transform.position, new Vector3(16, 1.08f, 51)) < .4f, "Return spawns by the Office entrance");
        Check(completed == 4, "Exactly four native gameplay loads");
    }
    private IEnumerator AwaitArea(JourneyArea area)
    {
        do { yield return null; entry = FindObjectsByType<JourneySceneEntry>().SingleOrDefault(); }
        while (entry == null || entry.Session == null || entry.Session.IsLoading || entry.Session.Area != area || owner.Loader.IsLoading);
        yield return Frames(12);
        Check(FindObjectsByType<GameBootstrap>(FindObjectsInactive.Include).Count(b => b.IsInitialized) == 1 && owner.Manager.CurrentState == GameState.Playing,
            area + ": same Core Playing");
        Check(FindObjectsByType<JourneySession>(FindObjectsInactive.Include).Length == 1 && (session == null || session == entry.Session), area + ": one persistent session");
        Check(ReferenceEquals(entry.Session.Loader, owner.Loader), area + ": same Core loader");
        Check(!entry.Gate.IsBlocked && Time.timeScale == 1, area + ": input and time restored");
        Check(entry.viewCamera.isActiveAndEnabled && entry.viewCamera.GetComponent<ThirdPersonCameraFollow>().enabled &&
            !float.IsNaN(entry.viewCamera.transform.position.x) && FindObjectsByType<Camera>().Count(c => c.isActiveAndEnabled) == 1,
            area + ": one active finite camera");
        var follow = entry.viewCamera.GetComponent<ThirdPersonCameraFollow>();
        Check((Transform)typeof(ThirdPersonCameraFollow).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(follow) == entry.player.transform,
            area + ": camera targets scene-local player");
        Check(Vector3.Distance(entry.player.transform.position, entry.spawn.position) < .4f || area == JourneyArea.Campus, area + ": authored spawn");
        if (area != JourneyArea.Floor01)
        {
            Check(entry.inventory.ItemIds.SequenceEqual(new[] { "Key_A101", "Key_A102", "Key_Lab" }) && entry.quests.IsComplete, area + ": inventory and Floor quests survive");
            Check(Vector3.Distance(entry.player.transform.position, entry.spawn.position) < .4f || entry.quests.OfficeIndex == 3, area + ": spawn or explicit return checkpoint");
        }
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private void Check(bool ok, string description) { if (!ok) throw new Exception(description); checks.Add(description); }
    private void Finish(Exception exception)
    {
        if (Finished) return;
        if (exception != null) errors.Add(exception.ToString());
        Finished = true; Passed = errors.Count == 0;
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "journey-integration-result.txt"),
            (Passed ? "PASS" : "FAIL") + "\n" + string.Join("\n", checks) + "\nERRORS\n" + string.Join("\n", errors) + "\nKNOWN EDITOR SEARCH ERRORS (" + editorErrors.Count + ")\n" + string.Join("\n", editorErrors));
        Application.logMessageReceived -= Log; SceneManager.sceneLoaded -= BeforeCompletion;
    }
}
#endif
