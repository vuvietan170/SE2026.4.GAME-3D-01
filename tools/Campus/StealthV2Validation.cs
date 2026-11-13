using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HUSEscape.AI;
using HUSEscape.World;
using HUSEscape.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Installed only in the reused D: verification copy. No Floor/Quest regression suite.
[InitializeOnLoad] public static class StealthV2Validation
{
    private static JourneySceneEntry entry;
    private static PatrolGuard guard, dog;
    private static float since;
    private static int phase, checks;
    private static string output;
    private static Vector3 remembered;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static StealthV2Validation(){if(SessionState.GetBool("StealthV2",false))EditorApplication.update+=Tick;}
    public static void Run()
    {
        output=ReportPath();
        try
        {
            var scene=EditorSceneManager.OpenScene(CampusSetup.Campus,OpenSceneMode.Single);
            if(GameObject.Find("Campus_StealthV2")!=null)throw new Exception("Already authored; refusing duplicate work.");
            entry=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().Single();guard=entry.guards.Single();
            var root=new GameObject("Campus_StealthV2").transform;
            var g=new GameObject("Dog_Prototype",typeof(CharacterController),typeof(PatrolGuard));g.transform.SetParent(root);g.transform.position=new Vector3(10,.65f,16);
            var cc=g.GetComponent<CharacterController>();cc.height=1.2f;cc.radius=.30f;cc.stepOffset=.2f;
            dog=g.GetComponent<PatrolGuard>();
            var visual=new GameObject("Dog_Visual_ReplaceWithModel").transform;visual.SetParent(g.transform,false);
            var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_Metal.mat");
            var gold=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_Key.mat");
            if(metal==null||gold==null)throw new Exception("Existing materials unavailable");
            Part("Dog_Body",new Vector3(0,0,0),new Vector3(.45f,.4f,.9f),visual,metal);
            Part("Dog_Neck",new Vector3(0,.16f,.4f),new Vector3(.3f,.38f,.25f),visual,metal);
            Part("Dog_Head",new Vector3(0,.3f,.57f),new Vector3(.35f,.32f,.4f),visual,metal);
            Part("Dog_Muzzle",new Vector3(0,.22f,.81f),new Vector3(.24f,.18f,.19f),visual,metal);
            foreach(float x in new[]{-.15f,.15f})
            {
                Part("Dog_Ear",new Vector3(x,.55f,.5f),new Vector3(.1f,.23f,.15f),visual,metal);
                foreach(float z in new[]{-.3f,.3f})Part("Dog_Leg",new Vector3(x,-.35f,z),new Vector3(.12f,.5f,.13f),visual,metal);
            }
            var tail=Part("Dog_Tail",new Vector3(0,.22f,-.62f),new Vector3(.1f,.12f,.5f),visual,metal);tail.transform.localRotation=Quaternion.Euler(30,0,0);
            var collar=Part("Dog_AlarmIndicator",new Vector3(0,.17f,.38f),new Vector3(.38f,.12f,.16f),visual,gold);
            dog.waypoints=new[]{new Vector3(10,.65f,16),new Vector3(20,.65f,16),new Vector3(20,.65f,20),new Vector3(10,.65f,20)}.Select((p,i)=>{var t=new GameObject("Dog_Waypoint_"+i).transform;t.SetParent(root);t.position=p;return t;}).ToArray();
            var data=new SerializedObject(dog);data.FindProperty("isDog").boolValue=true;data.FindProperty("speed").floatValue=2.2f;data.FindProperty("chaseSpeed").floatValue=4;data.FindProperty("sightRange").floatValue=12;data.FindProperty("indicator").objectReferenceValue=collar.GetComponent<Renderer>();data.FindProperty("alarmRecipient").objectReferenceValue=guard;data.ApplyModifiedPropertiesWithoutUndo();
            entry.guards=new[]{guard,dog};EditorUtility.SetDirty(entry);
            var hud=root.gameObject.AddComponent<CampusStealthHUD>();data=new SerializedObject(hud);data.FindProperty("guard").objectReferenceValue=guard;data.FindProperty("dog").objectReferenceValue=dog;data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");
            File.WriteAllText(output,"{\"passed\":true,\"authoring\":true}");EditorApplication.Exit(0);
        }
        catch(Exception e){Fail(e);}
    }
    private static GameObject Part(string name,Vector3 p,Vector3 scale,Transform parent,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    public static void Tests()
    {
        output=ReportPath();SessionState.SetString("StealthV2Report",output);SessionState.SetBool("StealthV2",true);
        EditorSceneManager.OpenScene(CampusSetup.Campus,OpenSceneMode.Single);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(output==null)output=SessionState.GetString("StealthV2Report","");
            if(phase==0)
            {
                entry=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().SingleOrDefault();if(entry==null||entry.Session==null)return;
                Time.captureDeltaTime=.02f;guard=entry.guards.Single(a=>!a.IsDog);dog=entry.guards.Single(a=>a.IsDog);
                Check(entry.guards.Length==2&&guard.waypoints.Length==4&&dog.waypoints.Length==4,"Explicit guard/dog composition");
                Check(dog.GetComponentsInChildren<Transform>().Count(t=>t.name=="Dog_Leg")==4,"Four-legged visual");
                Check(entry.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0,"No missing scripts");
                entry.player.GetComponent<PlayerController1>().enabled=false;
                Place(entry.player,new Vector3(-20,1.08f,40),Quaternion.identity);since=Time.time;phase=-1;return;
            }
            if(phase==-1)
            {
                if(Time.time-since<12)return;
                Check(dog.State==GuardState.Patrol&&dog.CompletedWaypoints>=2&&dog.GetComponent<CharacterController>().isGrounded,"Dog physically follows authored route without blocking");phase=10;
            }
            if(phase==10)
            {
                Place(guard.GetComponent<CharacterController>(),new Vector3(12,1.08f,30),Quaternion.Euler(0,270,0));Place(entry.player,new Vector3(0,1.08f,30),Quaternion.identity);
                Check(!guard.CanSee(entry.player.transform),"Real cover wall blocks guard sight");
                Place(dog.GetComponent<CharacterController>(),new Vector3(12,.65f,30),Quaternion.Euler(0,270,0));
                Check(!dog.CanSee(entry.player.transform)&&!dog.CanHear(entry.player.transform,6.4f,false),"Cover blocks sight and attenuates distant sound");
                Place(dog.GetComponent<CharacterController>(),new Vector3(12,.65f,20),Quaternion.identity);Place(entry.player,new Vector3(12,1.08f,28),Quaternion.identity);
                Check(dog.CanHear(entry.player.transform,6.4f,false)&&!dog.CanHear(entry.player.transform,4,false)&&!dog.CanHear(entry.player.transform,2,true)&&!dog.CanHear(entry.player.transform,0,false),"Sprint louder than walking/crouch; stationary silent");
                Place(guard.GetComponent<CharacterController>(),new Vector3(12,1.08f,30),Quaternion.identity);Place(entry.player,new Vector3(12,1.08f,40),Quaternion.identity);
                var animator=entry.player.GetComponentInChildren<Animator>();animator.SetBool("IsCrouching",false);Check(guard.CanSee(entry.player.transform),"Standing target visible");animator.SetBool("IsCrouching",true);Check(!guard.CanSee(entry.player.transform),"Crouch reduces visual detection range");animator.SetBool("IsCrouching",false);
                dog.enabled=false;Place(entry.player,new Vector3(12,1.08f,36),Quaternion.identity);since=Time.time;phase=1;return;
            }
            if(phase==1&&Time.time-since>.4f){Check(guard.State==GuardState.Suspicious,"Sight creates Suspicious");phase=2;return;}
            if(phase==2&&Time.time-since>2)
            {
                Check(guard.State==GuardState.Alert,"Sustained sight creates Alert");Check(guard.transform.position.z>30.2f,"Alert physically pursues");remembered=guard.LastKnownPosition;
                Place(entry.player,new Vector3(-20,1.08f,40),Quaternion.identity);since=Time.time;phase=3;return;
            }
            if(phase==3&&Time.time-since>3.3f)
            {
                Check(!guard.HasLineOfSight&&guard.State==GuardState.Search,"Lost sight enters Search");Check(guard.LastKnownPosition==remembered,"Hidden player cannot update last known position");since=Time.time;phase=4;return;
            }
            if(phase==4&&Time.time-since>4.2f)
            {
                Check(guard.State==GuardState.Return||guard.State==GuardState.Patrol,"Search expires into Return");since=Time.time;phase=5;return;
            }
            if(phase==5&&Time.time-since>12)
            {
                Check(guard.State==GuardState.Patrol&&guard.CompletedWaypoints>0,"Return rejoins physical patrol");
                Place(guard.GetComponent<CharacterController>(),new Vector3(12,1.08f,30),Quaternion.Euler(0,270,0));dog.enabled=true;
                Place(dog.GetComponent<CharacterController>(),new Vector3(0,.65f,30),Quaternion.identity);Place(entry.player,new Vector3(0,1.08f,35),Quaternion.identity);since=Time.time;phase=6;return;
            }
            if(phase==6&&Time.time-since>2)
            {
                Check(dog.State==GuardState.Alert&&dog.IsBarking,"Dog detects and visually barks");Check(!guard.HasLineOfSight&&guard.State==GuardState.Suspicious,"Bark alerts explicit guard without live sight");
                Place(entry.player,new Vector3(-20,1.08f,40),Quaternion.identity);since=Time.time;phase=7;return;
            }
            if(phase==7&&Time.time-since>3.4f)
            {
                Check(dog.State==GuardState.Search&&!dog.HasLineOfSight,"Dog loses hidden player and searches");
                // Force an old chase timestamp, exercising the production chase limit branch without waiting eight seconds.
                typeof(PatrolGuard).GetMethod("SetState",Private).Invoke(guard,new object[]{GuardState.Alert});typeof(PatrolGuard).GetField("chaseSince",Private).SetValue(guard,Time.time-20);phase=8;return;
            }
            if(phase==8)
            {
                Check(guard.State==GuardState.Return,"Chase time limit forces Return");
                SessionState.SetBool("StealthV2",false);EditorApplication.update-=Tick;File.WriteAllText(output,"{\"passed\":true,\"checks\":"+checks+",\"unity\":\""+Application.unityVersion+"\"}");EditorApplication.Exit(0);
            }
        }
        catch(Exception e){Fail(e);}
    }
    private static void Place(CharacterController cc,Vector3 p,Quaternion r){cc.enabled=false;cc.transform.SetPositionAndRotation(p,r);cc.enabled=true;Physics.SyncTransforms();}
    private static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);Debug.Log("Stealth V2 PASS: "+name);}
    private static string ReportPath(){var a=Environment.GetCommandLineArgs();return a[Array.IndexOf(a,"-validationReport")+1];}
    private static void Fail(Exception e){SessionState.SetBool("StealthV2",false);EditorApplication.update-=Tick;if(output==null)output=ReportPath();File.WriteAllText(output,"{\"passed\":false,\"checks\":"+checks+",\"error\":"+UnityString(e.ToString())+"}");EditorApplication.Exit(1);}
    private static string UnityString(string value)=>"\""+value.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";
}
