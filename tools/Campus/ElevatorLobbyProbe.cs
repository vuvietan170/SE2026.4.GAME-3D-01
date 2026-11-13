using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using HUSEscape.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class ElevatorLobbyProbe : MonoBehaviour
{
    public string reportPath;
    public bool Finished { get; private set; }
    public bool Passed => report.passed;
    private readonly CampusReport report = new CampusReport();
    private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    private Keyboard keyboard;
    private InputSettings original, settings;
    private JourneySceneEntry entry;
    private void Start() { DontDestroyOnLoad(gameObject); report.unity=Application.unityVersion; Time.captureDeltaTime=1f/60f; stack.Push(Run()); }
    private void Update()
    {
        if(Finished)return;
        try { while(stack.Count>0) { var e=stack.Peek(); if(!e.MoveNext()){stack.Pop();continue;} if(e.Current is IEnumerator child){stack.Push(child);continue;} return; } Finish(null); }
        catch(Exception e){Finish(e);}
    }
    private IEnumerator Run()
    {
        yield return Frames(25); entry=FindObjectsByType<JourneySceneEntry>().Single();
        var elevator=FindObjectsByType<ElevatorUnavailable>().Single();var exit=FindObjectsByType<BuildingExit>().Single();
        var doors=GameObject.Find("Elevator_Facade").GetComponentsInChildren<Transform>().Where(t=>t.name=="Elevator_ClosedDoor").ToArray();
        var positions=doors.Select(t=>t.position).ToArray();Vector3 spawn=entry.player.transform.position;
        original=Instantiate(InputSystem.settings);original.hideFlags=HideFlags.HideAndDontSave;settings=Instantiate(original);settings.hideFlags=HideFlags.HideAndDontSave;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        InputSystem.settings=settings;keyboard=InputSystem.AddDevice<Keyboard>();
        Place(new Vector3(0,1.08f,67.5f));yield return Frames(5);
        Check("Main escape centreline has no elevator interaction",!elevator.CanInteract);
        Place(new Vector3(1.8f,1.08f,68.05f));yield return Frames(45);
        Check("Side call panel accessible without main exit conflict",elevator.CanInteract&&!exit.IsNear);
        Check("Panel initially offers E",elevator.Message.StartsWith("E"));
        yield return Press(Key.E);
        Check("Real E publishes exact unavailable message",elevator.Message=="Thang máy chưa khả dụng");
        Check("No floor travel or quest or inventory mutation",!entry.Session.IsLoading&&entry.quests.CurrentIndex==0&&entry.inventory.ItemIds.Count==0);
        Check("Elevator doors remain stationary",doors.Select((t,i)=>t.position==positions[i]).All(v=>v));
        Check("Player remains grounded at ground floor",entry.player.isGrounded&&Mathf.Abs(entry.player.transform.position.y-1.08f)<.1f);
        yield return Press(Key.J);Check("Journal blocks elevator interaction",entry.Gate.IsBlocked&&!elevator.CanInteract);
        Vector3 at=entry.player.transform.position;yield return Press(Key.E,Key.F,Key.W,Key.Tab);
        Check("Modal prevents movement and inventory behind panel",Vector3.Distance(at,entry.player.transform.position)<.01f&&!entry.player.GetComponent<InventoryUI>().IsOpen);
        yield return Press(Key.Escape);yield return Press(Key.Tab);
        Check("Inventory blocks elevator interaction",entry.player.GetComponent<InventoryUI>().IsOpen&&!elevator.CanInteract);
        yield return Press(Key.Tab);Check("Closing inventory restores panel",elevator.CanInteract);
        Place(new Vector3(0,1.08f,70.5f));yield return Frames(5);
        Check("Final T3 exit keeps exclusive E interaction",exit.IsNear&&exit.CanShowPrompt&&!elevator.CanInteract);
        Place(spawn);InputSystem.RemoveDevice(keyboard);keyboard=null;InputSystem.settings=original;Destroy(settings);settings=null;
        yield return Frames(10);
        string folder=Path.Combine(Path.GetDirectoryName(reportPath),"FullRoute");Directory.CreateDirectory(folder);
        var regression=gameObject.AddComponent<CampusPlayProbe>();regression.reportPath=Path.Combine(folder,"floor-suite.json");
        while(!regression.Finished)yield return null;
        Check("Full native Floor to Campus and Save Continue regression passes",regression.Passed);
    }
    private void Place(Vector3 p){entry.player.enabled=false;entry.player.transform.SetPositionAndRotation(p,Quaternion.identity);entry.player.enabled=true;Physics.SyncTransforms();}
    private IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    private IEnumerator Press(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return Frames(2);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames(4);}
    private void Check(string name,bool value){report.checks.Add(new CampusCheck{name=name,passed=value});if(!value)throw new Exception(name);}
    private void Finish(Exception e){if(e!=null)report.errors.Add(e.ToString());if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(original!=null)InputSystem.settings=original;if(settings!=null)Destroy(settings);report.passed=e==null;File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));Finished=true;}
}
