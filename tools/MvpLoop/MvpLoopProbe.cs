#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HUSEscape.Core;
using HUSEscape.Player;
using HUSEscape.Persistence;
using HUSEscape.Quests;
using HUSEscape.World;
using HUSEscape.AI;
using HUSEscape.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MvpLoopProbe : MonoBehaviour
{
    const string Floor="Assets/_Project/Scenes/Floor01_Prototype.unity", Campus="Assets/_Project/Scenes/Campus_Prototype.unity";
    public bool Finished{get;private set;} public bool Passed{get;private set;}
    readonly Stack<IEnumerator> work=new Stack<IEnumerator>();
    readonly List<Func<IEnumerator>> tests=new List<Func<IEnumerator>>();
    readonly List<string> rows=new List<string>(),errors=new List<string>(),editorErrors=new List<string>();
    JourneySceneEntry entry;JourneySession session;GameBootstrap core;Keyboard keyboard;string storage,active;
    int next,failures,asserts;float deadline;bool expectedFailure;
    void Awake()
    {
        DontDestroyOnLoad(gameObject);Time.captureDeltaTime=1f/60f;
        var settings=Instantiate(InputSystem.settings);settings.hideFlags=HideFlags.HideAndDontSave;
        settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=settings;keyboard=InputSystem.AddDevice<Keyboard>();
        storage=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"mvp-test-saves");Directory.CreateDirectory(storage);
        Application.logMessageReceived+=Log;
        tests.Add(GateRequiresProgressAndProximity);tests.Add(GuardActuallyCatchesAndPausesOnce);
        tests.Add(CaptureRequiresHumanSightAndRange);tests.Add(RetryPreservesProgressAndSaves);
        tests.Add(RetryRejectionKeepsCaught);tests.Add(RetryFailureKeepsCaught);
        tests.Add(MenuRejectionKeepsSession);tests.Add(MenuFailureKeepsCaught);tests.Add(MenuFailureKeepsOpenModal);
        tests.Add(MenuDestroysSessionAndRestartsFresh);tests.Add(NativeBootstrapToWinMenuAndFreshStart);
        tests.Add(VictoryNewGameDoesNotDeleteSave);tests.Add(WinRefusesFurtherGameplay);
        tests.Add(MenuFailureAfterSourceUnloadCanRecover);tests.Add(EnterRetriesWithoutLeakingInput);tests.Add(MenuInvalidDestinationKeepsControls);
    }
    void Log(string message,string trace,LogType type)
    {
        if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
        if(expectedFailure&&message.StartsWith("Unity completed loading without activating the requested scene: Assets/_Project/Scenes/"))return;
        if(trace.Contains("UnityEditor.Search.SearchDatabase")&&trace.Contains("IndexationOnStartup"))editorErrors.Add(message+"\n"+trace);
        else errors.Add(active+": "+message+"\n"+trace);
    }
    void Update()
    {
        if(Finished)return;
        if(work.Count==0)
        {
            if(next==tests.Count){Finish();return;}
            var test=tests[next++];active=test.Method.Name;deadline=Time.realtimeSinceStartup+90;work.Push(Run(test));
        }
        try
        {
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException(active);
            while(work.Count>0)
            {
                var it=work.Peek();if(!it.MoveNext()){work.Pop();continue;}
                if(it.Current is IEnumerator nested){work.Push(nested);continue;}return;
            }
            rows.Add("PASS "+active);
        }
        catch(Exception e){failures++;rows.Add("FAIL "+active+": "+e);work.Clear();}
    }
    IEnumerator Run(Func<IEnumerator> test){yield return Setup();yield return test();}
    IEnumerator Reset()
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        foreach(var s in FindObjectsByType<JourneySession>())Destroy(s.gameObject);
        foreach(var b in FindObjectsByType<GameBootstrap>())Destroy(b.gameObject);yield return Frames(3);
        var host=SceneManager.GetSceneByName("MvpTestHost");if(!host.IsValid())host=SceneManager.CreateScene("MvpTestHost");SceneManager.SetActiveScene(host);
        for(int i=SceneManager.sceneCount-1;i>=0;i--){var scene=SceneManager.GetSceneAt(i);if(scene.path.EndsWith(".unity"))yield return Await(SceneManager.UnloadSceneAsync(scene));}
        Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    IEnumerator Setup()
    {
        yield return Reset();foreach(var file in Directory.GetFiles(storage))File.Delete(file);
        yield return Await(SceneManager.LoadSceneAsync(Floor,LoadSceneMode.Additive));yield return Frames(20);Resolve();
        session.StorageDirectoryOverride=storage;entry.floorSave.StorageDirectoryOverride=storage;
        entry.floorSave.RestoreForTravel(CompleteFloor());yield return Frames(4);
        Check(session.TryLeaveFloor(),"Campus fixture travel rejected: "+session.LastMessage);yield return Loaded(JourneyArea.Campus);
        foreach(var guard in entry.guards)guard.enabled=false;
    }
    void Resolve(){entry=FindObjectsByType<JourneySceneEntry>().Single();session=entry.Session;core=FindObjectsByType<GameBootstrap>().Single(b=>b.IsInitialized);}
    Floor01SaveData CompleteFloor()=>new Floor01SaveData{
        version=1,scene="Floor01_Prototype",savedUtc=DateTime.UtcNow.ToString("O"),questIds=entry.quests.Definitions.Select(q=>q.Id).ToArray(),
        facts=entry.quests.Definitions.Select(q=>q.CompletionEvent).ToArray(),inventory=new[]{"Key_A101","Key_A102","Key_Lab"},
        doors=new[]{"Key_A101","Key_A102","Key_Lab"}.Select(k=>new SavedDoor{keyId=k,open=true}).ToArray(),
        cabinetUnlocked=true,sequenceSolved=true,lab=new SavedLab{power=true,instructions=true,connected=3,data=true,complete=true},playerPosition=entry.spawn.position,playerYaw=0};
    IEnumerator Loaded(JourneyArea area)
    {
        while(session.IsLoading||core.Loader.IsLoading)yield return null;yield return Frames(14);Resolve();
        Check(session.Area==area&&!session.RecoveryRequired,session.LastMessage);
        Check(!entry.Gate.IsBlocked&&Time.timeScale==1&&core.Manager.CurrentState==GameState.Playing,"Loaded area did not resume");
    }
    void Place(Vector3 pos)
    {
        entry.player.enabled=false;entry.player.transform.SetPositionAndRotation(pos,Quaternion.identity);entry.player.enabled=true;Physics.SyncTransforms();
    }
    IEnumerator Catch()
    {
        var guard=entry.guards.Single(g=>!g.IsDog);Place(new Vector3(0,1.08f,14));
        guard.GetComponent<CharacterController>().enabled=false;guard.transform.SetPositionAndRotation(new Vector3(0,1.08f,12.3f),Quaternion.identity);
        guard.GetComponent<CharacterController>().enabled=true;guard.enabled=true;Physics.SyncTransforms();
        for(int i=0;i<150&&session.Outcome==JourneyOutcome.Playing;i++)yield return null;
        Check(session.Outcome==JourneyOutcome.Caught,"Natural guard Alert did not catch player");
        Check(Time.timeScale==0&&entry.Gate.IsBlocked&&Cursor.visible&&core.Manager.CurrentState==GameState.Paused,"Catch did not pause controls/Core/cursor");
    }
    IEnumerator GateRequiresProgressAndProximity()
    {
        var gate=FindObjectsByType<MainGateExit>().Single();Place(gate.transform.position);yield return Frames(4);
        Check(!gate.TryUse()&&session.Outcome==JourneyOutcome.Playing,"Premature gate win");
        Check(entry.quests.OfficeIndex==0,"Gate changed Office facts");
        Place(entry.spawn.position);Check(!gate.TryUse(),"Distant gate accepted");
    }
    IEnumerator GuardActuallyCatchesAndPausesOnce()
    {
        yield return Catch();var guard=entry.guards.Single(g=>!g.IsDog);
        Check(!guard.TryCapture(),"Catch repeated");var pos=entry.player.transform.position;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Space));yield return Frames(8);InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        Check(entry.player.transform.position==pos,"Caught player still moves");
    }
    IEnumerator CaptureRequiresHumanSightAndRange()
    {
        var guard=entry.guards.Single(g=>!g.IsDog);guard.enabled=true;
        typeof(PatrolGuard).GetMethod("SetState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guard,new object[]{GuardState.Alert});
        Place(new Vector3(-20,1.08f,8));Check(!guard.TryCapture(),"Distant guard catches");
        var dog=entry.guards.Single(g=>g.IsDog);dog.enabled=true;
        typeof(PatrolGuard).GetMethod("SetState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dog,new object[]{GuardState.Alert});
        Check(!dog.TryCapture(),"Dog directly catches instead of alarm");
        Place(guard.transform.position+Vector3.forward*1.7f);guard.transform.rotation=Quaternion.identity;
        var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=guard.transform.position+new Vector3(0,.3f,.85f);blocker.transform.localScale=new Vector3(3,3,.2f);Physics.SyncTransforms();
        Check(!guard.TryCapture()&&session.Outcome==JourneyOutcome.Playing,"Guard catches through cover");Destroy(blocker);yield return Frames(2);
    }
    Dictionary<string,string> SeedSaves()
    {
        var values=new Dictionary<string,string>{{"floor01-v1.json","floor bytes unchanged"},{"campus-v1.json","campus bytes unchanged"},{"trainingoffice-v1.json","office bytes unchanged"}};
        foreach(var pair in values)File.WriteAllText(Path.Combine(storage,pair.Key),pair.Value);return values;
    }
    void SavesUnchanged(Dictionary<string,string> values){foreach(var pair in values)Check(File.ReadAllText(Path.Combine(storage,pair.Key))==pair.Value,"Changed save "+pair.Key);}
    IEnumerator RetryPreservesProgressAndSaves()
    {
        entry.quests.Signal("campus:courtyard");var saves=SeedSaves();var old=session;
        yield return Catch();Check(session.TryRetry()&&!session.TryRetry(),"Retry duplicate accepted");yield return Loaded(JourneyArea.Campus);
        Check(session==old&&session.Outcome==JourneyOutcome.Playing&&entry.quests.CampusIndex==1&&entry.inventory.ItemIds.Count==3,"Retry lost progress or duplicated session");
        Check(Vector3.Distance(entry.player.transform.position,entry.spawn.position)<.2f&&entry.guards.All(g=>g.State==GuardState.Patrol),"Retry did not reset spawn/AI");SavesUnchanged(saves);
    }
    IEnumerator RetryRejectionKeepsCaught()
    {
        yield return Catch();var backend=TransactionBackendControl.Install(core.Loader);TransactionBackendControl.Reject(backend,true);
        Check(!session.TryRetry()&&session.Outcome==JourneyOutcome.Caught&&!session.IsLoading,"Rejected retry lost result");yield return Frames(4);
        Check(entry.Gate.IsBlocked&&Time.timeScale==0,"Rejected retry unlocked controls");
    }
    void Fail(object backend){expectedFailure=true;try{TransactionBackendControl.Complete(backend,false);}finally{expectedFailure=false;}}
    IEnumerator RetryFailureKeepsCaught()
    {
        yield return Catch();var backend=TransactionBackendControl.Install(core.Loader);Check(session.TryRetry(),"Retry not accepted");Fail(backend);yield return Frames(4);
        Check(session.Outcome==JourneyOutcome.Caught&&!session.RecoveryRequired&&entry.Gate.IsBlocked&&Time.timeScale==0,"Failed retry lost caught state");
    }
    IEnumerator MenuRejectionKeepsSession()
    {
        yield return Catch();var backend=TransactionBackendControl.Install(core.Loader);TransactionBackendControl.Reject(backend,true);
        Check(!session.TryReturnToMenu(),"Invalid menu accepted");yield return Frames(4);
        Check(session!=null&&session.Outcome==JourneyOutcome.Caught&&Time.timeScale==0&&entry.Gate.IsBlocked,"Menu rejection cleared session");
    }
    IEnumerator MenuFailureKeepsCaught()
    {
        yield return Catch();var backend=TransactionBackendControl.Install(core.Loader);Check(session.TryReturnToMenu()&&!session.TryReturnToMenu(),"Menu request duplicate accepted");Fail(backend);yield return Frames(4);
        Check(session.Outcome==JourneyOutcome.Caught&&!session.IsLoading&&!session.RecoveryRequired&&Time.timeScale==0&&entry.Gate.IsBlocked,"Menu failure lost source/result");
    }
    IEnumerator MenuFailureKeepsOpenModal()
    {
        yield return Press(Key.F5);var backend=TransactionBackendControl.Install(core.Loader);Check(session.TryReturnToMenu(),"Menu from modal rejected");Fail(backend);yield return Frames(4);
        Check(entry.ui.Mode==QuestModal.Progress&&Time.timeScale==0,"Menu failure closed existing modal");
        entry.ui.CloseModal();yield return Frames(4);Check(!entry.Gate.IsBlocked&&Time.timeScale==1,"Modal failure cleanup stuck");
    }
    IEnumerator MenuDestroysSessionAndRestartsFresh()
    {
        entry.quests.Signal("campus:courtyard");var saves=SeedSaves();var old=session;var owner=core;
        yield return Press(Key.F5);Check(session.TryReturnToMenu(),"Menu from normal play rejected");
        yield return Menu();Check(old==null&&FindObjectsByType<JourneySession>().Length==0,"Journey owner survived menu");
        Check(core==owner&&FindObjectsByType<GameBootstrap>().Length==1,"Menu duplicated Core");yield return StartFromMenu();
        Check(session!=old&&entry.quests.CurrentIndex==0&&entry.quests.CampusIndex==0&&entry.quests.OfficeIndex==0&&entry.inventory.ItemIds.Count==0,"Menu Start inherited old quests/inventory");SavesUnchanged(saves);
    }
    IEnumerator Menu()
    {
        while(core.Loader.IsLoading)yield return null;yield return Frames(10);
        Check(SceneManager.GetActiveScene().path==JourneySession.MenuPath&&core.Manager.CurrentState==GameState.Ready,"Menu not Ready");
        Check(Time.timeScale==1&&Cursor.visible&&Cursor.lockState==CursorLockMode.None,"Menu cursor/clock stuck");
    }
    IEnumerator StartFromMenu()
    {
        var view=FindObjectsByType<MainMenuView>().Single();for(int i=0;i<30&&!view.CanStart;i++)yield return null;
        Check(view.CanStart,"Menu Start not bound");var button=view.GetComponentInChildren<Button>(true);button.onClick.Invoke();button.onClick.Invoke();button.onClick.Invoke();
        while(core.Loader.IsLoading)yield return null;yield return Frames(20);Resolve();session.StorageDirectoryOverride=storage;entry.floorSave.StorageDirectoryOverride=storage;
        Check(session.Area==JourneyArea.Floor01&&FindObjectsByType<JourneySession>().Length==1&&FindObjectsByType<GameBootstrap>().Length==1,"Repeated Start duplicated owners");
    }
    IEnumerator CompleteOfficeAndWin()
    {
        foreach(var fact in new[]{"campus:courtyard","campus:map","campus:direction"})entry.quests.Signal(fact);
        Check(session.TryEnterOffice(),session.LastMessage);yield return Loaded(JourneyArea.TrainingOffice);
        // Drive the actual E/F interaction points and information modals, including computer prerequisites.
        foreach(var fact in new[]{"office:notice","office:document","office:computer"})
        {
            var point=FindObjectsByType<OfficeDiscovery>().Single(d=>d.fact==fact);Place(point.transform.position+Vector3.back*.6f);yield return Frames(4);
            yield return Press(point.useF?Key.F:Key.E);Check(entry.quests.HasObserved(fact),"Interaction did not record "+fact);
            yield return Press(Key.Escape);yield return Frames(4);
        }
        Check(session.TryReturnCampus(),session.LastMessage);yield return Loaded(JourneyArea.Campus);
        foreach(var g in entry.guards)g.enabled=false;
        var gate=FindObjectsByType<MainGateExit>().Single();Place(gate.transform.position);yield return Frames(4);yield return Press(Key.E);
        Check(session.Outcome==JourneyOutcome.Won&&Time.timeScale==0&&entry.Gate.IsBlocked&&Cursor.visible,"Gate interaction did not show victory");
    }
    IEnumerator NativeBootstrapToWinMenuAndFreshStart()
    {
        yield return Reset();yield return Await(SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity",LoadSceneMode.Single));yield return Frames(30);
        core=FindObjectsByType<GameBootstrap>().Single(b=>b.IsInitialized);while(core.Loader.IsLoading)yield return null;yield return StartFromMenu();
        entry.floorSave.RestoreForTravel(CompleteFloor());yield return Frames(4);Check(session.TryLeaveFloor(),session.LastMessage);yield return Loaded(JourneyArea.Campus);
        foreach(var g in entry.guards)g.enabled=false;yield return CompleteOfficeAndWin();
        var old=session;Check(session.TryReturnToMenu(),session.LastMessage);yield return Menu();Check(old==null,"Won session survived menu");yield return StartFromMenu();
        Check(session.Outcome==JourneyOutcome.Playing&&entry.quests.CurrentIndex==0&&entry.quests.OfficeIndex==0&&entry.inventory.ItemIds.Count==0,"Victory menu restart not fresh");
    }
    IEnumerator VictoryNewGameDoesNotDeleteSave()
    {
        var saves=SeedSaves();yield return CompleteOfficeAndWin();var old=session;
        Check(session.TryNewGame(true)&&!session.TryNewGame(true),"New Game accepted twice");yield return Loaded(JourneyArea.Floor01);
        Check(session==old&&session.Outcome==JourneyOutcome.Playing&&entry.quests.CurrentIndex==0&&entry.quests.CampusIndex==0&&entry.quests.OfficeIndex==0&&entry.inventory.ItemIds.Count==0,"Win New Game did not clear session");SavesUnchanged(saves);
    }
    IEnumerator WinRefusesFurtherGameplay()
    {
        yield return CompleteOfficeAndWin();var gate=FindObjectsByType<MainGateExit>().Single();
        Check(!gate.TryUse()&&!session.TryEnterOffice()&&!session.TrySave(true)&&!session.TryContinue(JourneyArea.Campus)&&!session.TryRetry(),"Terminal outcome accepted gameplay mutation");
        Check(session.Outcome==JourneyOutcome.Won,"Win changed on repeated input");
    }
    IEnumerator MenuFailureAfterSourceUnloadCanRecover()
    {
        var backend=TransactionBackendControl.Install(core.Loader);Check(session.TryReturnToMenu(),"Menu recovery fixture");
        SceneManager.SetActiveScene(SceneManager.CreateScene("MvpLostSourceHost"));
        yield return Await(SceneManager.UnloadSceneAsync(entry.gameObject.scene));Fail(backend);yield return Frames(4);
        Check(session.RecoveryRequired&&Time.timeScale==1&&Cursor.visible,"Lost source menu failure pretended rollback");
        TransactionBackendControl.UseNative(core.Loader);Check(session.TryReturnToMenu(),"Recovery menu retry refused");yield return Menu();
        Check(FindObjectsByType<JourneySession>().Length==0,"Recovery menu retained session");
    }
    IEnumerator EnterRetriesWithoutLeakingInput()
    {
        yield return Catch();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        yield return Loaded(JourneyArea.Campus);Check(session.Outcome==JourneyOutcome.Playing,"Enter did not retry");
    }
    IEnumerator MenuInvalidDestinationKeepsControls()
    {
        var backend=TransactionBackendControl.Install(core.Loader);Check(session.TryReturnToMenu(),"Invalid view fixture rejected");
        TransactionBackendControl.Complete(backend,true);yield return Frames(6);
        Check(!session.IsLoading&&!session.RecoveryRequired&&session.Outcome==JourneyOutcome.Playing,"Invalid menu lost surviving session");
        Check(core.Manager.CurrentState==GameState.Playing&&!entry.Gate.IsBlocked&&Time.timeScale==1,"Invalid menu view left source in Ready/input blocked");
    }
    IEnumerator Press(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(2);}
    static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    static IEnumerator Await(AsyncOperation op){if(op==null)throw new Exception("No scene operation");while(!op.isDone)yield return null;}
    void Check(bool value,string message){asserts++;if(!value)throw new Exception(message);}
    void Finish()
    {
        Finished=true;Passed=failures==0&&errors.Count==0;
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"mvp-loop-results.txt"),
            $"{(Passed?"PASS":"FAIL")} {tests.Count-failures}/{tests.Count}; {asserts} assertions\n"+string.Join("\n",rows)+"\nRUNTIME ERRORS\n"+string.Join("\n",errors)+"\nKNOWN EDITOR ERRORS\n"+string.Join("\n",editorErrors));
        Application.logMessageReceived-=Log;
    }
}
#endif
