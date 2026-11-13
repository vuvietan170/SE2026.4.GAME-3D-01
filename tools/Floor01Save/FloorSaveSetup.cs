using System;
using System.IO;
using System.Linq;
using HUSEscape.Inventory;
using HUSEscape.Persistence;
using HUSEscape.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FloorSaveSetup
{
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); string report = args[Array.IndexOf(args, "-validationReport") + 1];
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Floor01_Prototype.unity", OpenSceneMode.Single);
            if (UnityEngine.Object.FindObjectsByType<Floor01SaveSystem>().Length != 0) throw new InvalidOperationException("Save component already authored.");
            var quests = UnityEngine.Object.FindObjectsByType<QuestManager>().Single();
            var ui = UnityEngine.Object.FindObjectsByType<QuestPuzzleUI>().Single();
            var save = quests.gameObject.AddComponent<Floor01SaveSystem>();
            var data = new SerializedObject(save);
            data.FindProperty("quests").objectReferenceValue = quests;
            data.FindProperty("inventory").objectReferenceValue = quests.Inventory;
            data.FindProperty("player").objectReferenceValue = quests.Inventory.GetComponent<CharacterController>();
            data.FindProperty("cabinet").objectReferenceValue = UnityEngine.Object.FindObjectsByType<CodeLockCabinet>().Single();
            data.FindProperty("sequence").objectReferenceValue = UnityEngine.Object.FindObjectsByType<SequenceLockPanel>().Single();
            data.FindProperty("lab").objectReferenceValue = UnityEngine.Object.FindObjectsByType<LabRestoreSystem>().Single();
            var doors = UnityEngine.Object.FindObjectsByType<DoorInteraction>().OrderBy(d => d.RequiredKeyId).ToArray();
            var doorList = data.FindProperty("doors"); doorList.arraySize = doors.Length;
            for (int i = 0; i < doors.Length; i++) doorList.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
            var pickups = UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include).OrderBy(p => p.ItemId).ToArray();
            var itemList = data.FindProperty("pickups"); itemList.arraySize = pickups.Length;
            for (int i = 0; i < pickups.Length; i++) itemList.GetArrayElementAtIndex(i).objectReferenceValue = pickups[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            var view = new SerializedObject(ui); view.FindProperty("saveSystem").objectReferenceValue = save; view.ApplyModifiedPropertiesWithoutUndo();
            if (quests.Definitions.Count != 17) throw new InvalidOperationException("All 17 accepted objectives are required.");
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Scene.");
            File.WriteAllText(report, "{\"passed\":true,\"saveComponent\":1,\"objectives\":17}"); EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText(report, e.ToString()); EditorApplication.Exit(1); }
    }
}
