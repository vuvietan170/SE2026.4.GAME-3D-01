using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HUSEscape.World;
using HUSEscape.Quests;
using HUSEscape.Persistence;
using HUSEscape.Inventory;
using HUSEscape.Player;
using HUSEscape.Core;
using HUSEscape.AI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[Serializable] public sealed class CampusCheck { public string name; public bool passed; }
[Serializable] public sealed class CampusReport
{
    public bool passed;public string unity;public List<CampusCheck> checks=new List<CampusCheck>();public List<string> errors=new List<string>();public List<string> editorStartupErrors=new List<string>();
    public int exitCameraFrames;public string exitCameraFailure;
}
public sealed class CampusPlayProbe : MonoBehaviour
{
    public string reportPath;
    public bool Finished {get;private set;}
    public bool Passed=>report.passed;
    private readonly CampusReport report=new CampusReport();private readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
    private JourneySceneEntry entry;private JourneySession session;private Keyboard keyboard;private Mouse mouse;private InputSettings settings,oldSettings;private float oldCapture;
    private void Start(){DontDestroyOnLoad(gameObject);report.unity=Application.unityVersion;Application.logMessageReceived+=Log;oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;stack.Push(Run());}
    private void Update(){if(Finished)return;try{while(stack.Count>0){var e=stack.Peek();if(!e.MoveNext()){stack.Pop();continue;}if(e.Current is IEnumerator child){stack.Push(child);continue;}return;}Finish(null);}catch(Exception e){Finish(e);}}
    private IEnumerator Run()
    {
        yield return Frames(25);entry=FindObjectsByType<JourneySceneEntry>().Single();session=entry.Session;
        Check("One initialized Core owner and real Single-mode loader",FindObjectsByType<GameBootstrap>().Count(b=>b.IsInitialized)==1&&session.Loader!=null);
        oldSettings=InputSystem.settings;settings=UnityEngine.Object.Instantiate(oldSettings);settings.hideFlags=HideFlags.HideAndDontSave;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        InputSystem.settings=settings;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
        var route=gameObject.AddComponent<ExitRouteProbe>();var exit=FindObjectsByType<BuildingExit>().Single();
        Vector3 spawn=entry.player.transform.position;Place(new Vector3(0,1.08f,60));yield return Frames(30);
        Check("No exit interaction remains in original corridor",!exit.IsNear&&!exit.CanShowPrompt);
        route.Monitor=true;yield return Walk(0,64.5f);yield return Walk(0,70.5f);Check("WASD reaches visible final T3 door through real opening",exit.IsNear&&exit.CanShowPrompt);yield return Press(Key.E);
        Check("E at exit rejects incomplete Floor 01",SceneManager.GetActiveScene().path==entry.floorPath&&!session.IsLoading&&entry.quests.CurrentIndex==0);
        Check("Explicit travel API also rejects incomplete floor",!session.TryLeaveFloor());yield return Walk(0,64.5f);yield return Walk(0,60);route.Monitor=false;Place(spawn);yield return Frames(8);
        string nested=Path.Combine(Path.GetDirectoryName(reportPath),"FloorRegression");Directory.CreateDirectory(nested);
        var floorProbe=gameObject.AddComponent<FloorSavePlayProbe>();floorProbe.reportPath=Path.Combine(nested,"floor-regression.json");
        while(!floorProbe.Finished)yield return null;
        Check("Accepted Floor 01 suite remains 388/388",floorProbe.Passed);
        string storage=Path.Combine(Path.GetDirectoryName(reportPath),"Saves");Directory.CreateDirectory(storage);session.StorageDirectoryOverride=storage;
        string legacy=File.ReadAllText(Path.Combine(nested,"Saves/floor01-v1.json"));File.WriteAllText(Path.Combine(storage,"floor01-v1.json"),legacy);
        entry.floorSave.StorageDirectoryOverride=storage;
        var owner=FindObjectsByType<GameBootstrap>().Single(b=>b.IsInitialized);var manager=owner.Manager;
        var originalPlayer=entry.player;var originalCamera=entry.viewCamera;var originalQuest=entry.quests;
        yield return Press(Key.Tab);if(entry.player.GetComponent<InventoryUI>().IsOpen)yield return Press(Key.Tab);
        yield return Walk(17.35f,53);yield return Walk(5.8f,53);yield return Walk(0,53);yield return Walk(0,60);
        Check("Completed floor still has no invisible corridor portal",!exit.IsNear);route.Monitor=true;
        yield return Walk(0,64.5f);yield return Walk(0,66.5f);
        var follow=entry.viewCamera.GetComponent<ThirdPersonCameraFollow>();var yaw=typeof(ThirdPersonCameraFollow).GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(float angle in new[]{0f,90f,180f,270f}){yaw.SetValue(follow,angle);yield return Frames(90);}yaw.SetValue(follow,0f);yield return Frames(90);
        yield return Walk(0,70.5f);Check("Visible final-door prompt is available after LAB",exit.IsNear&&exit.CanShowPrompt&&exit.PromptText.Contains("CAMPUS"));
        Vector3 atDoor=entry.player.transform.position;Place(atDoor+Vector3.up*4);Physics.SyncTransforms();Check("Interaction volume rejects an actor at another elevation",!exit.IsNear);Place(atDoor);yield return Frames(10);
        Check("Save V1 accepts the new ground-level vestibule",entry.floorSave.TrySave(true));legacy=File.ReadAllText(Path.Combine(storage,"floor01-v1.json"));
        Check("Vestibule checkpoint remains V1 and preserves all 17 facts",JsonUtility.FromJson<Floor01SaveData>(legacy).version==1&&JsonUtility.FromJson<Floor01SaveData>(legacy).playerPosition.z>69&&entry.quests.CurrentIndex==17);
        report.exitCameraFrames=route.Samples;report.exitCameraFailure=route.Failure;Check("Camera and near plane clear the exit walls and doors",route.Samples>40&&route.Failure==null);route.Monitor=false;
        yield return Frames(20);Check("Walking near completed exit never auto-loads",SceneManager.GetActiveScene().path==entry.floorPath);
        yield return Press(Key.E);Check("Repeated exit request rejected",!session.TryLeaveFloor());yield return Transition(true);
        Check("Production Single replaces source scene and actors",!SceneManager.GetSceneByPath("Assets/_Project/Scenes/Floor01_Prototype.unity").isLoaded&&originalPlayer==null&&originalCamera==null&&originalQuest==null);
        Check("Same Core owner and manager survive native load",owner!=null&&ReferenceEquals(owner.Manager,manager)&&manager.CurrentState==GameState.Playing&&!session.Loader.IsLoading);
        Check("Campus spawns at T3 with safe ground",Vector3.Distance(entry.player.transform.position,new Vector3(0,1.08f,8))<.25f&&entry.player.isGrounded);
        Unique();Check("17 accepted floor objectives and keys retained",entry.quests.IsComplete&&entry.quests.Definitions.Count==17&&entry.quests.JournalDefinitions.Count==20&&entry.inventory.ItemIds.Count==3);
        Check("Campus HUD starts exploration rather than Game Over",entry.quests.CampusActive&&entry.quests.CurrentObjective.Contains("sân trường")&&manager.CurrentState==GameState.Playing);
        Place(new Vector3(0,1.08f,1.2f));yield return Frames(90);Check("Existing camera retracts against actual T3 facade",Vector3.Distance(entry.viewCamera.transform.position,entry.player.transform.position+Vector3.up*1.2f)<3&&entry.viewCamera.transform.position.z>.25f);
        Place(new Vector3(27.5f,1.08f,8));InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return Frames(45);InputSystem.QueueStateEvent(keyboard,new KeyboardState());Check("Campus boundary physically prevents leaving ground",entry.player.transform.position.x<28.1f&&entry.player.isGrounded);Place(new Vector3(0,1.08f,8));yield return Frames(60);
        float startZ=entry.player.transform.position.z;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));yield return Frames(45);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(4);Check("Campus WASD and Shift preserve sprint speed",entry.player.transform.position.z-startZ>4);
        Check("Travel never writes a save automatically",File.ReadAllText(Path.Combine(storage,"floor01-v1.json"))==legacy&&!File.Exists(session.CampusSavePath));
        yield return Press(Key.J);Vector3 at=entry.player.transform.position;yield return Press(Key.W,Key.E,Key.F,Key.Tab);yield return Frames(12);
        Check("Journal gates movement E F and Tab",entry.ui.Mode==QuestModal.Journal&&Vector3.Distance(at,entry.player.transform.position)<.01f&&!entry.player.GetComponent<InventoryUI>().IsOpen);
        yield return Press(Key.Escape);yield return Frames(5);yield return Press(Key.F5);Check("F5 Campus menu uses existing input gate",entry.ui.Mode==QuestModal.Progress&&entry.Gate.IsBlocked);yield return Press(Key.Escape);yield return Frames(5);
        yield return Walk(0,20);Check("Courtyard activates real exploration fact",entry.quests.HasObserved("campus:courtyard")&&entry.quests.CampusIndex==1);
        yield return Walk(0,32);yield return Walk(-12,32);yield return Walk(-12,28.5f);yield return Press(Key.E);Check("E reads map and advances same QuestManager",entry.quests.HasObserved("campus:map")&&entry.quests.CampusIndex==2);
        float ground=entry.player.transform.position.y;yield return Press(Key.Space);float peak=ground;for(int i=0;i<80;i++){peak=Mathf.Max(peak,entry.player.transform.position.y);yield return null;}
        Check("Space jump and landing remain functional",peak>ground+.5f&&entry.player.isGrounded);
        yield return Press(Key.LeftCtrl);var crouch=typeof(PlayerController1).GetField("isCrouching",BindingFlags.Instance|BindingFlags.NonPublic);Check("Ctrl preserves existing toggle crouch",(bool)crouch.GetValue(entry.player.GetComponent<PlayerController1>()));yield return Press(Key.LeftCtrl);Check("Second Ctrl returns to ordinary locomotion",!(bool)crouch.GetValue(entry.player.GetComponent<PlayerController1>()));
        var ai=entry.guards.Single();int reached=ai.CompletedWaypoints;yield return Frames(850);
        Check("Guard physically follows multiple waypoints",ai.CompletedWaypoints>=reached+2&&ai.transform.position.x>=11.5f&&ai.transform.position.x<=22.5f);
        ai.transform.rotation=Quaternion.identity;Place(ai.transform.position+Vector3.forward*6);Physics.SyncTransforms();Check("Guard sees unobstructed player inside cone",ai.CanSee(entry.player.transform));yield return Frames(10);
        Check("Visible player creates Suspicious state",ai.State==GuardState.Suspicious);yield return Frames(100);Check("Sustained evidence creates Alert without combat",ai.State==GuardState.Alert&&manager.CurrentState==GameState.Playing);
        var colors=new MaterialPropertyBlock();ai.GetComponentInChildren<Renderer>().GetPropertyBlock(colors);Check("Alert has visible red material feedback",colors.GetColor("_BaseColor").r>.9f&&colors.GetColor("_BaseColor").g<.1f);
        var guardController=ai.GetComponent<CharacterController>();guardController.enabled=false;ai.transform.position=new Vector3(12,1.08f,30);ai.transform.rotation=Quaternion.Euler(0,270,0);guardController.enabled=true;Place(new Vector3(0,1.08f,30));Physics.SyncTransforms();
        Check("Actual Campus cover wall occludes player",!ai.CanSee(entry.player.transform));yield return Frames(250);Check("Lost visibility clears Alert and resumes patrol",!ai.HasLineOfSight&&ai.State==GuardState.Patrol);
        ai.transform.rotation=Quaternion.Euler(0,90,0);Check("Sight cone rejects player behind guard",!ai.CanSee(entry.player.transform));
        Place(new Vector3(-12,1.08f,28.5f));yield return Frames(15);yield return Walk(-12,32);yield return Walk(0,32);yield return Walk(0,48);yield return Walk(14.3f,48);yield return Press(Key.E);
        Check("Training Office direction completes exploration only",entry.quests.CampusIndex==3&&SceneManager.GetActiveScene().path==entry.campusPath&&entry.quests.IsComplete);
        Check("Manual Campus save succeeds",session.TrySave());string campus=File.ReadAllText(session.CampusSavePath);
        Check("Existing Campus slot requires overwrite confirmation",!session.TrySave()&&File.ReadAllText(session.CampusSavePath)==campus);
        Check("Campus save cannot modify Floor V1",File.ReadAllText(Path.Combine(storage,"floor01-v1.json"))==legacy);
        var invalid=JsonUtility.FromJson<JourneyCheckpoint>(campus);invalid.version=99;File.WriteAllText(session.CampusSavePath,JsonUtility.ToJson(invalid));
        Check("Incompatible Campus save rejected without scene or quest mutation",!session.TryContinue(true)&&entry.quests.CampusIndex==3&&entry.inventory.ItemIds.Count==3);
        File.WriteAllText(session.CampusSavePath,"{bad json");Check("Corrupt Campus JSON rejected safely",!session.TryContinue(true)&&session.IsCampus);File.WriteAllText(session.CampusSavePath,campus);
        Check("Continue Floor V1 from Campus accepted",session.TryContinue(false));yield return Transition(false);Unique();
        Check("Legacy Continue restores original puzzle flags and keys",entry.quests.IsComplete&&entry.inventory.ItemIds.Count==3&&FindObjectsByType<DoorInteraction>().All(d=>!d.IsLocked&&d.IsOpen)&&FindObjectsByType<LabRestoreSystem>().Single().FloorCompleted);
        Check("Continue Floor restores the saved vestibule pose",entry.player.transform.position.z>69&&FindObjectsByType<BuildingExit>().Single().CanShowPrompt);
        var pathField=typeof(JourneySession).GetField("campusPath",BindingFlags.Instance|BindingFlags.NonPublic);string correct=(string)pathField.GetValue(session);pathField.SetValue(session,"Assets/_Project/Scenes/Missing.unity");
        Check("Missing destination recovers source and manager",!session.TryLeaveFloor()&&!session.IsLoading&&manager.CurrentState==GameState.Playing);pathField.SetValue(session,correct);yield return Frames(6);
        Check("Continue Campus from Floor accepted",session.TryContinue(true));yield return Transition(true);Unique();
        Check("Campus Continue restores pose facts keys and 17 completed objectives",entry.quests.CampusIndex==3&&entry.inventory.ItemIds.Count==3&&Vector3.Distance(entry.player.transform.position,new Vector3(14.3f,1.08f,48))<.3f&&entry.quests.IsComplete);
        Check("New Game explicitly returns to Floor without deleting saves",session.TryNewGame(true));yield return Transition(false);Unique();
        Check("New Game has accepted initial state",entry.quests.CurrentIndex==0&&entry.inventory.ItemIds.Count==0&&entry.floorSave.HasSaveFile&&!session.TryLeaveFloor());
        Check("New Game and Continue never overwrite either slot",File.ReadAllText(Path.Combine(storage,"floor01-v1.json"))==legacy&&File.ReadAllText(session.CampusSavePath)==campus);
        Check("No production runtime errors",report.errors.Count==0);
    }
    private void Unique(){Check("Exactly one active Player Camera Inventory QuestManager and session",FindObjectsByType<CharacterController>().Count(c=>c.GetComponent<PlayerInventory>()!=null)==1&&FindObjectsByType<Camera>().Length==1&&FindObjectsByType<PlayerInventory>().Length==1&&FindObjectsByType<QuestManager>().Length==1&&FindObjectsByType<JourneySession>().Length==1);}
    private IEnumerator Transition(bool campus){float until=Time.realtimeSinceStartup+25;while(session.IsLoading||SceneManager.GetActiveScene().path!=(campus?"Assets/_Project/Scenes/Campus_Prototype.unity":"Assets/_Project/Scenes/Floor01_Prototype.unity")){if(Time.realtimeSinceStartup>until)throw new Exception("Transition timeout: "+session.LastMessage);yield return null;}yield return Frames(20);entry=FindObjectsByType<JourneySceneEntry>().Single();Check("Destination activates automatically",entry.isCampus==campus&&entry.Session==session);}
    private IEnumerator Walk(float x,float z){float until=Time.time+25;while(Vector2.Distance(new Vector2(entry.player.transform.position.x,entry.player.transform.position.z),new Vector2(x,z))>.15f){if(Time.time>until)throw new Exception("Blocked walk "+x+","+z+" at "+entry.player.transform.position);Vector3 p=entry.player.transform.position;var keys=new List<Key>();if(Mathf.Abs(x-p.x)>.08f)keys.Add(x>p.x?Key.D:Key.A);if(Mathf.Abs(z-p.z)>.08f)keys.Add(z>p.z?Key.W:Key.S);InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));yield return null;}InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(5);Check("Campus/floor path walk stays grounded",entry.player.isGrounded);}
    private void Place(Vector3 p){entry.player.enabled=false;entry.player.transform.SetPositionAndRotation(p,Quaternion.identity);entry.player.enabled=true;}
    private IEnumerator Press(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(4);}
    private IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    private void Check(string name,bool value){report.checks.Add(new CampusCheck{name=name,passed=value});if(!value)throw new InvalidOperationException(name+"; "+session?.LastMessage);}
    private void Log(string m,string s,LogType t){if(t!=LogType.Error&&t!=LogType.Exception&&t!=LogType.Assert)return;if(s.Contains("UnityEditor.Search.SearchDatabase")&&s.Contains("IndexationOnStartup"))report.editorStartupErrors.Add(m+"\n"+s);else report.errors.Add(m+"\n"+s);}
    private void Finish(Exception e){if(e!=null)report.errors.Add(e.ToString());Application.logMessageReceived-=Log;if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);if(oldSettings!=null)InputSystem.settings=oldSettings;if(settings!=null)Destroy(settings);Time.captureDeltaTime=oldCapture;report.passed=e==null&&report.errors.Count==0;File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));Finished=true;}
}
