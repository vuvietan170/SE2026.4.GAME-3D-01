using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HUSEscape.World;
using HUSEscape.AI;
using HUSEscape.Inventory;
using HUSEscape.Persistence;
using HUSEscape.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CampusSetup
{
    public const string Floor = "Assets/_Project/Scenes/Floor01_Prototype.unity";
    public const string Campus = "Assets/_Project/Scenes/Campus_Prototype.unity";
    private static Transform architecture;
    private static Material stone, path, metal, wall, foliage, wood, display;
    public static void Run()
    {
        string[] args=Environment.GetCommandLineArgs();string report=args[Array.IndexOf(args,"-validationReport")+1];
        try
        {
            if(File.Exists(Campus))throw new InvalidOperationException("Never overwrite an existing Campus scene.");
            var source=EditorSceneManager.OpenScene(Floor,OpenSceneMode.Single);
            var quests=UnityEngine.Object.FindObjectsByType<QuestManager>().Single();var inventory=quests.Inventory;
            var camera=UnityEngine.Object.FindObjectsByType<Camera>().Single();
            if(quests.Definitions.Count!=17)throw new InvalidOperationException("Expected the accepted 17 objectives.");
            var floorEntry=quests.gameObject.AddComponent<JourneySceneEntry>();
            floorEntry.player=inventory.GetComponent<CharacterController>();floorEntry.inventory=inventory;floorEntry.viewCamera=camera;floorEntry.quests=quests;floorEntry.ui=quests.GetComponent<QuestPuzzleUI>();floorEntry.floorSave=quests.GetComponent<Floor01SaveSystem>();
            floorEntry.spawn=new GameObject("Floor01_JourneySpawn").transform;floorEntry.spawn.position=inventory.transform.position;
            stone=Load("Floor");path=Load("TileHall");metal=Load("Metal");wall=Load("Plaster");foliage=Load("Foliage");wood=Load("WoodWarm");display=Load("Door");
            architecture=new GameObject("Floor01_CampusExit").transform;
            Box("Exit_Portal",new Vector3(0,1.65f,61.83f),new Vector3(3.2f,3.3f,.12f),display);
            Box("Exit_LeftFrame",new Vector3(-1.72f,1.8f,61.7f),new Vector3(.2f,3.6f,.25f),metal);
            Box("Exit_RightFrame",new Vector3(1.72f,1.8f,61.7f),new Vector3(.2f,3.6f,.25f),metal);
            Box("Exit_Header",new Vector3(0,3.5f,61.7f),new Vector3(3.6f,.2f,.25f),metal);
            Sign("LOI RA CAMPUS",new Vector3(0,4.1f,61.6f),Quaternion.identity,3.8f,.65f);
            var exit=architecture.gameObject.AddComponent<BuildingExit>();var exitData=new SerializedObject(exit);exitData.FindProperty("entry").objectReferenceValue=floorEntry;exitData.ApplyModifiedPropertiesWithoutUndo();architecture.position=new Vector3(0,0,0);
            // Place the interaction origin at the portal, retaining world positions of its children.
            var children=architecture.Cast<Transform>().ToArray();foreach(var child in children)child.SetParent(null,true);architecture.position=new Vector3(0,1,61.8f);foreach(var child in children)child.SetParent(architecture,true);
            EditorSceneManager.MarkSceneDirty(source);EditorSceneManager.SaveScene(source);

            var campus=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(campus);
            architecture=new GameObject("Campus_Architecture").transform;
            Box("Campus_Ground",new Vector3(0,-.1f,30),new Vector3(58,.2f,64),stone);
            Box("Central_Walkway",new Vector3(0,.025f,30),new Vector3(6,.05f,58),path,false);
            Box("Courtyard",new Vector3(0,.035f,21),new Vector3(24,.04f,12),path,false);
            Box("West_Walkway",new Vector3(-12,.025f,34),new Vector3(4,.05f,34),path,false);
            Box("East_Walkway",new Vector3(17,.025f,35),new Vector3(12,.05f,40),path,false);
            Box("Cross_Path",new Vector3(2,.025f,32),new Vector3(32,.05f,4),path,false);
            Box("North_Path",new Vector3(8,.025f,48),new Vector3(22,.05f,4),path,false);
            Box("T3_Building",new Vector3(0,6,-6),new Vector3(20,12,12),wall);
            Box("T3_Entrance",new Vector3(0,1.8f,.12f),new Vector3(3.8f,3.6f,.2f),display);
            Box("T3_Canopy",new Vector3(0,3.8f,1.5f),new Vector3(7,.22f,3),metal);
            for(int x=-8;x<=8;x+=4)for(int y=5;y<=10;y+=3)Box("T3_Window",new Vector3(x,y,.15f),new Vector3(2,1.5f,.12f),display,false);
            Sign("TOA NHA T3",new Vector3(0,4.7f,.35f),Quaternion.Euler(0,180,0),5,.8f);
            Box("West_Boundary",new Vector3(-28.5f,1.5f,30),new Vector3(.3f,3,64),metal);
            Box("East_Boundary",new Vector3(28.5f,1.5f,30),new Vector3(.3f,3,64),metal);
            Box("North_Boundary",new Vector3(0,1.5f,62.05f),new Vector3(57,3,.3f),metal);
            Box("South_Boundary",new Vector3(0,1.5f,-2),new Vector3(57,3,.3f),metal);
            Box("Stealth_Cover_Wall",new Vector3(6,1.6f,30),new Vector3(.6f,3.2f,8),wall);
            foreach(float x in new[]{-22f,-18f,24f})foreach(float z in new[]{12f,26f,42f,55f})Tree(x,z);
            foreach(float z in new[]{12f,25f,43f})foreach(float x in new[]{-6f,7f})Bench(x,z);
            foreach(float x in new[]{-16f,24f})foreach(float z in new[]{18f,38f,55f})Lamp(x,z);
            var sun=new GameObject("Dusk_Directional",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=.8f;sun.color=new Color(.7f,.78f,1);sun.transform.rotation=Quaternion.Euler(25,-35,0);sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.4f,.48f);RenderSettings.fog=false;

            var actors=new GameObject("Campus_ActorRig");
            var p=UnityEngine.Object.Instantiate(inventory.gameObject,actors.transform);p.name="Player";
            var c=UnityEngine.Object.Instantiate(camera.gameObject,actors.transform);c.name="Main Camera";
            var q=UnityEngine.Object.Instantiate(quests.gameObject,actors.transform);q.name="Campus_QuestOwner";
            // The floor owner also parents puzzle props. Keep only its existing manager/UI components.
            foreach(Transform child in q.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            UnityEngine.Object.DestroyImmediate(q.GetComponent<JourneySceneEntry>());UnityEngine.Object.DestroyImmediate(q.GetComponent<Floor01SaveSystem>());
            var map=new Dictionary<UnityEngine.Object,UnityEngine.Object>();Map(inventory.gameObject,p,map);Map(camera.gameObject,c,map);Map(quests.gameObject,q,map);
            foreach(var behaviour in actors.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(behaviour==null)throw new InvalidOperationException("Missing production script in cloned actors.");
                var data=new SerializedObject(behaviour);var it=data.GetIterator();
                while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue!=null&&map.TryGetValue(it.objectReferenceValue,out var value))it.objectReferenceValue=value;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var viewData=new SerializedObject(q.GetComponent<QuestPuzzleUI>());viewData.FindProperty("saveSystem").objectReferenceValue=null;viewData.ApplyModifiedPropertiesWithoutUndo();
            var entry=new GameObject("Campus_Composition").AddComponent<JourneySceneEntry>();entry.isCampus=true;entry.actorRoot=actors;entry.player=p.GetComponent<CharacterController>();entry.inventory=p.GetComponent<PlayerInventory>();entry.viewCamera=c.GetComponent<Camera>();entry.quests=q.GetComponent<QuestManager>();entry.ui=q.GetComponent<QuestPuzzleUI>();
            entry.spawn=new GameObject("Campus_Spawn_T3").transform;entry.spawn.position=new Vector3(0,1.08f,8);p.transform.SetPositionAndRotation(entry.spawn.position,Quaternion.identity);
            Discovery(entry,"campus:courtyard","Khảo sát sân trường",new Vector3(0,1.08f,20),true);
            Discovery(entry,"campus:map","SƠ ĐỒ CAMPUS — T3 / SÂN TRƯỜNG / TRAINING OFFICE",new Vector3(-12,1.08f,30),false);
            Discovery(entry,"campus:direction","TRAINING OFFICE — HƯỚNG BẮC (CHƯA MỞ)",new Vector3(16,1.08f,48),false);
            var guard=new GameObject("Guard_Prototype",typeof(CharacterController),typeof(PatrolGuard));guard.transform.position=new Vector3(12,1.08f,22);var cc=guard.GetComponent<CharacterController>();cc.height=2;cc.radius=.35f;cc.center=Vector3.zero;
            var body=GameObject.CreatePrimitive(PrimitiveType.Capsule);body.name="Guard_Visual";body.transform.SetParent(guard.transform,false);body.transform.localScale=new Vector3(.65f,.9f,.65f);UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());body.GetComponent<Renderer>().sharedMaterial=display;
            var ai=guard.GetComponent<PatrolGuard>();ai.waypoints=new[]{new Vector3(12,1.08f,22),new Vector3(22,1.08f,22),new Vector3(22,1.08f,36),new Vector3(12,1.08f,36)}.Select((point,i)=>{var t=new GameObject("Patrol_Waypoint_"+i).transform;t.position=point;return t;}).ToArray();
            var aiData=new SerializedObject(ai);aiData.FindProperty("indicator").objectReferenceValue=body.GetComponent<Renderer>();aiData.ApplyModifiedPropertiesWithoutUndo();entry.guards=new[]{ai};
            actors.SetActive(false);EditorSceneManager.MarkSceneDirty(campus);EditorSceneManager.SaveScene(campus,Campus);AssetDatabase.SaveAssets();
            var scenes=EditorBuildSettings.scenes.ToList();foreach(string path in new[]{Floor,Campus})if(!scenes.Any(s=>s.path==path))scenes.Add(new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=scenes.ToArray();
            File.WriteAllText(report,"{\"passed\":true,\"scene\":\""+Campus+"\",\"objectives\":17,\"guard\":1}");EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(report,e.ToString());EditorApplication.Exit(1);}
    }
    private static Material Load(string suffix)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_"+suffix+".mat")??throw new InvalidOperationException("Missing existing material "+suffix);
    private static GameObject Box(string name,Vector3 p,Vector3 scale,Material material,bool solid=true)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(architecture,false);go.transform.position=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    private static void Tree(float x,float z)
    {
        Box("Planter",new Vector3(x,.2f,z),new Vector3(2.5f,.4f,2.5f),wall);
        Box("Tree_Trunk",new Vector3(x,1.6f,z),new Vector3(.35f,2.8f,.35f),wood);
        var crown=GameObject.CreatePrimitive(PrimitiveType.Sphere);crown.name="Tree_Crown";crown.transform.SetParent(architecture);crown.transform.position=new Vector3(x,3.6f,z);crown.transform.localScale=new Vector3(3,3.5f,3);crown.GetComponent<Renderer>().sharedMaterial=foliage;UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());
    }
    private static void Bench(float x,float z)
    {Box("Bench_Seat",new Vector3(x,.6f,z),new Vector3(2.4f,.18f,.7f),wall);Box("Bench_Back",new Vector3(x,1,z+.35f),new Vector3(2.4f,.8f,.12f),wall);foreach(float dx in new[]{-.8f,.8f})Box("Bench_Leg",new Vector3(x+dx,.27f,z),new Vector3(.2f,.54f,.6f),metal);}
    private static void Lamp(float x,float z)
    {Box("Lamp_Post",new Vector3(x,2.2f,z),new Vector3(.14f,4.4f,.14f),metal);Box("Lamp_Housing",new Vector3(x,4.4f,z),new Vector3(.6f,.22f,.6f),wall,false);var light=new GameObject("Campus_Lamp",typeof(Light)).GetComponent<Light>();light.transform.position=new Vector3(x,4.1f,z);light.type=LightType.Point;light.range=13;light.intensity=5;light.color=new Color(1,.83f,.63f);light.shadows=LightShadows.None;}
    private static void Discovery(JourneySceneEntry entry,string fact,string text,Vector3 position,bool automatic)
    {var target=new GameObject(fact).AddComponent<CampusDiscovery>();target.entry=entry;target.fact=fact;target.description=text;target.automatic=automatic;target.transform.position=position;if(!automatic){Box("Sign_Post",position+Vector3.right*2+Vector3.up*.3f,new Vector3(.12f,2.4f,.12f),metal);Sign(text,position+Vector3.right*2+Vector3.up*1.2f,Quaternion.identity,4,1.2f);}}
    private static void Sign(string text,Vector3 position,Quaternion rotation,float width,float height)
    {
        var go=new GameObject(text,typeof(RectTransform),typeof(Canvas));go.transform.SetParent(architecture);go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=Vector3.one*.01f;go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<RectTransform>().sizeDelta=new Vector2(width*100,height*100);
        var bg=new GameObject("Backing",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));bg.transform.SetParent(go.transform,false);Stretch(bg.GetComponent<RectTransform>());bg.GetComponent<Image>().color=new Color(.035f,.06f,.09f);
        var label=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));label.transform.SetParent(go.transform,false);Stretch(label.GetComponent<RectTransform>());label.transform.localPosition=new Vector3(0,0,-1);var t=label.GetComponent<Text>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=30;t.resizeTextForBestFit=true;t.resizeTextMinSize=16;t.resizeTextMaxSize=40;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;
    }
    private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static void Map(GameObject source,GameObject target,Dictionary<UnityEngine.Object,UnityEngine.Object> map)
    {
        map[source]=target;
        foreach(var c in source.GetComponents<Component>())
        {if(c==null)continue;var originals=source.GetComponents(c.GetType());var clones=target.GetComponents(c.GetType());int i=Array.IndexOf(originals,c);if(i<clones.Length)map[c]=clones[i];}
        for(int i=0;i<Math.Min(source.transform.childCount,target.transform.childCount);i++)Map(source.transform.GetChild(i).gameObject,target.transform.GetChild(i).gameObject,map);
    }
}
