using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HUSEscape.Inventory;
using HUSEscape.Quests;
using HUSEscape.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable] public sealed class QuestV2Check { public string name; public bool passed; }
[Serializable] public sealed class QuestV2RouteReport
{
    public string unityVersion; public bool passed; public int drivenFrames;
    public List<QuestV2Check> checks = new List<QuestV2Check>();
    public List<string> errors = new List<string>();
    public List<string> visited = new List<string>();
    public List<string> screenshots = new List<string>();
    public string visualCaptureStatus;
}

[DefaultExecutionOrder(-10000)]
public sealed class QuestV2PlayProbe : MonoBehaviour
{
    public string reportPath;public bool Finished{get;private set;}public bool Passed=>report.passed;
    private readonly QuestV2RouteReport report=new QuestV2RouteReport();private IEnumerator steps;
    private Transform player;private CharacterController capsule;private Component movement;private Camera camera;
    private PlayerInventory inventory;private DoorInteraction[] doors;private PickupItem[] keys;
    private Keyboard keyboard;private Mouse mouse;private InputActionAsset actions;private ReadOnlyArray<InputDevice>? originalDevices;
    private InputSettings originalSettings,settings;private HideFlags originalFlags;private float oldCapture;
    private DoorInteraction watched; private bool sawOpen; private float contactAtOpen, weightAtOpen; private int followFrames; private Quaternion closedRotation;
    private void LateUpdate()
    {
        if(watched==null)return;
        var ik=watched.GetComponent<DoorHandleIK>();bool open=(bool)Field(watched,"isOpen");
        if(open&&!sawOpen){sawOpen=true;contactAtOpen=ik.ContactError;weightAtOpen=ik.CurrentWeight;}
        float angle=Quaternion.Angle(closedRotation,watched.transform.localRotation);
        if(open&&angle>.2f&&angle<15&&ik.CurrentWeight>.3f&&ik.ContactError<.07f)followFrames++;
    }
    private void Start(){report.unityVersion=Application.unityVersion;Application.logMessageReceived+=OnLog;steps=Run();}
    private void Update(){if(Finished){return;}try{if(!steps.MoveNext()){Finish(null);}}catch(Exception e){Finish(e);}}
    private IEnumerator Run()
    {
        Check("Saved floor scene loaded",SceneManager.GetActiveScene().path=="Assets/_Project/Scenes/Floor01_Prototype.unity");
        capsule=UnityEngine.Object.FindObjectsByType<CharacterController>().Single();player=capsule.transform;
        inventory=player.GetComponent<PlayerInventory>();camera=UnityEngine.Object.FindObjectsByType<Camera>().Single();
        movement=player.GetComponents<MonoBehaviour>().Single(c=>c!=null&&c.GetType().Name=="PlayerController1");
        Check("Existing 2m CharacterController preserved",capsule.height==2f&&capsule.radius==.5f);
        Check("Existing PlayerController enabled",((Behaviour)movement).enabled);
        Check("Existing Animator and Avatar retained",player.GetComponentInChildren<Animator>().runtimeAnimatorController!=null&&player.GetComponentInChildren<Animator>().avatar!=null);
        Check("Movement references cloned scene camera",ReferenceEquals(Field(movement,"cameraTransform"),camera.transform));
        var follow=camera.GetComponents<MonoBehaviour>().Single(c=>c!=null&&c.GetType().Name=="ThirdPersonCameraFollow");
        Check("Camera follows cloned player without tuning changes",ReferenceEquals(Field(follow,"target"),player)&&(float)Field(follow,"distance")==6.3f);
        var ui=player.GetComponent<InventoryUI>();Check("Inventory UI binds same inventory",ReferenceEquals(Field(ui,"inventory"),inventory));
        var quests=UnityEngine.Object.FindObjectsByType<QuestManager>().Single();var puzzleUI=UnityEngine.Object.FindObjectsByType<QuestPuzzleUI>().Single();var gate=player.GetComponent<GameplayInputGate>();var cabinet=UnityEngine.Object.FindObjectsByType<CodeLockCabinet>().Single();
        Check("First quest objective is initialized",quests.Definitions.Count==12&&quests.CurrentIndex==0&&quests.GetState(0)==QuestState.InProgress&&quests.GetState(1)==QuestState.NotStarted&&quests.CurrentObjective.Contains("Key_A101"));
        Check("Inventory starts empty",inventory.ItemIds.Count==0);
        doors=UnityEngine.Object.FindObjectsByType<DoorInteraction>();keys=UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
        Check("Three existing door components and three pickups",doors.Length==3&&keys.Length==3);
        foreach(var pair in new[]{new[]{"Door_A101","Key_A101"},new[]{"Door_A102","Key_A102"},new[]{"Door_LAB","Key_Lab"}})
        {
            var door=doors.Single(d=>d.name==pair[0]);Check(pair[0]+" correct key and no consumption",(string)Field(door,"requiredKeyId")==pair[1]&&!(bool)Field(door,"consumeKey"));
            Check(pair[1]+" matching accessible pickup exists",keys.Any(k=>(string)Field(k,"itemId")==pair[1]));
            Check(pair[0]+" initially blocks its doorway",PortalObstacles(door).Any(c=>c.transform.IsChildOf(door.transform)));
        }
        var text=UnityEngine.Object.FindObjectsByType<Text>().Select(t=>t.text).ToArray();
        Check("All exact room labels are saved",new[]{"A101","A102","PHONG MAY TINH"}.All(label=>text.Contains(label)));
        var sign=GameObject.Find("A101_Name");Check("Room label faces corridor with positive scale",Vector3.Dot(sign.transform.forward,Vector3.right)>.99f&&sign.transform.lossyScale.x>0);
        Check("Floors walls ceilings and door colliders exist",UnityEngine.Object.FindObjectsByType<BoxCollider>().Length>45);
        originalSettings=InputSystem.settings;originalFlags=originalSettings.hideFlags;if(originalFlags==HideFlags.HideAndDontSave){originalSettings.hideFlags=HideFlags.DontSave;}
        settings=UnityEngine.Object.Instantiate(originalSettings);settings.hideFlags=HideFlags.HideAndDontSave;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        InputSystem.settings=settings;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
        actions=(InputActionAsset)Field(movement,"inputActions");originalDevices=actions.devices;actions.devices=new InputDevice[]{keyboard,mouse};
        oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
        for(int i=0;i<25;i++){yield return null;}
        Check("Player rests on saved corridor floor",capsule.isGrounded&&player.position.y>.8f&&player.position.y<1.3f);
        Capture("quest-hud");for(int i=0;i<3;i++)yield return null;
        Check("Saved editable interiors exist",GameObject.Find("Floor01_Interiors")!=null);
        Check("Interior renderer budget below 300",GameObject.Find("Floor01_Interiors").GetComponentsInChildren<Renderer>().Length<300);
        Check("All three doors share exactly one arm rig",doors.All(d=>d.GetComponent<DoorHandleIK>()!=null)&&doors.Select(d=>Field(d.GetComponent<DoorHandleIK>(),"rightHand")).Distinct().Count()==1);
        var first=doors.Single(d=>d.name=="Door_A101").GetComponent<DoorHandleIK>();var second=doors.Single(d=>d.name=="Door_A102").GetComponent<DoorHandleIK>();
        var reservation=first.AlignForOpening(player,player.GetComponentInChildren<Animator>());
        Check("First door reserves arm before alignment",reservation.MoveNext()&&(bool)Field(first,"ownsArm"));
        var competing=second.AlignForOpening(player,player.GetComponentInChildren<Animator>());
        Check("Second door cannot acquire occupied arm",!competing.MoveNext()&&!(bool)Field(second,"ownsArm")&&(bool)Field(first,"ownsArm"));
        var constraint=(UnityEngine.Animations.Rigging.TwoBoneIKConstraint)Field(first,"rightHand");constraint.weight=.6f;second.enabled=false;second.enabled=true;
        Check("Idle door lifecycle cannot reset active owner",Mathf.Abs(constraint.weight-.6f)<.001f);
        first.enabled=false;first.enabled=true;
        Check("Disabling owner releases arm and culling",constraint.weight==0&&!(bool)Field(first,"ownsArm")&&player.GetComponentInChildren<Animator>().cullingMode==AnimatorCullingMode.CullUpdateTransforms);
        competing=second.AlignForOpening(player,player.GetComponentInChildren<Animator>());Check("Released arm can be reacquired",competing.MoveNext()&&(bool)Field(second,"ownsArm"));second.enabled=false;second.enabled=true;
        Vector3 spawn=player.position;Quaternion spawnRotation=player.rotation;
        foreach(var lockedDoor in doors)
        {
            capsule.enabled=false;player.position=new Vector3(6,1.08f,lockedDoor.transform.position.z+.9f);capsule.enabled=true;
            for(int i=0;i<5;i++)yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            for(int i=0;i<35;i++)yield return null;
            Check(lockedDoor.name+" locked interaction does not start Open or IK",(bool)Field(lockedDoor,"isLocked")&&!(bool)Field(lockedDoor,"isOpen")&&!player.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Open")&&constraint.weight==0&&!lockedDoor.GetComponent<DoorHandleIK>().IsPrepared);
        }
        capsule.enabled=false;player.SetPositionAndRotation(spawn,spawnRotation);capsule.enabled=true;for(int i=0;i<5;i++)yield return null;
        foreach(var door in doors)
        {
            Quaternion original=door.transform.rotation;
            var leaf=door.GetComponentInChildren<BoxCollider>();
            bool clear=true;
            foreach(float angle in new[]{0f,15f,30f,45f,60f,75f,90f})
            {
                door.transform.rotation=original*Quaternion.Euler(0,angle,0);Physics.SyncTransforms();
                foreach(var other in UnityEngine.Object.FindObjectsByType<BoxCollider>())
                {
                    if(other==leaf||other.isTrigger||other.transform.IsChildOf(door.transform)){continue;}
                    if(Physics.ComputePenetration(leaf,leaf.transform.position,leaf.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float distance)&&distance>.005f){clear=false;}
                }
            }
            door.transform.rotation=original;Physics.SyncTransforms();Check(door.name+" hinge sweep clears static geometry",clear);
        }
        var walk=Drive(0,10.3f);while(walk.MoveNext()){yield return null;}
        var operation=Pickup("Key_A101");while(operation.MoveNext()){yield return null;}
        walk=Drive(2,10.3f);while(walk.MoveNext()){yield return null;}
        walk=Drive(2,17);while(walk.MoveNext()){yield return null;}
        walk=Drive(6,17);while(walk.MoveNext()){yield return null;}
        operation=Open("Door_A101");while(operation.MoveNext()){yield return null;}
        walk=Drive(10.7f,17);while(walk.MoveNext()){yield return null;}
        report.visited.Add("A101");Check("Player physically enters A101",player.position.x>9);
        walk=Drive(10.7f,21);while(walk.MoveNext()){yield return null;}
        Check("Key_A102 is locked away before solving",!keys.Single(k=>k.name=="Key_A102").gameObject.activeInHierarchy&&!inventory.HasItem("Key_A102"));
        operation=PressQuestKey(Key.F);while(operation.MoveNext())yield return null;for(int i=0;i<45;i++)yield return null;
        Check("F cannot collect the locked reward",!inventory.HasItem("Key_A102")&&inventory.ItemIds.Count==1);
        walk=Drive(10.7f,15.5f);while(walk.MoveNext())yield return null;walk=Drive(17,15.5f);while(walk.MoveNext())yield return null;walk=Drive(17,12.4f);while(walk.MoveNext())yield return null;
        operation=PressQuestKey(Key.F);while(operation.MoveNext())yield return null;
        Check("F reads the real clue with correct sample code",puzzleUI.Mode==QuestModal.Clue&&UnityEngine.Object.FindObjectsByType<ClueNote>().Single(n=>n.name=="A101_ClueNote").Content.Contains("2018")&&quests.CurrentIndex==3);
        Check("Clue dialog pauses and gates gameplay",gate.IsBlocked&&Time.timeScale==0);
        Capture("quest-clue");for(int i=0;i<3;i++)yield return null;
        Vector3 paused=player.position;int itemsBefore=inventory.ItemIds.Count;bool crouched=(bool)Field(movement,"isCrouching");int openedBefore=doors.Count(d=>(bool)Field(d,"isOpen"));
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift,Key.Space,Key.LeftCtrl,Key.E,Key.F,Key.Tab,Key.J));for(int i=0;i<12;i++)yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        Check("WASD/Shift/Space/Ctrl/E/F/Tab/J cannot leak through clue",Vector3.Distance(paused,player.position)<.0001f&&inventory.ItemIds.Count==itemsBefore&&(bool)Field(movement,"isCrouching")==crouched&&doors.Count(d=>(bool)Field(d,"isOpen"))==openedBefore&&!ui.IsOpen&&puzzleUI.Mode==QuestModal.Clue);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape,Key.F));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());for(int i=0;i<5;i++)yield return null;
        Check("Esc closes without same-frame F reopening or collecting",puzzleUI.Mode==QuestModal.None&&!gate.IsBlocked&&Time.timeScale==1&&inventory.ItemIds.Count==itemsBefore);
        operation=PressQuestKey(Key.LeftCtrl);while(operation.MoveNext())yield return null;Check("Existing crouch enabled before modal",(bool)Field(movement,"isCrouching"));
        operation=PressQuestKey(Key.J);while(operation.MoveNext())yield return null;Check("J opens journal with all three quest states",puzzleUI.Mode==QuestModal.Journal&&quests.GetState(0)==QuestState.Completed&&quests.GetState(3)==QuestState.InProgress&&quests.GetState(7)==QuestState.NotStarted);
        Capture("quest-journal");for(int i=0;i<3;i++)yield return null;
        operation=PressQuestKey(Key.Tab);while(operation.MoveNext())yield return null;Check("Tab does not overlap journal",!ui.IsOpen&&puzzleUI.Mode==QuestModal.Journal);
        operation=PressQuestKey(Key.J);while(operation.MoveNext())yield return null;for(int i=0;i<4;i++)yield return null;
        Check("J closes journal and preserves crouch",puzzleUI.Mode==QuestModal.None&&!gate.IsBlocked&&(bool)Field(movement,"isCrouching"));operation=PressQuestKey(Key.LeftCtrl);while(operation.MoveNext())yield return null;
        walk=Drive(17,15.5f);while(walk.MoveNext())yield return null;walk=Drive(10.7f,15.5f);while(walk.MoveNext())yield return null;walk=Drive(10.7f,21);while(walk.MoveNext())yield return null;
        operation=PressQuestKey(Key.E);while(operation.MoveNext())yield return null;Check("E opens four-digit cabinet UI",puzzleUI.Mode==QuestModal.Code&&gate.IsBlocked);
        for(int digit=0;digit<4;digit++){operation=PressQuestKey(Key.Digit9);while(operation.MoveNext())yield return null;}
        operation=PressQuestKey(Key.Enter);while(operation.MoveNext())yield return null;
        Check("Wrong code keeps cabinet and reward locked",!cabinet.IsUnlocked&&!keys.Single(k=>k.name=="Key_A102").gameObject.activeSelf&&puzzleUI.Mode==QuestModal.Code&&puzzleUI.Message.Length>0);
        Vector3 pinPosition=player.position;int pinItems=inventory.ItemIds.Count;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.E,Key.F,Key.Tab,Key.J,Key.Space,Key.LeftShift,Key.LeftCtrl));for(int i=0;i<12;i++)yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        Check("Code dialog blocks gameplay and other panels",Vector3.Distance(pinPosition,player.position)<.0001f&&inventory.ItemIds.Count==pinItems&&!ui.IsOpen&&!cabinet.IsUnlocked&&puzzleUI.Mode==QuestModal.Code);
        Capture("quest-code-wrong");for(int i=0;i<3;i++)yield return null;
        Check("Malformed PINs rejected by production cabinet",!cabinet.TryUnlock("201")&&!cabinet.TryUnlock("abcd")&&!cabinet.TryUnlock("２０１８"));
        operation=PressQuestKey(Key.Delete);while(operation.MoveNext())yield return null;
        foreach(var digit in new[]{Key.Digit2,Key.Digit0,Key.Digit1,Key.Digit8}){operation=PressQuestKey(digit);while(operation.MoveNext())yield return null;}
        Check("Numeric keyboard enters exactly 2018",puzzleUI.EnteredCode=="2018");Vector3 beforeCabinetOpen=player.position;operation=PressQuestKey(Key.Enter);while(operation.MoveNext())yield return null;
        Check("Correct code opens cabinet, closes modal, no direct item grant",cabinet.IsUnlocked&&puzzleUI.Mode==QuestModal.None&&!inventory.HasItem("Key_A102"));
        Check("Key cannot be collected while cabinet is opening",cabinet.IsOpening&&!keys.Single(k=>k.name=="Key_A102").gameObject.activeSelf);
        float openingDeadline=Time.realtimeSinceStartup+3f;
        while(cabinet.IsOpening&&Time.realtimeSinceStartup<openingDeadline)yield return null;
        for(int i=0;i<5;i++)yield return null;
        Check("Cabinet completes its real unscaled-time animation",!cabinet.IsOpening);
        Check("Cabinet leaf does not push Player out of pickup range",Vector3.Distance(beforeCabinetOpen,player.position)<.03f);
        Check("Key_A102 enabled only after cabinet animation",keys.Single(k=>k.name=="Key_A102").gameObject.activeSelf&&!gate.IsBlocked&&quests.CurrentIndex==4);
        Capture("quest-cabinet-open");for(int i=0;i<3;i++)yield return null;
        operation=Pickup("Key_A102");while(operation.MoveNext()){yield return null;}
        CheckCamera("A101");
        walk=Drive(10.7f,19);while(walk.MoveNext()){yield return null;}walk=Drive(17,19);while(walk.MoveNext()){yield return null;}walk=Drive(17,15.5f);while(walk.MoveNext()){yield return null;}walk=Drive(10.7f,15.5f);while(walk.MoveNext()){yield return null;}
        walk=Drive(10.7f,17);while(walk.MoveNext()){yield return null;}
        walk=Drive(2,17);while(walk.MoveNext()){yield return null;}
        walk=Drive(2,35);while(walk.MoveNext()){yield return null;}
        walk=Drive(6,35);while(walk.MoveNext()){yield return null;}
        operation=Open("Door_A102");while(operation.MoveNext()){yield return null;}
        Check("A102 opens only with earned Key_A102",inventory.HasItem("Key_A102"));
        walk=Drive(10.7f,35);while(walk.MoveNext()){yield return null;}
        report.visited.Add("A102");Check("Player physically enters A102",player.position.x>9);
        for(int i=0;i<8;i++)yield return null;
        var sequence=UnityEngine.Object.FindObjectsByType<SequenceLockPanel>().Single();
        Check("Single existing QuestManager activates A102 objectives",UnityEngine.Object.FindObjectsByType<QuestManager>().Length==1&&quests.HasObserved("room:A102")&&quests.CurrentIndex==6&&quests.CurrentObjective.Contains("A102"));
        walk=Drive(10.7f,39);while(walk.MoveNext()){yield return null;}
        Check("Key_Lab starts inaccessible behind A102 puzzle",!keys.Single(k=>k.name=="Key_Lab").gameObject.activeInHierarchy&&!inventory.HasItem("Key_Lab"));
        operation=PressQuestKey(Key.F);while(operation.MoveNext())yield return null;for(int i=0;i<45;i++)yield return null;
        Check("F cannot bypass unsolved sequence lock",!inventory.HasItem("Key_Lab")&&inventory.ItemIds.Count==2);
        operation=PressQuestKey(Key.E);while(operation.MoveNext())yield return null;
        Check("E opens reused modal for sequence rather than PIN",puzzleUI.Mode==QuestModal.Sequence&&gate.IsBlocked);
        foreach(var k in new[]{Key.A,Key.C,Key.B}){operation=PressQuestKey(k);while(operation.MoveNext())yield return null;}
        operation=PressQuestKey(Key.Enter);while(operation.MoveNext())yield return null;
        Check("Correct guess before clues cannot unlock reward",!sequence.IsSolved&&!keys.Single(k=>k.name=="Key_Lab").gameObject.activeSelf&&puzzleUI.Message.Length>0);
        operation=PressQuestKey(Key.Escape);while(operation.MoveNext())yield return null;for(int i=0;i<5;i++)yield return null;
        walk=Drive(10.7f,37);while(walk.MoveNext())yield return null;walk=Drive(17.35f,37);while(walk.MoveNext())yield return null;walk=Drive(17.35f,39);while(walk.MoveNext())yield return null;
        operation=PressQuestKey(Key.F);while(operation.MoveNext())yield return null;
        Check("Clue C reads 2018 and own A102 title",puzzleUI.Mode==QuestModal.Clue&&quests.HasObserved("clue:A102:C")&&UnityEngine.Object.FindObjectsByType<ClueNote>().Single(n=>n.name=="A102_Clue_C").Content.Contains("2018"));
        Check("Reading future clue records progress without skipping A",sequence.ReadClueCount==1&&quests.CurrentIndex==6);
        operation=PressQuestKey(Key.Escape);while(operation.MoveNext())yield return null;for(int i=0;i<5;i++)yield return null;
        walk=Drive(17.35f,37);while(walk.MoveNext())yield return null;walk=Drive(17,29.3f);while(walk.MoveNext())yield return null;
        operation=PressQuestKey(Key.F);while(operation.MoveNext())yield return null;
        Check("Clue A reads 2016",puzzleUI.Mode==QuestModal.Clue&&quests.HasObserved("clue:A102:A")&&sequence.ReadClueCount==2&&quests.CurrentIndex==7);
        operation=PressQuestKey(Key.Escape);while(operation.MoveNext())yield return null;for(int i=0;i<5;i++)yield return null;
        walk=Drive(17,29.9f);while(walk.MoveNext())yield return null;walk=Drive(15,29.9f);while(walk.MoveNext())yield return null;
        operation=PressQuestKey(Key.F);while(operation.MoveNext())yield return null;
        Check("Clue B reads 2020 and completes all clue goals",puzzleUI.Mode==QuestModal.Clue&&quests.HasObserved("clue:A102:B")&&sequence.ReadClueCount==3&&quests.CurrentIndex==9);
        operation=PressQuestKey(Key.Escape);while(operation.MoveNext())yield return null;for(int i=0;i<5;i++)yield return null;
        operation=PressQuestKey(Key.J);while(operation.MoveNext())yield return null;
        Check("Journal shows three completed A102 clue steps",puzzleUI.Mode==QuestModal.Journal&&Enumerable.Range(6,3).All(i=>quests.GetState(i)==QuestState.Completed)&&quests.GetState(9)==QuestState.InProgress);
        operation=PressQuestKey(Key.J);while(operation.MoveNext())yield return null;for(int i=0;i<5;i++)yield return null;
        walk=Drive(17,29.9f);while(walk.MoveNext())yield return null;walk=Drive(17,37);while(walk.MoveNext())yield return null;walk=Drive(10.7f,37);while(walk.MoveNext())yield return null;walk=Drive(10.7f,39);while(walk.MoveNext())yield return null;
        operation=PressQuestKey(Key.E);while(operation.MoveNext())yield return null;
        Vector3 sequencePosition=player.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.E,Key.F,Key.Tab,Key.J,Key.Space,Key.LeftCtrl));for(int i=0;i<12;i++)yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        Check("Sequence modal prevents WASD/E/F/Tab/J/Space/Ctrl leakage",Vector3.Distance(sequencePosition,player.position)<.0001f&&!ui.IsOpen&&puzzleUI.Mode==QuestModal.Sequence&&inventory.ItemIds.Count==2);
        foreach(var k in new[]{Key.A,Key.B,Key.C}){operation=PressQuestKey(k);while(operation.MoveNext())yield return null;}
        operation=PressQuestKey(Key.Enter);while(operation.MoveNext())yield return null;
        Check("Wrong A-B-C order gives feedback and keeps reward locked",!sequence.IsSolved&&!keys.Single(k=>k.name=="Key_Lab").gameObject.activeSelf&&puzzleUI.Mode==QuestModal.Sequence&&puzzleUI.Message.Contains("chưa đúng"));
        Check("Missing, duplicate and out-of-range permutations rejected",!sequence.TrySolve(new[]{0,0,1})&&!sequence.TrySolve(new[]{0,1})&&!sequence.TrySolve(new[]{0,2,3}));
        operation=PressQuestKey(Key.Delete);while(operation.MoveNext())yield return null;operation=PressQuestKey(Key.A);while(operation.MoveNext())yield return null;operation=PressQuestKey(Key.A);while(operation.MoveNext())yield return null;
        Check("Repeated event cannot occupy two slots",puzzleUI.SelectedEvents.Count==1&&puzzleUI.Message.Length>0);
        operation=PressQuestKey(Key.Backspace);while(operation.MoveNext())yield return null;Check("Backspace can edit sequence",puzzleUI.SelectedEvents.Count==0);
        foreach(var k in new[]{Key.Digit1,Key.Digit3,Key.Digit2}){operation=PressQuestKey(k);while(operation.MoveNext())yield return null;}
        Check("1-3-2 selects A-C-B",puzzleUI.SelectedEvents.SequenceEqual(new[]{0,2,1}));
        Vector3 beforeSequenceOpen=player.position;operation=PressQuestKey(Key.Enter);while(operation.MoveNext())yield return null;
        Check("Correct A-C-B solves without direct inventory grant",sequence.IsSolved&&puzzleUI.Mode==QuestModal.None&&!inventory.HasItem("Key_Lab"));
        Check("Reward remains inactive during shutter opening",sequence.IsOpening&&!keys.Single(k=>k.name=="Key_Lab").gameObject.activeSelf);
        float sequenceDeadline=Time.realtimeSinceStartup+3;while(sequence.IsOpening&&Time.realtimeSinceStartup<sequenceDeadline)yield return null;for(int i=0;i<5;i++)yield return null;
        Check("Completed shutter releases original Key_Lab",!sequence.IsOpening&&keys.Single(k=>k.name=="Key_Lab").gameObject.activeSelf&&quests.CurrentIndex==10);
        Check("Shutter does not push Player away",Vector3.Distance(beforeSequenceOpen,player.position)<.03f);
        operation=Pickup("Key_Lab");while(operation.MoveNext()){yield return null;}
        // Quest V1's existing inventory adapter observes the pickup on its next Update.
        for(int i=0;i<3;i++)yield return null;
        Check("Original Inventory records Key_Lab and advances to LAB",inventory.HasItem("Key_Lab")&&quests.CurrentIndex==11&&quests.CurrentObjective.Contains("LAB"));
        Check("Solved panel cannot grant a second reward",!sequence.TrySolve(new[]{0,2,1})&&inventory.ItemIds.Count==3);
        CheckCamera("A102");
        walk=Drive(10.7f,37);while(walk.MoveNext()){yield return null;}walk=Drive(17,37);while(walk.MoveNext()){yield return null;}walk=Drive(17,33.5f);while(walk.MoveNext()){yield return null;}walk=Drive(10.7f,33.5f);while(walk.MoveNext()){yield return null;}
        walk=Drive(10.7f,35);while(walk.MoveNext()){yield return null;}
        walk=Drive(2,35);while(walk.MoveNext()){yield return null;}
        walk=Drive(2,53);while(walk.MoveNext()){yield return null;}
        walk=Drive(6,53);while(walk.MoveNext()){yield return null;}
        operation=Open("Door_LAB");while(operation.MoveNext()){yield return null;}
        walk=Drive(10.7f,53);while(walk.MoveNext()){yield return null;}
        report.visited.Add("LAB");Check("Player physically enters LAB",player.position.x>9);
        walk=Drive(20,53);while(walk.MoveNext()){yield return null;}
        for(int i=0;i<30;i++){yield return null;}CheckCamera("LAB finish");
        Check("Complete route retains all three ordinary keys",inventory.ItemIds.Count==3&&new[]{"Key_A101","Key_A102","Key_Lab"}.All(inventory.HasItem));
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Tab));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        Check("Existing Tab inventory still opens",(bool)Field(ui,"isOpen"));
        operation=PressQuestKey(Key.J);while(operation.MoveNext())yield return null;Check("Journal cannot overlap an open Inventory",ui.IsOpen&&puzzleUI.Mode==QuestModal.None);
        Check("All twelve actual route quests complete",quests.IsComplete&&Enumerable.Range(0,12).All(i=>quests.GetState(i)==QuestState.Completed));
        int completed=quests.CurrentIndex;Check("Unknown/repeated events are harmless",!quests.Signal("not-configured")&&!quests.Signal("clue:A101")&&quests.CurrentIndex==completed);
        Check("Unlocked cabinet never regrants collected key",!cabinet.TryUnlock("2018")&&!keys.Single(k=>k.name=="Key_A102").gameObject.activeSelf&&inventory.ItemIds.Count==3);
        operation=PressQuestKey(Key.Tab);while(operation.MoveNext())yield return null;
        Check("Tab closes inventory after quest completion",!ui.IsOpen);
        walk=Drive(10.7f,53);while(walk.MoveNext())yield return null;walk=Drive(2,53);while(walk.MoveNext())yield return null;
        Vector3 begin=player.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));for(int i=0;i<25;i++)yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        float walked=Vector3.Distance(begin,player.position);Check("WASD movement resumes after every modal",walked>1f);
        begin=player.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A,Key.LeftShift));for(int i=0;i<25;i++)yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        Check("Shift sprint remains faster than walk",Vector3.Distance(begin,player.position)>walked*1.3f);
        operation=PressQuestKey(Key.LeftCtrl);while(operation.MoveNext())yield return null;Check("Ctrl crouch and Animator flag preserved",(bool)Field(movement,"isCrouching")&&player.GetComponentInChildren<Animator>().GetBool("IsCrouching"));
        operation=PressQuestKey(Key.LeftCtrl);while(operation.MoveNext())yield return null;Check("Ctrl returns to standing",!(bool)Field(movement,"isCrouching"));
        float floorY=player.position.y,peakY=floorY;operation=PressQuestKey(Key.Space);while(operation.MoveNext()){peakY=Mathf.Max(peakY,player.position.y);yield return null;}
        for(int i=0;i<100;i++){peakY=Mathf.Max(peakY,player.position.y);yield return null;}
        Check("Space jump and landing preserved after quest dialogs",peakY>floorY+.5f&&Mathf.Abs(player.position.y-floorY)<.15f);
        // Independent scene-local quest instance exercises production ordering logic, not duplicate implementation.
        var testGO=new GameObject("Temporary_QuestOrder_Test");testGO.SetActive(false);var ordered=testGO.AddComponent<QuestManager>();
        var def1=new QuestDefinition();var def2=new QuestDefinition();SetField(def1,"id","first");SetField(def1,"title","First");SetField(def1,"completionEvent","first-event");SetField(def2,"id","second");SetField(def2,"title","Second");SetField(def2,"completionEvent","second-event");SetField(ordered,"definitions",new[]{def1,def2});testGO.SetActive(true);
        Check("Quest state initialization on another instance",ordered.GetState(0)==QuestState.InProgress&&ordered.GetState(1)==QuestState.NotStarted);
        Check("Early future gameplay event is retained",ordered.Signal("second-event")&&ordered.GetState(1)==QuestState.NotStarted);
        Check("Earlier completion consumes recorded future fact",ordered.Signal("first-event")&&ordered.IsComplete&&ordered.GetState(1)==QuestState.Completed);
        gate.Block();Check("Input gate can be acquired",gate.IsBlocked&&Time.timeScale==0);gate.enabled=false;Check("Disabling gate releases pause/input",!gate.IsBlocked&&Time.timeScale==1);gate.enabled=true;
        for(int i=0;i<5;i++)yield return null;Check("Camera resumes safely after modal teardown",camera.GetComponent<HUSEscape.Player.ThirdPersonCameraFollow>().enabled&&!float.IsNaN(camera.transform.position.x));
        Check("No runtime errors",report.errors.Count==0);
    }
    private IEnumerator Drive(float x,float z)
    {
        float deadline=Time.time+20;
        while(Vector2.Distance(new Vector2(player.position.x,player.position.z),new Vector2(x,z))>.12f)
        {
            if(Time.time>deadline){throw new InvalidOperationException($"Walking blocked towards ({x},{z}), actual={player.position}");}
            var pressed=new List<Key>();if(Mathf.Abs(x-player.position.x)>.07f){pressed.Add(x>player.position.x?Key.D:Key.A);}if(Mathf.Abs(z-player.position.z)>.07f){pressed.Add(z>player.position.z?Key.W:Key.S);}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(pressed.ToArray()));report.drivenFrames++;yield return null;
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        Check($"Walk reached ({x},{z}) without falling",player.position.y>.8f&&player.position.y<1.4f);
    }
    private IEnumerator Pickup(string id)
    {
        int count=inventory.ItemIds.Count;
        var selected=keys.Where(k=>k.gameObject.activeInHierarchy&&Selected(k)).ToArray();
        if(selected.Length!=1||(string)Field(selected[0],"itemId")!=id)
            throw new InvalidOperationException("Pickup selection diagnostics: "+id+" actor="+player.position+" gate="+player.GetComponent<GameplayInputGate>().IsBlocked+" candidates="+string.Join(";",keys.Select(k=>k.name+" active="+k.gameObject.activeInHierarchy+" position="+k.transform.position+" distance="+Vector3.Distance(player.position,k.transform.position)+" selected="+Selected(k))));
        Check(id+" is unique F target",true);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        float deadline=Time.time+2;while(!inventory.HasItem(id)&&Time.time<deadline){yield return null;}
        Check(id+" one F adds exactly one item",inventory.HasItem(id)&&inventory.ItemIds.Count==count+1);
    }
    private IEnumerator Open(string name)
    {
        var door=doors.Single(d=>d.name==name);Check(name+" is unique E target",Selected(door)&&doors.Count(Selected)==1);
        watched=door;sawOpen=false;contactAtOpen=999;weightAtOpen=0;followFrames=0;closedRotation=door.transform.localRotation;
        var ik=door.GetComponent<DoorHandleIK>();var target=(Transform)Field(ik,"handleTarget");Check(name+" local target belongs to moving hinge",target.parent==door.transform&&target.localPosition==new Vector3(1.5f,-.25f,.24f));
        Check(name+" resolves same Player inventory",ReferenceEquals(Field(door,"inventory"),inventory));
        int locked=doors.Count(d=>(bool)Field(d,"isLocked"));int items=inventory.ItemIds.Count;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        float end=Time.time+2.4f;while(Time.time<end){yield return null;}
        Check(name+" one E unlocks only one door",!(bool)Field(door,"isLocked")&&doors.Count(d=>(bool)Field(d,"isLocked"))==locked-1);
        Check(name+" opens fully without consuming key",(bool)Field(door,"isOpen")&&inventory.ItemIds.Count==items);
        Check(name+" open portal has no blocking wall or leaf",PortalObstacles(door).Length==0);
        Check(name+" hand contacts handle before opening",sawOpen&&contactAtOpen<.07f&&weightAtOpen>.8f);
        Check(name+" hand follows initial door swing",followFrames>=2);
        Check(name+" releases shared IK after interaction",ik.CurrentWeight<.001f&&doors.All(d=>!d.GetComponent<DoorHandleIK>().IsPrepared&&!(bool)Field(d.GetComponent<DoorHandleIK>(),"ownsArm")));
        watched=null;
    }
    private Collider[] PortalObstacles(DoorInteraction door)
    {
        float z=door.transform.position.z+.9f;
        return Physics.CapsuleCastAll(new Vector3(5.8f,.6f,z),new Vector3(5.8f,1.5f,z),.48f,Vector3.right,4f,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>!(h.collider is CharacterController)&&!h.collider.transform.IsChildOf(player)).Select(h=>h.collider).Distinct().ToArray();
    }
    private void CheckCamera(string where)
    {
        Vector3 p=camera.transform.position;Check("Default camera finite and indoors at "+where,!float.IsNaN(p.x)&&p.y>0&&p.y<5.4f&&p.x>7&&p.x<23);
        Vector3 focus=player.position+new Vector3(0,1.2f,0);Vector3 delta=focus-p;
        var obstacles=Physics.RaycastAll(p,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>!h.collider.transform.IsChildOf(player)).ToArray();
        Check("Default camera has unobstructed player view at "+where,obstacles.Length==0);
    }
    private static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private static void SetField(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    private void Capture(string name){string path=Path.Combine(Path.GetDirectoryName(reportPath),name+".png");ScreenCapture.CaptureScreenshot(path);report.screenshots.Add(path);}
    private IEnumerator PressQuestKey(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
    private static bool Selected(MonoBehaviour o)=>(bool)o.GetType().GetMethod("IsSelectedTarget",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);
    private void Check(string name,bool passed){report.checks.Add(new QuestV2Check{name=name,passed=passed});if(!passed){throw new InvalidOperationException(name);}}
    private void OnLog(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert){report.errors.Add(m+"\n"+s);}}
    private void Finish(Exception error)
    {
        if(error!=null){report.errors.Add(error.ToString());}
        if(actions!=null){actions.devices=originalDevices;}if(keyboard!=null&&keyboard.added){InputSystem.RemoveDevice(keyboard);}if(mouse!=null&&mouse.added){InputSystem.RemoveDevice(mouse);}
        if(originalSettings!=null){InputSystem.settings=originalSettings;originalSettings.hideFlags=originalFlags;}if(settings!=null){UnityEngine.Object.Destroy(settings);}Time.captureDeltaTime=oldCapture;
        report.screenshots=report.screenshots.Where(File.Exists).ToList();
        report.visualCaptureStatus=report.screenshots.Count==5?"Captured; manual image inspection required":"NOT TESTED: batch Game View did not produce all requested screenshots.";
        Application.logMessageReceived-=OnLog;report.passed=error==null&&report.errors.Count==0;File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));Finished=true;
    }
}
