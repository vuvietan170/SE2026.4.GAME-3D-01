#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TEMPORARY VT-103 manual-acceptance helper.
/// Redirects JourneySession's three manual slots and Floor01 autosave to a unique D: folder.
/// Delete this file after G05-G07. It is excluded from player builds.
/// </summary>
[InitializeOnLoad]
internal static class VT103ManualAcceptanceSandbox
{
    private const string SandboxParent = @"D:\Game3D-01\VT103_ManualAcceptance";
    private static readonly BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static string sessionRoot;
    private static readonly Dictionary<Component, string> LastLoggedState =
        new Dictionary<Component, string>();

    private static bool typesResolved;
    private static Type journeyType;
    private static Type saveType;
    private static Type autosaveType;

    static VT103ManualAcceptanceSandbox()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            sessionRoot = Path.Combine(
                SandboxParent,
                DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));

            Directory.CreateDirectory(sessionRoot);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.delayCall += Tick;

            Debug.Log("[VT103 SANDBOX] Started. Do not interact until the manual-slot paths are printed. Root: " + sessionRoot);
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EditorApplication.update -= Tick;
            LastLoggedState.Clear();
            sessionRoot = null;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EditorApplication.delayCall += Tick;
    }

    private static void ResolveTypesOnce()
    {
        if (typesResolved)
            return;

        // Unity 6.6-compatible type discovery; avoids AppDomain.GetAssemblies().
        Type[] behaviours = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().ToArray();

        journeyType = behaviours.FirstOrDefault(type =>
            type.FullName == "HUSEscape.World.JourneySession");
        saveType = behaviours.FirstOrDefault(type =>
            type.FullName == "HUSEscape.Persistence.Floor01SaveSystem");
        autosaveType = behaviours.FirstOrDefault(type =>
            type.FullName == "HUSEscape.Persistence.Floor01Autosave");

        typesResolved = true;

        if (journeyType == null || saveType == null || autosaveType == null)
        {
            Debug.LogError(
                "[VT103 SANDBOX] Could not resolve required component types. " +
                "JourneySession=" + (journeyType != null) +
                ", Floor01SaveSystem=" + (saveType != null) +
                ", Floor01Autosave=" + (autosaveType != null) +
                ". Stop Play Mode; do not run acceptance tests.");
        }
    }

    private static IEnumerable<Component> FindLoadedComponents(Type type)
    {
        if (type == null)
            yield break;

        foreach (UnityEngine.Object item in Resources.FindObjectsOfTypeAll(type))
        {
            Component component = item as Component;
            if (component != null && component.gameObject.scene.IsValid())
            {
                yield return component;
            }
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || string.IsNullOrEmpty(sessionRoot))
            return;

        ResolveTypesOnce();

        if (journeyType == null || saveType == null || autosaveType == null)
            return;

        Component journey = FindLoadedComponents(journeyType).FirstOrDefault();
        if (journey != null)
        {
            SetStorageOverride(journeyType, journey, sessionRoot);
            LogJourneyPaths(journeyType, journey);
        }

        foreach (Component save in FindLoadedComponents(saveType))
        {
            SetStorageOverride(saveType, save, sessionRoot);

            FieldInfo questField = saveType.GetField("quests", InstanceFlags);
            object quests = questField != null ? questField.GetValue(save) : null;

            Component autosave = autosaveType != null
                ? save.gameObject.GetComponent(autosaveType)
                : null;

            if (autosave == null && autosaveType != null)
                autosave = save.gameObject.AddComponent(autosaveType);

            if (autosave != null && quests != null)
            {
                string expectedDraft = Path.Combine(sessionRoot, "floor01-draft-v1.json");
                PropertyInfo draftPathProperty = autosaveType.GetProperty("DraftPath", InstanceFlags);
                string actualDraft = draftPathProperty != null
                    ? draftPathProperty.GetValue(autosave) as string
                    : null;

                // Awake initially binds to Application.persistentDataPath. Rebind after redirecting
                // the save system so both the manual Floor01 slot and the draft use the D: sandbox.
                if (!SamePath(actualDraft, expectedDraft))
                {
                    MethodInfo bind = autosaveType.GetMethod("Bind", InstanceFlags);
                    if (bind == null)
                        throw new MissingMethodException(autosaveType.FullName, "Bind");

                    bind.Invoke(autosave, new[] { save, quests });
                    Debug.Log("[VT103 SANDBOX] Floor01 autosave rebound to: " + expectedDraft);
                }

                LogAutosaveState(autosaveType, autosave);
            }
        }
    }

    private static void SetStorageOverride(Type type, object instance, string path)
    {
        PropertyInfo property = type.GetProperty("StorageDirectoryOverride", InstanceFlags);
        if (property == null)
            throw new MissingMemberException(type.FullName, "StorageDirectoryOverride");

        string current = property.GetValue(instance) as string;
        if (!SamePath(current, path))
            property.SetValue(instance, path);
    }

    private static bool SamePath(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return false;

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void LogJourneyPaths(Type type, Component journey)
    {
        string floor = ReadStringProperty(type, journey, "FloorSavePath");
        string campus = ReadStringProperty(type, journey, "CampusSavePath");
        string office = ReadStringProperty(type, journey, "OfficeSavePath");

        string message =
            "[VT103 SANDBOX] Manual slots: Floor=" + floor +
            " | Campus=" + campus +
            " | Office=" + office;

        LogIfChanged(journey, message);
    }

    private static void LogAutosaveState(Type type, Component autosave)
    {
        string draft = ReadStringProperty(type, autosave, "DraftPath");
        object commits = ReadProperty(type, autosave, "CommitCount");
        object revision = ReadProperty(type, autosave, "LastCommittedRevision");
        object pending = ReadProperty(type, autosave, "HasPendingChanges");
        string error = ReadStringProperty(type, autosave, "LastError");

        string message =
            "[VT103 SANDBOX] Autosave state: commits=" + commits +
            "; lastRevision=" + revision +
            "; pending=" + pending +
            "; error=" + (string.IsNullOrEmpty(error) ? "<none>" : error) +
            "; draft=" + draft;

        LogIfChanged(autosave, message);
    }

    private static object ReadProperty(Type type, object instance, string name)
    {
        PropertyInfo property = type.GetProperty(name, InstanceFlags);
        return property != null ? property.GetValue(instance) : null;
    }

    private static string ReadStringProperty(Type type, object instance, string name)
    {
        return ReadProperty(type, instance, name) as string;
    }

    private static void LogIfChanged(Component component, string message)
    {
        if (component == null)
            return;

        string previous;
        if (!LastLoggedState.TryGetValue(component, out previous) || previous != message)
        {
            LastLoggedState[component] = message;
            Debug.Log(message);
        }
    }
}
#endif
