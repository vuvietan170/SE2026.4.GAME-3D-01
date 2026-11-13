using System;
using System.IO;
using System.Linq;
using System.Collections;
using HUSEscape.World;
using HUSEscape.Quests;
using HUSEscape.Inventory;
using HUSEscape.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CampusColdProbe : MonoBehaviour
{
    public string reportPath,checkpointDirectory;
    public bool Finished{get;private set;}public bool Passed=>report.passed;
    private readonly CampusReport report=new CampusReport();private IEnumerator steps;private float previousCapture;
    private void Start(){DontDestroyOnLoad(gameObject);report.unity=Application.unityVersion;previousCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;Application.logMessageReceived+=Log;steps=Run();}
    private void Update(){if(Finished)return;try{if(!steps.MoveNext())Finish(null);}catch(Exception e){Finish(e);}}
    private IEnumerator Run()
    {
        for(int i=0;i<25;i++)yield return null;
        var entry=FindObjectsByType<JourneySceneEntry>().Single();var session=entry.Session;var core=FindObjectsByType<GameBootstrap>().Single(c=>c.IsInitialized);
        Check("Cold process starts with fresh Floor and no in-memory snapshot",entry.quests.CurrentIndex==0&&entry.inventory.ItemIds.Count==0&&session.FloorSnapshot==null);
        session.StorageDirectoryOverride=Path.Combine(Path.GetDirectoryName(reportPath),"MissingSlot");Check("Missing Campus slot is safe",!session.TryContinue(true)&&!session.IsLoading&&entry.quests.CurrentIndex==0);
        session.StorageDirectoryOverride=checkpointDirectory;
        string campus=File.ReadAllText(session.CampusSavePath),legacy=File.ReadAllText(Path.Combine(checkpointDirectory,"floor01-v1.json"));
        Check("Cold Continue Campus starts production native load",session.TryContinue(true));
        float end=Time.realtimeSinceStartup+25;while(session.IsLoading||SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/Campus_Prototype.unity"){if(Time.realtimeSinceStartup>end)throw new Exception("Cold load timeout: "+session.LastMessage);yield return null;}
        for(int i=0;i<20;i++)yield return null;entry=FindObjectsByType<JourneySceneEntry>().Single();
        Check("Cold native load activates Campus and retains Core",entry.isCampus&&core!=null&&core.Manager.CurrentState==GameState.Playing&&!session.Loader.IsLoading);
        Check("Cold load reconstructs all floor states and original keys",entry.quests.IsComplete&&entry.quests.Definitions.Count==17&&entry.inventory.ItemIds.Count==3&&new[]{"Key_A101","Key_A102","Key_Lab"}.All(entry.inventory.HasItem));
        Check("Cold load restores Campus objective facts and grounded pose",entry.quests.CampusIndex==3&&entry.player.isGrounded&&Vector3.Distance(entry.player.transform.position,new Vector3(14.3f,1.08f,48))<.3f);
        Check("Cold load never duplicates scene-owned services",FindObjectsByType<PlayerInventory>().Length==1&&FindObjectsByType<QuestManager>().Length==1&&FindObjectsByType<Camera>().Length==1&&FindObjectsByType<JourneySession>().Length==1);
        Check("Cold load cannot respawn floor pickups in Campus",FindObjectsByType<PickupItem>(FindObjectsInactive.Include).Length==0);
        Check("Cold Continue leaves both save slots byte-identical",File.ReadAllText(session.CampusSavePath)==campus&&File.ReadAllText(Path.Combine(checkpointDirectory,"floor01-v1.json"))==legacy);
        Check("Cold Continue does not replay completion notifications",entry.ui.VisibleNotification.Length==0);
        Check("Cold Continue has no production runtime errors",report.errors.Count==0);
    }
    private void Check(string name,bool value){report.checks.Add(new CampusCheck{name=name,passed=value});if(!value)throw new InvalidOperationException(name);}
    private void Log(string m,string s,LogType t){if(t!=LogType.Error&&t!=LogType.Exception&&t!=LogType.Assert)return;if(s.Contains("UnityEditor.Search.SearchDatabase")&&s.Contains("IndexationOnStartup"))report.editorStartupErrors.Add(m+"\n"+s);else report.errors.Add(m+"\n"+s);}
    private void Finish(Exception e){if(e!=null)report.errors.Add(e.ToString());Time.captureDeltaTime=previousCapture;Application.logMessageReceived-=Log;report.passed=e==null&&report.errors.Count==0;File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));Finished=true;}
}
