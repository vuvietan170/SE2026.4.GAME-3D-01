using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HUSEscape.World;
using HUSEscape.Quests;
using HUSEscape.Inventory;
using HUSEscape.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[Serializable] public sealed class CampusAuditReport {public bool passed;public int scenes,scriptGuids,missingScripts;public string error;}
public static class CampusAudit
{
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();string output=args[Array.IndexOf(args,"-validationReport")+1];var report=new CampusAuditReport();
        try
        {
            var guids=new System.Collections.Generic.HashSet<string>();
            foreach(string path in new[]{CampusSetup.Floor,CampusSetup.Campus})
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var roots=scene.GetRootGameObjects();
                var all=roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();report.missingScripts+=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                var entries=all.Select(t=>t.GetComponent<JourneySceneEntry>()).Where(e=>e!=null).ToArray();if(entries.Length!=1)throw new Exception("Expected one composition in "+path);var entry=entries[0];
                if(entry.player==null||entry.inventory==null||entry.quests==null||entry.ui==null||entry.viewCamera==null||entry.spawn==null||entry.quests.Definitions.Count!=17)throw new Exception("Incomplete composition "+path);
                Check(entry.quests,"inventory",entry.inventory);Check(entry.ui,"quests",entry.quests);Check(entry.ui,"inputGate",entry.Gate);Check(entry.ui,"inventoryUI",entry.player.GetComponent<InventoryUI>());
                Check(entry.player.GetComponent<PlayerController1>(),"cameraTransform",entry.viewCamera.transform);Check(entry.viewCamera.GetComponent<ThirdPersonCameraFollow>(),"target",entry.player.transform);Check(entry.player.GetComponent<InventoryUI>(),"inventory",entry.inventory);
                if(entry.isCampus){if(entry.floorSave!=null||entry.actorRoot.activeSelf||entry.guards.Length!=1||entry.guards[0].waypoints.Length!=4)throw new Exception("Unsafe Campus lifecycle composition");Check(entry.ui,"saveSystem",null);}
                else{if(entry.floorSave==null)throw new Exception("Floor V1 service missing");Check(entry.ui,"saveSystem",entry.floorSave);}
                foreach(Match match in Regex.Matches(File.ReadAllText(path),@"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})"))
                {string guid=match.Groups[1].Value;guids.Add(guid);if(string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))throw new Exception("Unresolved script GUID "+guid);}
                report.scenes++;
            }
            if(report.missingScripts!=0)throw new Exception("Missing MonoBehaviour references");report.scriptGuids=guids.Count;report.passed=true;
        }
        catch(Exception e){report.error=e.ToString();}
        File.WriteAllText(output,JsonUtility.ToJson(report,true));EditorApplication.Exit(report.passed?0:1);
    }
    private static void Check(Component component,string field,UnityEngine.Object expected){if(new SerializedObject(component).FindProperty(field).objectReferenceValue!=expected)throw new Exception(component.name+" wrong binding "+field);}
}
