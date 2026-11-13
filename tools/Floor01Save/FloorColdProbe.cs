using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HUSEscape.Persistence;
using HUSEscape.Quests;
using HUSEscape.Inventory;
using UnityEngine;

[Serializable] public sealed class ColdCheck { public string name; public bool passed; }
[Serializable] public sealed class ColdReport { public bool passed; public List<ColdCheck> checks=new List<ColdCheck>(); public string error; public List<string> errors=new List<string>(); public List<string> editorStartupErrors=new List<string>(); }
public sealed class FloorColdProbe : MonoBehaviour
{
    public string reportPath,saveDirectory;
    public bool Finished{get;private set;} public bool Passed=>report.passed;
    private readonly ColdReport report=new ColdReport();
    private IEnumerator Start()
    {
        Application.logMessageReceived+=OnLog;
        for(int i=0;i<35;i++)yield return null;
        // A fresh high-FPS Editor may run 35 frames before gravity has settled the Player.
        var controller=FindAnyObjectByType<CharacterController>();
        for(int i=0;i<300&&controller!=null&&!controller.isGrounded;i++)yield return new WaitForSecondsRealtime(.02f);
        try
        {
            var save=FindObjectsByType<Floor01SaveSystem>().Single();save.StorageDirectoryOverride=saveDirectory;
            var quests=FindObjectsByType<QuestManager>().Single();var inventory=FindObjectsByType<PlayerInventory>().Single();
            Check("Fresh process starts with fresh scene state",quests.CurrentIndex==0&&inventory.ItemIds.Count==0);
            string original=File.ReadAllText(save.SavePath);bool loaded=save.TryContinue();Check("Cold Continue reads disk checkpoint: "+save.LastMessage,loaded);
            Check("Cold Continue restores completed floor",quests.IsComplete&&quests.CurrentIndex==17&&FindObjectsByType<LabRestoreSystem>().Single().FloorCompleted);
            Check("Cold Continue restores all original inventory keys",inventory.ItemIds.Count==3&&new[]{"Key_A101","Key_A102","Key_Lab"}.All(inventory.HasItem));
            Check("Cold Continue restores settled doors",FindObjectsByType<DoorInteraction>().All(d=>!d.IsLocked&&d.IsOpen&&!d.IsBusy));
            Check("Collected pickups remain absent after cold load",FindObjectsByType<PickupItem>(FindObjectsInactive.Include).All(p=>p.IsCollected&&!p.gameObject.activeSelf));
            Check("Cold Continue never writes disk save",File.ReadAllText(save.SavePath)==original);
        }
        catch(Exception e){report.error=e.ToString();Finish();yield break;}
        for(int i=0;i<30;i++)yield return null;
        try
        {
            var inventory=FindObjectsByType<PlayerInventory>().Single();var ui=FindObjectsByType<QuestPuzzleUI>().Single();
            Check("No delayed rewards or completion toast replay",inventory.ItemIds.Count==3&&ui.VisibleNotification.Length==0);
            Check("Player camera survives cold restore",FindObjectsByType<Camera>().All(c=>!float.IsNaN(c.transform.position.x)));
            Check("Cold restoration has no production runtime errors",report.errors.Count==0);
        }
        catch(Exception e){report.error=e.ToString();}
        Finish();
    }
    private void Check(string name,bool value){report.checks.Add(new ColdCheck{name=name,passed=value});if(!value)throw new InvalidOperationException(name);}
    private void OnLog(string m,string s,LogType t)
    {
        if(t!=LogType.Error&&t!=LogType.Exception&&t!=LogType.Assert)return;
        if(s.Contains("UnityEditor.Search.SearchDatabase")&&s.Contains("IndexationOnStartup"))report.editorStartupErrors.Add(m+"\n"+s);
        else report.errors.Add(m+"\n"+s);
    }
    private void Finish(){Application.logMessageReceived-=OnLog;report.passed=report.error==null&&report.errors.Count==0;File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));Finished=true;}
}
