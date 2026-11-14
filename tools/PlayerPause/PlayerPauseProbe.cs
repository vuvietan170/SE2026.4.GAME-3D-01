#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HUSEscape.Core;
using HUSEscape.Player;
using HUSEscape.World;
using HUSEscape.Quests;
using HUSEscape.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public sealed class PlayerPauseProbe : MonoBehaviour
{
    const string Floor="Assets/_Project/Scenes/Floor01_Prototype.unity";
    public bool Finished {get;private set;}
    public bool Passed {get;private set;}
    readonly Stack<IEnumerator> work=new Stack<IEnumerator>();
    readonly List<Func<IEnumerator>> cases=new List<Func<IEnumerator>>();
    readonly List<string> results=new List<string>(),errors=new List<string>(),editorErrors=new List<string>();
    JourneySceneEntry entry; GameBootstrap core; GameplayInputGate gate; ThirdPersonCameraFollow follow;
    Keyboard keyboard; Mouse mouse; Gamepad gamepad;
    int next,failed; float deadline; string active; bool expectedDuplicate;
    void Awake()
    {
        DontDestroyOnLoad(gameObject);Time.captureDeltaTime=1f/60f;
        var settings=Instantiate(InputSystem.settings); settings.hideFlags=HideFlags.HideAndDontSave;
        settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=settings; keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();gamepad=InputSystem.AddDevice<Gamepad>();
        Application.logMessageReceived+=Log;
        cases.Add(InventoryPausesCoreAndInput);cases.Add(CrouchUsesExistingAction);cases.Add(NestedOwnersKeepPause);
        cases.Add(GatePauseMatchesGameManager);cases.Add(CameraEnableCannotBypassGate);cases.Add(DisableGateClosesUi);
        cases.Add(ControllerFamilyHasSingleOwner);
        cases.Add(SaveAndJournalRoundTrip);cases.Add(NestedModalCancelClosesTopOnly);cases.Add(DisableUiReleasesPause);
        cases.Add(GamepadCancelDoesNotCrouch);cases.Add(ExternalPauseIsPreserved);cases.Add(RestoreClosedModalSnapshot);
        cases.Add(UnloadModalRestoresEnvironment);cases.Add(CameraRelativeMovementAndSprint);cases.Add(JumpAndStance);
        cases.Add(ActionAssetsAreIsolated);cases.Add(GateDisableEnableRestoresControls);cases.Add(PausedCameraAndControllerStayStill);cases.Add(NativeTravelWithOpenModal);
    }
    void Log(string message,string trace,LogType type)
    {
        if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
        if(expectedDuplicate && type==LogType.Error && message.Contains("already"))return;
        if(trace.Contains("UnityEditor.Search.SearchDatabase")&&trace.Contains("IndexationOnStartup"))editorErrors.Add(message+"\n"+trace);
        else errors.Add(active+": "+message+"\n"+trace);
    }
    void Update()
    {
        if(Finished)return;
        if(work.Count==0)
        {
            if(next==cases.Count){Finish();return;}
            var test=cases[next++];active=test.Method.Name;deadline=Time.realtimeSinceStartup+60;work.Push(Run(test));
        }
        try
        {
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException(active);
            while(work.Count>0)
            {
                var item=work.Peek();if(!item.MoveNext()){work.Pop();continue;}
                if(item.Current is IEnumerator nested){work.Push(nested);continue;}return;
            }
            results.Add("PASS "+active);
        }
        catch(Exception e){failed++;results.Add("FAIL "+active+": "+e);work.Clear();}
    }
    IEnumerator Run(Func<IEnumerator> test){yield return Setup();yield return test();}
    IEnumerator Setup()
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(gamepad,new GamepadState());
        foreach(var s in FindObjectsByType<JourneySession>())Destroy(s.gameObject);
        foreach(var b in FindObjectsByType<GameBootstrap>())Destroy(b.gameObject);
        yield return Frames(2);
        var host=SceneManager.GetSceneByName("PlayerPauseHost");if(!host.IsValid())host=SceneManager.CreateScene("PlayerPauseHost");SceneManager.SetActiveScene(host);
        for(int i=SceneManager.sceneCount-1;i>=0;i--){var scene=SceneManager.GetSceneAt(i);if(scene.path.EndsWith(".unity"))yield return Await(SceneManager.UnloadSceneAsync(scene));}
        Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        yield return Await(SceneManager.LoadSceneAsync(Floor,LoadSceneMode.Additive));yield return Frames(20);
        entry=FindObjectsByType<JourneySceneEntry>().Single();gate=entry.Gate;follow=entry.viewCamera.GetComponent<ThirdPersonCameraFollow>();core=FindObjectsByType<GameBootstrap>().Single(b=>b.IsInitialized);
        Check(entry.Session!=null,"Fixture not bound");
    }
    IEnumerator InventoryPausesCoreAndInput()
    {
        yield return Press(Key.Tab);
        Check(entry.player.GetComponent<InventoryUI>().IsOpen,"Inventory did not open");
        Check(gate.IsBlocked && Time.timeScale==0 && core.Manager.CurrentState==GameState.Paused,"Inventory leaves gameplay/Core unpaused");
    }
    IEnumerator CrouchUsesExistingAction()
    {
        yield return Press(Key.C);
        Check(entry.player.GetComponentInChildren<Animator>().GetBool("IsCrouching"),"Player/Crouch action C is ignored");
    }
    IEnumerator NestedOwnersKeepPause()
    {
        var a=new object();var b=new object();Acquire(a);Acquire(b);Release(a);yield return Frames(8);
        Check(gate.IsBlocked && Time.timeScale==0,"Releasing one modal resumed another");Release(b);
    }
    IEnumerator GatePauseMatchesGameManager()
    {gate.Block();yield return Frames(2);Check(core.Manager.CurrentState==GameState.Paused,"Gate pauses time without Paused Core");gate.ReleaseAfterFrame();}
    IEnumerator CameraEnableCannotBypassGate()
    {
        gate.Block();follow.enabled=true;
        var action=(InputAction)Field(follow,"lookAction");
        Check(!action.enabled,"Camera OnEnable re-enabled Look behind pause");yield return Frames(2);
    }
    IEnumerator DisableGateClosesUi()
    {
        yield return Press(Key.Tab);Check(entry.player.GetComponent<InventoryUI>().IsOpen,"Inventory fixture closed");gate.enabled=false;yield return Frames(2);
        Check(!entry.player.GetComponent<InventoryUI>().IsOpen,"Disabled gate leaves UI open without a lease");
    }
    IEnumerator ControllerFamilyHasSingleOwner()
    {
        var clone=Instantiate(entry.player.gameObject);clone.SetActive(false);clone.name="DuplicateControllerFixture";
        var existing=clone.GetComponent<PlayerController1>();
        expectedDuplicate=true;var other=clone.AddComponent<PlayerController>();expectedDuplicate=false;
        if(other!=null){Set(other,"inputActions",Field(existing,"inputActions"));Set(other,"cameraTransform",entry.viewCamera.transform);}
        clone.transform.position=new Vector3(0,1.08f,60);clone.SetActive(true);yield return Frames(2);
        int count=clone.GetComponents<MonoBehaviour>().Count(c=>(c is PlayerController||c is PlayerController1)&&c.enabled);
        Destroy(clone);Check(count==1,"Two movement controllers own one CharacterController");
    }
    IEnumerator SaveAndJournalRoundTrip()
    {
        yield return Press(Key.F5);Check(entry.ui.Mode==QuestModal.Progress && Time.timeScale==0,"F5 save menu not paused");
        yield return Press(Key.Escape);yield return Frames(4);Check(entry.ui.Mode==QuestModal.None&&!gate.IsBlocked&&Time.timeScale==1,"Save close leaves pause/input");
        yield return Press(Key.J);Check(entry.ui.Mode==QuestModal.Journal&&Time.timeScale==0,"Journal not paused");
        yield return Press(Key.J);yield return Frames(4);Check(entry.ui.Mode==QuestModal.None&&!gate.IsBlocked,"Journal did not resume");
    }
    IEnumerator NestedModalCancelClosesTopOnly()
    {
        yield return Press(Key.J);Check(entry.ui.Mode==QuestModal.Journal,"Journal fixture");
        Check(entry.player.GetComponent<InventoryUI>().SetOpen(true),"Nested inventory fixture");
        yield return Press(Key.Escape);
        Check(!entry.player.GetComponent<InventoryUI>().IsOpen&&entry.ui.Mode==QuestModal.Journal&&Time.timeScale==0,"One Cancel closed both nested modals");
        yield return Press(Key.Escape);yield return Frames(4);Check(!gate.IsBlocked&&Time.timeScale==1,"Last nested modal did not resume");
    }
    IEnumerator DisableUiReleasesPause()
    {
        yield return Press(Key.F5);entry.ui.enabled=false;yield return Frames(4);
        Check(!gate.IsBlocked&&Time.timeScale==1&&entry.ui.Mode==QuestModal.None,"Disabled save UI leaked pause");
        entry.ui.enabled=true;yield return Press(Key.J);yield return Press(Key.Escape);yield return Frames(4);
        Check(!gate.IsBlocked&&Time.timeScale==1,"Reenabled UI leaked pause");
    }
    IEnumerator GamepadCancelDoesNotCrouch()
    {
        yield return Press(Key.Tab);
        InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.East));yield return Frames(8);
        Check(!entry.player.GetComponent<InventoryUI>().IsOpen,"Gamepad Cancel did not close");
        Check(!(bool)Field(entry.player.GetComponent<PlayerController1>(),"isCrouching"),"Closing UI crouched player");
        InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return Frames(4);
        Check(!gate.IsBlocked,"Closing UI left input blocked");
    }
    IEnumerator ExternalPauseIsPreserved()
    {
        Check(core.Manager.TryChangeState(GameState.Paused),"Cannot externally pause");yield return Frames(2);
        var ui=entry.player.GetComponent<InventoryUI>();Check(ui.SetOpen(true),"Cannot open over external pause");ui.SetOpen(false);yield return Frames(4);
        Check(core.Manager.CurrentState==GameState.Paused&&gate.IsBlocked&&Time.timeScale==0,"UI resumed someone else's pause");
        Check(core.Manager.TryChangeState(GameState.Playing),"External resume failed");yield return Frames(4);Check(!gate.IsBlocked&&Time.timeScale==1,"External resume stuck");
    }
    IEnumerator RestoreClosedModalSnapshot()
    {
        yield return Press(Key.F5);var snapshot=gate.CaptureSnapshot();entry.ui.CloseModal();gate.RestoreSnapshot(snapshot);yield return Frames(4);
        Check(!gate.IsBlocked&&Time.timeScale==1,"Restoring a closed modal leaked its lease");
    }
    IEnumerator UnloadModalRestoresEnvironment()
    {
        Time.timeScale=.5f;Cursor.lockState=CursorLockMode.Confined;Cursor.visible=false;
        var expectedLock=Cursor.lockState;var expectedVisible=Cursor.visible;
        yield return Press(Key.F5);Check(Time.timeScale==0,"Modal fixture");
        yield return Await(SceneManager.UnloadSceneAsync(entry.gameObject.scene));yield return Frames(4);
        Check(Time.timeScale==.5f&&Cursor.lockState==expectedLock&&Cursor.visible==expectedVisible,"Unload leaked clock/cursor");
        Check(core.Manager.CurrentState==GameState.Playing,"Unload leaked Core pause");
    }
    IEnumerator CameraRelativeMovementAndSprint()
    {
        follow.enabled=false;entry.viewCamera.transform.rotation=Quaternion.Euler(0,90,0);
        var start=entry.player.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return Frames(12);
        var walk=entry.player.transform.position-start;Check(walk.x>.1f&&Mathf.Abs(walk.z)<.1f,"Movement not camera relative");
        start=entry.player.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));yield return Frames(12);
        Check((entry.player.transform.position-start).x>walk.x*1.3f,"Sprint does not increase speed");
        Check(entry.player.GetComponentInChildren<Animator>().GetFloat("Speed")>.5f,"Movement animation not updated");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
    }
    IEnumerator JumpAndStance()
    {
        var cc=entry.player.GetComponent<CharacterController>();var height=cc.height;var center=cc.center;
        yield return Press(Key.C);Check(cc.height==height&&cc.center==center,"Stance changed collision capsule");
        yield return Press(Key.LeftCtrl);Check(!entry.player.GetComponentInChildren<Animator>().GetBool("IsCrouching"),"Ctrl alias did not restore standing");
        var y=entry.player.transform.position.y;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return Frames(6);
        Check(entry.player.transform.position.y>y+.1f,"Jump did not rise");InputSystem.QueueStateEvent(keyboard,new KeyboardState());
    }
    IEnumerator ActionAssetsAreIsolated()
    {
        var asset=(InputActionAsset)Field(gate,"inputActions");var clone=Instantiate(entry.player.gameObject);yield return Frames(4);
        var other=clone.GetComponent<GameplayInputGate>();gate.Block();
        Check(!gate.PlayerAction("Move").enabled&&other.PlayerAction("Move").enabled,"Actors share enabled Player actions");
        Check(!ReferenceEquals(gate.PlayerAction("Move"),asset.FindAction("Player/Move")),"Gate mutates serialized action asset");
        gate.enabled=false;Check(other.PlayerAction("Move").enabled,"Disabling one gate disabled another actor");Destroy(clone);
    }
    IEnumerator GateDisableEnableRestoresControls()
    {
        yield return Press(Key.Tab);gate.enabled=false;yield return Frames(3);gate.enabled=true;yield return Frames(4);
        Check(!gate.IsBlocked&&gate.PlayerAction("Move").enabled&&gate.PlayerAction("Look").enabled&&Time.timeScale==1,"Gate reenable stuck");
        var controller=entry.player.GetComponent<PlayerController1>();controller.enabled=false;controller.enabled=true;yield return Frames(4);
        yield return Press(Key.Tab);Check(entry.player.GetComponent<InventoryUI>().IsOpen,"Reenabled actor UI not functional");
        yield return Press(Key.Tab);yield return Frames(4);Check(!gate.IsBlocked,"Reenabled actor close stuck");
    }
    IEnumerator PausedCameraAndControllerStayStill()
    {
        yield return Press(Key.Tab);var pose=entry.player.transform.position;var camera=entry.viewCamera.transform;var position=camera.position;var rotation=camera.rotation;
        follow.enabled=false;follow.enabled=true;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Space,Key.C));
        InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(90,40));yield return Frames(8);
        Check(entry.player.transform.position==pose&&camera.position==position&&Quaternion.Angle(camera.rotation,rotation)<.01f,"Paused actor/camera moved");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
    }
    IEnumerator NativeTravelWithOpenModal()
    {
        var data=new Floor01SaveData {
            version=1,scene="Floor01_Prototype",savedUtc=DateTime.UtcNow.ToString("O"),
            questIds=entry.quests.Definitions.Select(q=>q.Id).ToArray(),facts=entry.quests.Definitions.Select(q=>q.CompletionEvent).ToArray(),
            inventory=new[]{"Key_A101","Key_A102","Key_Lab"},doors=new[]{"Key_A101","Key_A102","Key_Lab"}.Select(k=>new SavedDoor{keyId=k,open=true}).ToArray(),
            cabinetUnlocked=true,sequenceSolved=true,lab=new SavedLab{power=true,instructions=true,connected=3,data=true,complete=true},
            playerPosition=entry.spawn.position,playerYaw=entry.spawn.eulerAngles.y};
        entry.floorSave.RestoreForTravel(data);yield return Frames(4);var session=entry.Session;
        yield return Press(Key.F5);Check(Time.timeScale==0,"Source modal fixture");Check(session.TryLeaveFloor(),session.LastMessage);
        while(session.IsLoading)yield return null;yield return Frames(12);
        entry=FindObjectsByType<JourneySceneEntry>().Single();gate=entry.Gate;
        Check(session.Area==JourneyArea.Campus&&!gate.IsBlocked&&Time.timeScale==1&&core.Manager.CurrentState==GameState.Playing,"Modal travel left campus paused");
        Check(entry.player.enabled&&entry.viewCamera.GetComponent<ThirdPersonCameraFollow>().enabled&&gate.PlayerAction("Look").enabled,"Modal travel lost camera/controller");
        yield return Press(Key.F5);Check(session.TryNewGame(true),session.LastMessage);
        while(session.IsLoading)yield return null;yield return Frames(12);
        entry=FindObjectsByType<JourneySceneEntry>().Single();gate=entry.Gate;
        Check(session.Area==JourneyArea.Floor01&&!gate.IsBlocked&&Time.timeScale==1&&core.Manager.CurrentState==GameState.Playing,"New Game from modal left pause");
        Check(FindObjectsByType<JourneySession>().Length==1&&FindObjectsByType<GameBootstrap>().Length==1,"Modal travel duplicated services");
    }
    void Acquire(object owner)
    {var method=typeof(GameplayInputGate).GetMethod("Acquire");if(method==null)gate.Block();else method.Invoke(gate,new object[]{owner,true});}
    void Release(object owner)
    {var method=typeof(GameplayInputGate).GetMethod("Release");if(method==null)gate.ReleaseAfterFrame();else method.Invoke(gate,new object[]{owner});}
    IEnumerator Press(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(2);}
    static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    static IEnumerator Await(AsyncOperation op){if(op==null)throw new Exception("No scene operation");while(!op.isDone)yield return null;}
    static FieldInfo Info(object obj,string name){for(var t=obj.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(f!=null)return f;}throw new Exception(name);}
    static object Field(object obj,string name)=>Info(obj,name).GetValue(obj);
    static void Set(object obj,string name,object value)=>Info(obj,name).SetValue(obj,value);
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    void Finish()
    {
        Finished=true;Passed=failed==0&&errors.Count==0;
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"player-pause-results.txt"),
            $"{(Passed?"PASS":"FAIL")} {cases.Count-failed}/{cases.Count}\n"+string.Join("\n",results)+"\nRUNTIME ERRORS\n"+string.Join("\n",errors)+"\nEDITOR ERRORS\n"+string.Join("\n",editorErrors));
        Application.logMessageReceived-=Log;
    }
}
#endif
