using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HUSEscape.Player;
using HUSEscape.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

[Serializable] public sealed class CameraCheck { public string name; public bool passed; }
[Serializable] public sealed class CameraCollisionReport
{
    public bool passed;public string unity;public List<CameraCheck> checks=new List<CameraCheck>();public List<string> errors=new List<string>();
    public List<string> editorStartupErrors=new List<string>();
    public int safetyFrames;public float maxPenetration,maxStationaryJitter,maxJumpCameraY,effectiveRadius;
}
[DefaultExecutionOrder(3000)]
public sealed class CameraCollisionProbe:MonoBehaviour
{
    public string reportPath;public bool Finished{get;private set;}public bool Passed=>r.passed;
    readonly CameraCollisionReport r=new CameraCollisionReport();readonly Stack<IEnumerator> steps=new Stack<IEnumerator>();
    ThirdPersonCameraFollow follow;Camera camera;Transform actor;CharacterController cc;Component move;PlayerInventory inventory;
    Keyboard keyboard;Mouse mouse;InputActionAsset actions;ReadOnlyArray<InputDevice>? devices;InputSettings original,settings;HideFlags flags;float capture;
    bool monitor;SphereCollider probe;readonly Vector3[] corners=new Vector3[4];
    void Start(){Application.logMessageReceived+=OnLog;r.unity=Application.unityVersion;steps.Push(Run());}
    void Update()
    {
        if(Finished)return;try{while(steps.Count>0){var current=steps.Peek();if(!current.MoveNext()){steps.Pop();continue;}if(current.Current is IEnumerator nested){steps.Push(nested);continue;}return;}Finish(null);}catch(Exception e){Finish(e);}
    }
    void LateUpdate()
    {
        if(Finished||!monitor||probe==null)return;
        try
        {
            r.safetyFrames++;float radius=(float)Call(follow,"GetCollisionRadius");r.effectiveRadius=radius;
            foreach(var other in Physics.OverlapSphere(camera.transform.position,radius,~0,QueryTriggerInteraction.Ignore))
            {
                if(other.transform.IsChildOf(actor)||other.transform.IsChildOf(camera.transform)||!Included(other))continue;
                if(Physics.ComputePenetration(probe,camera.transform.position,Quaternion.identity,other,other.transform.position,other.transform.rotation,out _,out float depth))
                {r.maxPenetration=Mathf.Max(r.maxPenetration,depth);if(depth>.003f)throw new InvalidOperationException("Camera sphere penetrates "+other.name+" by "+depth+" at "+camera.transform.position);}
            }
            camera.CalculateFrustumCorners(new Rect(0,0,1,1),camera.nearClipPlane,Camera.MonoOrStereoscopicEye.Mono,corners);
            foreach(var corner in corners)foreach(var other in Physics.OverlapSphere(camera.transform.TransformPoint(corner),.003f,~0,QueryTriggerInteraction.Ignore))
                if(!other.transform.IsChildOf(actor)&&!other.transform.IsChildOf(camera.transform)&&Included(other))throw new InvalidOperationException("Near-plane corner inside "+other.name);
        }
        catch(Exception e){Finish(e);}
    }
    bool Included(Collider other)=>(((LayerMask)Field(follow,"collisionLayers")).value&(1<<other.gameObject.layer))!=0;
    IEnumerator Run()
    {
        yield return null;actor=UnityEngine.Object.FindObjectsByType<PlayerInventory>().Single().transform;inventory=actor.GetComponent<PlayerInventory>();cc=actor.GetComponent<CharacterController>();
        camera=UnityEngine.Object.FindObjectsByType<Camera>().Single();follow=camera.GetComponent<ThirdPersonCameraFollow>();move=actor.GetComponents<MonoBehaviour>().Single(c=>c!=null&&c.GetType().Name=="PlayerController1");
        probe=(SphereCollider)Field(follow,"overlapProbe");
        Check("Existing camera and GUID-bound component active",follow!=null&&follow.enabled&&probe!=null&&!probe.enabled);
        Check("Original orbit settings retained",(float)Field(follow,"distance")==6.3f&&(float)Field(follow,"sensitivity")==.12f&&(float)Field(follow,"minPitch")==-20&&(float)Field(follow,"maxPitch")==65);
        Check("Original smoothing and focus retained",(float)Field(follow,"positionSmoothTime")==.08f&&(float)Field(follow,"rotationSharpness")==12&&(Vector3)Field(follow,"lookAtOffset")==new Vector3(0,1.2f,0));
        original=InputSystem.settings;flags=original.hideFlags;if(flags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;
        settings=UnityEngine.Object.Instantiate(original);settings.hideFlags=HideFlags.HideAndDontSave;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        InputSystem.settings=settings;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();actions=(InputActionAsset)Field(move,"inputActions");devices=actions.devices;actions.devices=new InputDevice[]{keyboard,mouse};capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/60f;
        monitor=true;Place(0,8);Orbit(0,20);yield return Wait(120);
        Check("Corridor retains original 6.3m unobstructed orbit",Mathf.Abs(Distance()-6.3f)<.03f);
        float yaw=(float)Field(follow,"yaw"),pitch=(float)Field(follow,"pitch");InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(120,60));yield return Wait(3);
        Check("Mouse yaw sensitivity unchanged",Mathf.Abs((float)Field(follow,"yaw")-yaw-14.4f)<.01f);
        Check("Mouse pitch sensitivity unchanged",Mathf.Abs((float)Field(follow,"pitch")-pitch+7.2f)<.01f);
        Place(-6.3f,25);Orbit(90,20);yield return Wait(100);Check("West wall pulls camera inward",Distance()<1);
        Vector3 stable=camera.transform.position;for(int i=0;i<90;i++){r.maxStationaryJitter=Mathf.Max(r.maxStationaryJitter,Vector3.Distance(stable,camera.transform.position));yield return null;}
        Check("No stationary wall jitter",r.maxStationaryJitter<.005f);
        Orbit(0,20);float previousBoom=(float)Field(follow,"boomDistance");bool gradual=false;for(int i=0;i<100;i++){yield return null;float boom=(float)Field(follow,"boomDistance");if(boom>previousBoom+.0001f&&boom<6.2f)gradual=true;previousBoom=boom;}
        Check("Clear view restores original distance smoothly",gradual&&Mathf.Abs(Distance()-6.3f)<.04f);
        foreach(var room in new[]{new Vector2(17,19),new Vector2(17,37),new Vector2(17,53)})
        {
            Place(room.x,room.y);
            foreach(float p in new[]{-20f,20f,65f})for(int y=0;y<360;y+=30){Orbit(y,p);yield return Wait(6);}
            Check("Full yaw and pitch sweep safe at "+room,true);
        }
        foreach(var spot in new[]{new Vector2(15,19.3f),new Vector2(19,37.3f),new Vector2(15,53.3f)})
        {
            Place(spot.x,spot.y);Orbit(0,-20);yield return Wait(80);Check("Camera safe near furniture/computers at "+spot,Distance()<6.3f);
        }
        Place(7.65f,11);Orbit(45,65);foreach(float aspect in new[]{4f/3f,16f/9f,21f/9f}){camera.aspect=aspect;yield return Wait(40);}camera.ResetAspect();
        Check("Room corner and multiple aspect ratios protect near plane",r.maxPenetration<=.003f);
        Place(17,53);Orbit(0,65);yield return Wait(100);float floor=actor.position.y,peak=floor;yield return Press(Key.Space);
        for(int i=0;i<100;i++){peak=Mathf.Max(peak,actor.position.y);r.maxJumpCameraY=Mathf.Max(r.maxJumpCameraY,camera.transform.position.y);yield return null;}
        Check("Real Space jump still reaches normal height",peak>floor+.5f&&Mathf.Abs(actor.position.y-floor)<.15f);
        Check("Jump near ceiling protects near clip sphere",r.maxJumpCameraY+r.effectiveRadius<5.41f);
        var door=UnityEngine.Object.FindObjectsByType<DoorInteraction>().Single(d=>d.name=="Door_A101");inventory.AddItem("Key_A101");Place(6,17);Orbit(0,20);yield return Wait(40);yield return Press(Key.E);yield return Wait(180);
        Check("Real E opening and IK still work",(bool)Field(door,"isOpen")&&!door.GetComponent<DoorHandleIK>().IsPrepared);
        Place(8.1f,17);Orbit(0,20);yield return Wait(90);Check("Open door leaf blocks camera",Distance()<1.5f);
        yield return Press(Key.E);yield return Wait(140);Check("Closing moving door remains safe",!(bool)Field(door,"isOpen"));
        // Temporary isolated geometry tests exercise the production component, not a duplicate camera solver.
        var floorGo=Box("Temporary_Camera_Floor",new Vector3(1000,-.1f,1000),new Vector3(40,.2f,40));Place(1000,1000);Orbit(0,20);yield return Wait(120);
        Check("Open area ignores Player collider",Mathf.Abs(Distance()-6.3f)<.04f);
        Vector3 focus=actor.position+new Vector3(0,1.2f,0);Vector3 backwards=-(Quaternion.Euler(20,0,0)*Vector3.forward);
        var blocker=Box("Temporary_Thin_Wall",focus+backwards*3,new Vector3(10,10,.02f));blocker.layer=29;yield return Wait(40);
        Check("Thin wall retracts camera",Distance()<3);
        blocker.GetComponent<Collider>().isTrigger=true;yield return Wait(130);Check("Interaction trigger is ignored",Mathf.Abs(Distance()-6.3f)<.04f);
        blocker.GetComponent<Collider>().isTrigger=false;Set(follow,"collisionLayers",(LayerMask)(~(1<<29)));yield return Wait(130);Check("LayerMask can exclude a solid obstacle",Mathf.Abs(Distance()-6.3f)<.04f);
        Set(follow,"collisionLayers",(LayerMask)(~0));yield return Wait(30);Check("LayerMask re-includes obstacle",Distance()<3);
        blocker.SetActive(false);yield return Wait(130);var own=Box("Temporary_Player_Child",focus+backwards*2,new Vector3(1,1,1));own.transform.SetParent(actor,true);yield return Wait(70);
        Check("Solid Player descendants ignored on Default layer",Mathf.Abs(Distance()-6.3f)<.04f);
        own.SetActive(false);blocker.SetActive(true);
        var dense=new List<GameObject>();for(int i=0;i<40;i++){var child=Box("Temporary_Ignored_Child",focus+backwards*(1+i*.025f),new Vector3(.05f,.05f,.05f));child.transform.SetParent(actor,true);dense.Add(child);}yield return Wait(40);
        Check("Dense ignored hits cannot hide a wall",Distance()<3&&((RaycastHit[])Field(follow,"castHits")).Length>16);
        foreach(var child in dense)child.SetActive(false);blocker.SetActive(false);yield return Wait(130);
        blocker.transform.position=camera.transform.position;blocker.SetActive(true);yield return Wait(5);Check("Obstacle appearing around camera is recovered",r.maxPenetration<=.003f);
        blocker.SetActive(false);yield return Wait(120);blocker.transform.position=focus;blocker.SetActive(true);yield return Wait(30);Check("Overlapped cast origin is recovered",r.maxPenetration<=.003f);
        blocker.SetActive(false);yield return Wait(120);
        Check("All monitored frames protect camera and near plane",r.safetyFrames>2000&&r.maxPenetration<=.003f);
        Check("No runtime errors",r.errors.Count==0);
    }
    IEnumerator Wait(int count){for(int i=0;i<count;i++)yield return null;}
    IEnumerator Press(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
    void Place(float x,float z){cc.enabled=false;actor.position=new Vector3(x,1.08f,z);actor.rotation=Quaternion.identity;cc.enabled=true;}
    void Orbit(float y,float p){Set(follow,"yaw",y);Set(follow,"pitch",p);}
    float Distance()=>Vector3.Distance(camera.transform.position,actor.position+new Vector3(0,1.2f,0));
    static GameObject Box(string name,Vector3 p,Vector3 size){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.position=p;g.transform.localScale=size;return g;}
    static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
    static object Call(object o,string n)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
    void Check(string name,bool value){r.checks.Add(new CameraCheck{name=name,passed=value});if(!value)throw new InvalidOperationException(name);}
    void OnLog(string m,string s,LogType t)
    {
        if(t!=LogType.Error&&t!=LogType.Exception&&t!=LogType.Assert)return;
        if(s.Contains("UnityEditor.Search.SearchDatabase")&&s.Contains("IndexationOnStartup"))r.editorStartupErrors.Add(m+"\n"+s);
        else r.errors.Add(m+"\n"+s);
    }
    void Finish(Exception e)
    {
        if(Finished)return;if(e!=null)r.errors.Add(e.ToString());monitor=false;
        if(actions!=null)actions.devices=devices;if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
        if(original!=null){InputSystem.settings=original;original.hideFlags=flags;}if(settings!=null)UnityEngine.Object.Destroy(settings);Time.captureDeltaTime=capture;
        Application.logMessageReceived-=OnLog;r.passed=e==null&&r.errors.Count==0;File.WriteAllText(reportPath,JsonUtility.ToJson(r,true));Finished=true;
    }
}
