using System;
using System.IO;
using System.Linq;
using HUSEscape.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ElevatorLobbySetup
{
    private static Transform root,facade;
    private static Material metal,trim,plaster,door,gold;
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();string report=args[Array.IndexOf(args,"-validationReport")+1];
        try
        {
            var scene=EditorSceneManager.OpenScene(CampusSetup.Floor,OpenSceneMode.Single);
            if(GameObject.Find("T3_ElevatorLobby_Reserved")!=null)throw new Exception("Elevator lobby already authored; refusing duplicate work.");
            if(GameObject.Find("T3_ExitVestibule")==null)throw new Exception("Inspect existing exit route first.");
            var entry=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().Single();var exit=UnityEngine.Object.FindObjectsByType<BuildingExit>().Single();
            metal=Load("Metal");trim=Load("TrimIvory");plaster=Load("Plaster");door=Load("Door");gold=Load("Key");
            var east=UnityEngine.Object.FindObjectsByType<Transform>().Single(t=>t.name=="Vestibule_SideWall"&&t.position.x>3);
            if(east.localScale!=new Vector3(.2f,5.4f,10.8f))throw new Exception("Unexpected east wall; stop before altering it.");
            root=new GameObject("T3_ElevatorLobby_Reserved").transform;
            east.position=new Vector3(3.2f,2.7f,63.3f);east.localScale=new Vector3(.2f,5.4f,3);
            Box("Vestibule_EastWall_North",new Vector3(3.2f,2.7f,70.4f),new Vector3(.2f,5.4f,4.4f),plaster,root,true);
            Box("Elevator_ShaftOpening_Infill_RemoveWhenEnabled",new Vector3(3.2f,2.7f,66.5f),new Vector3(.2f,5.4f,3.4f),plaster,root,true);
            var shaft=new GameObject("RESERVED_SHAFT_4x4m_NO_GAMEPLAY").transform;shaft.SetParent(root);shaft.position=new Vector3(5.2f,2.7f,66.5f);shaft.localScale=new Vector3(4,5.4f,4);
            Box("Elevator_LobbyFloorMarker",new Vector3(2.03f,.026f,66.5f),new Vector3(1.9f,.02f,3.5f),trim,root,false);
            facade=new GameObject("Elevator_Facade").transform;facade.SetParent(root);facade.SetPositionAndRotation(new Vector3(3.03f,0,66.5f),Quaternion.Euler(0,90,0));
            foreach(float x in new[]{-.525f,.525f})Box("Elevator_ClosedDoor",new Vector3(x,1.4f,0),new Vector3(1.04f,2.8f,.1f),metal,facade,true);
            foreach(float x in new[]{-1.12f,1.12f})Box("Elevator_DoorFrame",new Vector3(x,1.5f,-.025f),new Vector3(.15f,3,.18f),trim,facade,true);
            Box("Elevator_DoorHeader",new Vector3(0,2.96f,-.025f),new Vector3(2.39f,.15f,.18f),trim,facade,true);
            Box("Elevator_FloorDisplay",new Vector3(0,3.35f,-.06f),new Vector3(2.4f,.6f,.1f),door,facade,false);
            Sign("THANG MAY T3 / G",new Vector3(0,3.35f,-.12f),2.35f,.5f);
            Box("Elevator_CallPanel",new Vector3(-1.55f,1.25f,-.1f),new Vector3(.32f,.65f,.15f),metal,facade,true);
            var button=GameObject.CreatePrimitive(PrimitiveType.Sphere);button.name="Elevator_CallButton_Unavailable";button.transform.SetParent(facade,false);button.transform.localPosition=new Vector3(-1.55f,1.25f,-.22f);button.transform.localScale=Vector3.one*.12f;button.GetComponent<Renderer>().sharedMaterial=gold;UnityEngine.Object.DestroyImmediate(button.GetComponent<Collider>());
            Sign("GOI TANG / E",new Vector3(-1.55f,1.85f,-.12f),.85f,.35f);
            var zone=new GameObject("Elevator_CallApproach").AddComponent<BoxCollider>();zone.transform.SetParent(root);zone.transform.position=new Vector3(2,1.5f,68.05f);zone.size=new Vector3(1.5f,3,1.4f);zone.isTrigger=true;
            var panel=root.gameObject.AddComponent<ElevatorUnavailable>();var data=new SerializedObject(panel);data.FindProperty("entry").objectReferenceValue=entry;data.FindProperty("exit").objectReferenceValue=exit;data.FindProperty("interactionZone").objectReferenceValue=zone;data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save lobby.");
            File.WriteAllText(report,"{\"passed\":true,\"doorX\":3.03,\"doorZ\":66.5,\"callZ\":68.05,\"shaftReserved\":true,\"operational\":false}");EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(report,e.ToString());EditorApplication.Exit(1);}
    }
    private static Material Load(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_"+name+".mat")??throw new Exception("Missing existing material "+name);
    private static GameObject Box(string name,Vector3 p,Vector3 size,Material m,Transform parent,bool solid){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;if(!solid)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
    private static void Sign(string text,Vector3 p,float width,float height)
    {
        var g=new GameObject(text,typeof(RectTransform),typeof(Canvas));g.transform.SetParent(facade,false);g.transform.localPosition=p;g.transform.localScale=Vector3.one*.01f;g.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;g.GetComponent<RectTransform>().sizeDelta=new Vector2(width*100,height*100);
        var label=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));label.transform.SetParent(g.transform,false);var r=label.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;var t=label.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=28;t.resizeTextForBestFit=true;t.resizeTextMinSize=16;t.resizeTextMaxSize=36;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;
    }
}
