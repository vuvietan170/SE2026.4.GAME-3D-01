using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using HUSEscape.World;
using HUSEscape.Core;
using HUSEscape.Quests;
using HUSEscape.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class TrainingOfficeProbe : MonoBehaviour
{
    public string reportPath,fixture;
    public bool Finished {get;private set;}
    public bool Passed=>report.passed;
    private readonly CampusReport report=new CampusReport();
    private readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
    private JourneySceneEntry entry;private JourneySession session;private Keyboard keyboard;
    private InputSettings settings,oldSettings;
    private void Start(){Debug.Log("Office probe start "+GetEntityId());DontDestroyOnLoad(gameObject);Time.captureDeltaTime=1f/60f;report.unity=Application.unityVersion;Application.logMessageReceived+=Log;stack.Push(Run());}
    private void Update(){if(Finished)return;try{while(stack.Count>0){var e=stack.Peek();if(!e.MoveNext()){stack.Pop();continue;}if(e.Current is IEnumerator child){stack.Push(child);continue;}return;}Finish(null);}catch(Exception e){Finish(e);}}
    private IEnumerator Run()
    {
        yield return Frames(25);entry=FindObjectsByType<JourneySceneEntry>().Single();session=entry.Session;
        string store=Path.Combine(Path.GetDirectoryName(reportPath),"Saves");Directory.CreateDirectory(store);
        foreach(string name in new[]{"floor01-v1.json","campus-v1.json"})File.Copy(Path.Combine(fixture,name),Path.Combine(store,name));
        session.StorageDirectoryOverride=store;
        string floor=File.ReadAllText(session.FloorSavePath),campus=File.ReadAllText(session.CampusSavePath);
        oldSettings=Instantiate(InputSystem.settings);oldSettings.hideFlags=HideFlags.HideAndDontSave;settings=Instantiate(oldSettings);settings.hideFlags=HideFlags.HideAndDontSave;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        InputSystem.settings=settings;keyboard=InputSystem.AddDevice<Keyboard>();
        Place(new Vector3(16,1.08f,51.2f));yield return Frames(10);yield return Press(Key.E);
        Check("Incomplete Campus rejects real E with guidance",!session.IsLoading&&entry.ui.Mode==QuestModal.Information&&entry.quests.CampusIndex==0);yield return Press(Key.Escape);
        Check("Existing Campus V1 fixture remains compatible",session.TryContinue(true));yield return Frames(20);
        Check("Campus restores 17 floor goals and three discoveries",entry.quests.IsComplete&&entry.quests.CampusIndex==3&&entry.inventory.ItemIds.Count==3);
        Place(new Vector3(16,1.08f,51.2f));yield return Frames(30);Check("Walking near door does not auto-load",session.IsCampus&&!session.IsLoading);
        var destination=typeof(JourneySession).GetField("officePath",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);string correct=(string)destination.GetValue(session);destination.SetValue(session,"Assets/_Project/Scenes/MissingOffice.unity");
        Check("Missing destination rejected",!session.TryEnterOffice());yield return Frames(10);
        Check("Failed startup releases loading and input gate",session.IsCampus&&!session.IsLoading&&!entry.Gate.IsBlocked&&!session.Loader.IsLoading);destination.SetValue(session,correct);
        var owner=FindObjectsByType<GameBootstrap>().Single(b=>b.IsInitialized);var loader=owner.Loader;
        yield return Press(Key.E);Check("Duplicate Office request rejected",!session.TryEnterOffice());yield return Transition(JourneyArea.TrainingOffice);
        Check("Production Single preserves same Core owner and loader",owner!=null&&ReferenceEquals(owner.Loader,loader)&&!loader.IsLoading&&owner.Manager.CurrentState==GameState.Playing);
        Check("Office spawn is grounded and retains old progress",Vector3.Distance(entry.player.transform.position,new Vector3(0,1.08f,3.7f))<.2f&&entry.player.isGrounded&&entry.quests.IsComplete&&entry.inventory.ItemIds.Count==3&&entry.quests.OfficeIndex==0);
        Check("Travel never creates Office save",!session.HasOfficeSave);
        Unique();
        yield return Press(Key.F5);Check("Office uses existing F5 progress menu",entry.ui.Mode==QuestModal.Progress);yield return Press(Key.Escape);
        Check("Journal appends three objectives after existing 17 and Campus three",entry.quests.Definitions.Count==17&&entry.quests.JournalDefinitions.Count==23);
        Place(new Vector3(4,1.08f,16));yield return Frames(12);yield return Press(Key.E);
        Check("Computer rejects missing information",entry.ui.Mode==QuestModal.Information&&!entry.quests.HasObserved("office:computer"));yield return Press(Key.Escape);
        Place(new Vector3(-6.8f,1.08f,7));yield return Frames(12);yield return Press(Key.E);
        Check("E reads notice using existing modal and QuestManager",entry.ui.Mode==QuestModal.Information&&entry.quests.OfficeIndex==1);
        Vector3 at=entry.player.transform.position;yield return Press(Key.W,Key.F,Key.J,Key.Tab);
        Check("Information modal blocks gameplay and other UI",entry.Gate.IsBlocked&&Vector3.Distance(at,entry.player.transform.position)<.01f&&!entry.player.GetComponent<InventoryUI>().IsOpen);yield return Press(Key.Escape);
        Place(new Vector3(-3,1.08f,10));yield return Frames(12);yield return Press(Key.F);
        Check("F reads administrative document",entry.quests.OfficeIndex==2&&entry.ui.Mode==QuestModal.Information);yield return Press(Key.Escape);
        Check("Manual Office save succeeds",session.TrySave());string partial=File.ReadAllText(session.OfficeSavePath);
        Check("Office has explicit V1 slot and refuses unconfirmed overwrite",JsonUtility.FromJson<JourneyCheckpoint>(partial).version==1&&!session.TrySave());
        Place(new Vector3(0,1.08f,5));yield return Frames(10);
        Check("Continue partial Office accepted",session.TryContinue(JourneyArea.TrainingOffice));yield return Frames(15);
        Check("Partial restore retains facts and pose",entry.quests.OfficeIndex==2&&Vector3.Distance(entry.player.transform.position,new Vector3(-3,1.08f,10))<.2f);
        Place(new Vector3(4,1.08f,16));yield return Frames(12);yield return Press(Key.E);
        Check("E confirms route only after both discoveries",entry.quests.OfficeIndex==3&&entry.quests.CurrentObjective.Contains("cổng chính"));yield return Press(Key.Escape);
        Check("Confirmed Office save succeeds",session.TrySave(true));string completed=File.ReadAllText(session.OfficeSavePath);
        Place(new Vector3(0,1.08f,1.5f));yield return Frames(12);yield return Press(Key.E);yield return Transition(JourneyArea.Campus);
        Check("Return reaches existing Campus near office",Vector3.Distance(entry.player.transform.position,new Vector3(16,1.08f,51))<.2f);
        Check("Completed Office goal remains visible after return",entry.quests.CurrentObjective.Contains("cổng chính")&&entry.quests.JournalDefinitions.Count==23);
        Check("Continue Office from Campus accepted",session.TryContinue(JourneyArea.TrainingOffice));yield return Transition(JourneyArea.TrainingOffice);Unique();
        Check("Cross-scene Office Continue restores facts and keys",entry.quests.OfficeIndex==3&&entry.quests.IsComplete&&entry.inventory.ItemIds.Count==3);
        Check("Continue Floor V1 from Office accepted",session.TryContinue(false));yield return Transition(JourneyArea.Floor01);
        Check("Floor V1 still restores completed progress",entry.quests.CurrentIndex==17&&entry.inventory.ItemIds.Count==3);
        Check("Continue Campus from Floor accepted",session.TryContinue(true));yield return Transition(JourneyArea.Campus);
        File.WriteAllText(session.OfficeSavePath,"{bad json");Check("Corrupt Office slot rejected without travel",!session.TryContinue(JourneyArea.TrainingOffice)&&session.IsCampus&&!session.IsLoading);
        var invalid=JsonUtility.FromJson<JourneyCheckpoint>(completed);invalid.version=99;File.WriteAllText(session.OfficeSavePath,JsonUtility.ToJson(invalid));Check("Unsupported Office version rejected",!session.TryContinue(JourneyArea.TrainingOffice));File.WriteAllText(session.OfficeSavePath,completed);
        Check("Floor and Campus files unchanged by Office operations",File.ReadAllText(session.FloorSavePath)==floor&&File.ReadAllText(session.CampusSavePath)==campus);
        Check("No production runtime errors",report.errors.Count==0);
    }
    private IEnumerator Transition(JourneyArea area){float until=Time.realtimeSinceStartup+25;while(session.IsLoading||session.Area!=area){if(Time.realtimeSinceStartup>until)throw new Exception("Transition timeout: "+session.LastMessage);yield return null;}yield return Frames(25);entry=FindObjectsByType<JourneySceneEntry>().Single();}
    private void Unique(){Check("One Player Camera QuestManager Inventory",FindObjectsByType<PlayerInventory>().Length==1&&FindObjectsByType<QuestManager>().Length==1&&FindObjectsByType<Camera>().Length==1);}
    private void Place(Vector3 p){entry.player.enabled=false;entry.player.transform.SetPositionAndRotation(p,Quaternion.identity);entry.player.enabled=true;Physics.SyncTransforms();}
    private IEnumerator Press(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(5);}
    private IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    private void Check(string name,bool value){report.checks.Add(new CampusCheck{name=name,passed=value});if(!value)throw new Exception(name+"; "+session.LastMessage);Debug.Log("Office PASS: "+name);}
    private void Log(string m,string s,LogType t){if(t!=LogType.Error&&t!=LogType.Exception&&t!=LogType.Assert)return;if(s.Contains("UnityEditor.Search.SearchDatabase"))report.editorStartupErrors.Add(m);else report.errors.Add(m+"\n"+s);}
    private void Finish(Exception e){Application.logMessageReceived-=Log;if(e!=null)report.errors.Add(e.ToString());if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(oldSettings!=null)InputSystem.settings=oldSettings;if(settings!=null)Destroy(settings);report.passed=e==null&&report.errors.Count==0;File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));Finished=true;}
}
