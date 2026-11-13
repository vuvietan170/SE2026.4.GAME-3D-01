using System;
using System.IO;
using System.Linq;
using HUSEscape.World;
using HUSEscape.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad] public static class TrainingOfficeValidation
{
    private static TrainingOfficeProbe probe;
    static TrainingOfficeValidation(){if(SessionState.GetBool("OfficeProbe",false))EditorApplication.update+=Observe;}
    public static void Tests()
    {
        var office=EditorSceneManager.OpenScene(Office,OpenSceneMode.Single);
        var all=office.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        if(all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))!=0)throw new Exception("Missing Office script");
        var binding=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().Single();
        if(!binding.isTrainingOffice||binding.player==null||binding.viewCamera==null||binding.inventory==null||binding.quests==null||binding.ui==null||binding.spawn==null||UnityEngine.Object.FindObjectsByType<OfficeDiscovery>().Length!=3||!EditorBuildSettings.scenes.Any(s=>s.path==Office&&s.enabled))throw new Exception("Invalid Office composition/build reference");
        foreach(var component in all.SelectMany(t=>t.GetComponents<MonoBehaviour>()))
        {
            var data=new SerializedObject(component);var it=data.GetIterator();
            while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference)
            {
                var go=it.objectReferenceValue is GameObject obj?obj:it.objectReferenceValue is Component c?c.gameObject:null;
                if(go!=null&&go.scene.IsValid()&&go.scene!=office)throw new Exception("Office contains cross-scene reference: "+it.propertyPath);
            }
        }
        var campus=EditorSceneManager.OpenScene(CampusSetup.Campus,OpenSceneMode.Single);
        // Keep the probe out of Campus's unsaved Play Mode scene snapshot, otherwise
        // returning to Campus can instantiate a second probe from Unity's temporary scene copy.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        probe=new GameObject("Temporary_Office_Probe").AddComponent<TrainingOfficeProbe>();probe.reportPath=Report();var a=Environment.GetCommandLineArgs();probe.fixture=a[Array.IndexOf(a,"-checkpointDirectory")+1];SceneManager.SetActiveScene(campus);SessionState.SetBool("OfficeProbe",true);EditorApplication.update-=Observe;EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();
    }
    private static void Observe(){if(!EditorApplication.isPlaying)return;if(probe==null)probe=Resources.FindObjectsOfTypeAll<TrainingOfficeProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());if(probe==null||!probe.Finished)return;SessionState.SetBool("OfficeProbe",false);EditorApplication.update-=Observe;EditorApplication.Exit(probe.Passed?0:1);}
    public const string Office="Assets/_Project/Scenes/TrainingOffice_Prototype.unity";
    private static Transform root;
    private static Material plaster,metal,wood,blue;
    public static void Run()
    {
        string output=Report();
        try
        {
            if(File.Exists(Office))throw new Exception("Office already exists; refusing overwrite");
            var campus=EditorSceneManager.OpenScene(CampusSetup.Campus,OpenSceneMode.Single);
            var source=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().Single();
            if(GameObject.Find("TrainingOffice_CampusEntrance")!=null)throw new Exception("Campus entry already authored");
            plaster=Mat("Plaster");metal=Mat("Metal");wood=Mat("WoodWarm");blue=Mat("Door");
            root=new GameObject("TrainingOffice_CampusEntrance").transform;
            Box("Office_Exterior",new Vector3(16,4,58),new Vector3(12,8,8),plaster);
            Box("Office_EntranceDoor",new Vector3(16,1.6f,53.9f),new Vector3(2.8f,3.2f,.15f),blue);
            Box("Office_EntryPath",new Vector3(16,.035f,51),new Vector3(3,.04f,6),wood,false);
            Sign("PHÒNG ĐÀO TẠO",new Vector3(16,3.9f,53.78f),Quaternion.identity,7,.7f);
            var portal=new GameObject("Office_Enter_E").AddComponent<TrainingOfficeEntrance>();portal.transform.SetParent(root);portal.transform.position=new Vector3(16,1.08f,52.7f);portal.entry=source;
            foreach(var text in campus.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Text>()).Where(t=>t.text.StartsWith("TRAINING OFFICE"))) {text.text="PHÒNG ĐÀO TẠO — HƯỚNG BẮC";EditorUtility.SetDirty(text);}
            EditorSceneManager.MarkSceneDirty(campus);EditorSceneManager.SaveScene(campus);
            var office=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(office);
            root=new GameObject("TrainingOffice_Architecture").transform;
            Box("Office_Floor",new Vector3(0,-.1f,12),new Vector3(16,.2f,24),Mat("TileRoom"));
            Box("Office_Ceiling",new Vector3(0,5.5f,12),new Vector3(16,.2f,24),plaster);
            foreach(float x in new[]{-8f,8f})Box("Office_SideWall",new Vector3(x,2.7f,12),new Vector3(.2f,5.4f,24),plaster);
            Box("Office_BackWall",new Vector3(0,2.7f,24),new Vector3(16,5.4f,.2f),plaster);
            foreach(float x in new[]{-4.8f,4.8f})Box("Office_EntranceWall",new Vector3(x,2.7f,0),new Vector3(6.4f,5.4f,.2f),plaster);
            Box("Office_EntranceLintel",new Vector3(0,4.5f,0),new Vector3(3.2f,1.8f,.2f),plaster);
            Sign("PHÒNG ĐÀO TẠO",new Vector3(0,3.4f,1),Quaternion.Euler(0,180,0),5,.6f);
            Box("Reception_Counter",new Vector3(-3,.8f,12),new Vector3(5,1.6f,1),wood);
            Box("Administrative_Document",new Vector3(-3,1.62f,11.8f),new Vector3(.6f,.03f,.4f),plaster,false);
            Box("NoticeBoard",new Vector3(-7.85f,1.9f,7),new Vector3(.12f,1.8f,3),blue,false);
            Sign("THÔNG BÁO\nHỒ SƠ / HƯỚNG DẪN RA CỔNG",new Vector3(-7.75f,1.9f,7),Quaternion.Euler(0,-90,0),2.8f,1.5f);
            foreach(float x in new[]{-4f,4f})
            {
                Box("Work_Desk",new Vector3(x,.8f,18),new Vector3(2.6f,.14f,1.2f),wood);
                foreach(float dx in new[]{-1.1f,1.1f})Box("Desk_Leg",new Vector3(x+dx,.38f,18),new Vector3(.12f,.76f,.9f),metal);
                Box("Computer_Screen",new Vector3(x,1.3f,18.2f),new Vector3(.9f,.6f,.1f),blue,false);
                Box("Computer_Stand",new Vector3(x,.99f,18.2f),new Vector3(.12f,.3f,.12f),metal,false);
                Box("Keyboard",new Vector3(x,.9f,17.7f),new Vector3(.7f,.04f,.25f),metal,false);
                Box("Chair_Seat",new Vector3(x,.5f,19.4f),new Vector3(.7f,1,.7f),wood);
                Box("Chair_Back",new Vector3(x,1.15f,19.7f),new Vector3(.7f,.7f,.12f),wood,false);
            }
            foreach(float z in new[]{16f,20f})Box("File_Cabinet",new Vector3(-7.1f,1.1f,z),new Vector3(1.2f,2.2f,2.2f),metal);
            Box("Waiting_Bench",new Vector3(5,.45f,7),new Vector3(3,.9f,.7f),wood);
            foreach(float z in new[]{5f,13f,21f})
            {
                Box("Ceiling_Lamp",new Vector3(0,5.25f,z),new Vector3(2,.1f,.5f),plaster,false);
                var light=new GameObject("Office_Light").AddComponent<Light>();light.transform.SetParent(root);light.transform.position=new Vector3(0,4.5f,z);light.type=LightType.Point;light.range=16;light.intensity=5;light.shadows=LightShadows.None;
            }
            RenderSettings.ambientLight=new Color(.5f,.5f,.55f);
            var actors=UnityEngine.Object.Instantiate(source.actorRoot);actors.name="TrainingOffice_ActorRig";SceneManager.MoveGameObjectToScene(actors,office);
            var entry=new GameObject("TrainingOffice_Composition").AddComponent<JourneySceneEntry>();entry.isTrainingOffice=true;entry.actorRoot=actors;
            entry.player=actors.GetComponentInChildren<CharacterController>();entry.inventory=entry.player.GetComponent<HUSEscape.Inventory.PlayerInventory>();entry.viewCamera=actors.GetComponentInChildren<Camera>();entry.quests=actors.GetComponentInChildren<QuestManager>();entry.ui=actors.GetComponentInChildren<QuestPuzzleUI>();
            entry.spawn=new GameObject("TrainingOffice_Spawn").transform;entry.spawn.position=new Vector3(0,1.08f,3.7f);entry.player.transform.SetPositionAndRotation(entry.spawn.position,Quaternion.identity);
            var exit=new GameObject("Office_ReturnToCampus_E").AddComponent<TrainingOfficeEntrance>();exit.transform.position=new Vector3(0,1.08f,1.2f);exit.entry=entry;exit.returnToCampus=true;
            Discovery(entry,"office:notice","BẢNG THÔNG BÁO", "Lối ra cổng chính nằm sau khuôn viên phía Bắc. Đọc tài liệu tại quầy để biết tuyến đi được phép.",new Vector3(-6.8f,1.08f,7),false);
            Discovery(entry,"office:document","TÀI LIỆU HÀNH CHÍNH","Hướng dẫn mẫu: từ Phòng Đào tạo, trở lại Campus và đi theo biển cổng chính. Xác nhận tuyến đường tại máy tính trước khi tiếp tục.",new Vector3(-3,1.3f,11.3f),true);
            Discovery(entry,"office:computer","MÁY TÍNH PHÒNG ĐÀO TẠO","Đã xác nhận tuyến đường ra cổng chính. Mục tiêu tiếp theo: Đến cổng chính. Khu vực Main Gate chưa được triển khai trong bản prototype này.",new Vector3(4,1.3f,17.3f),false);
            if(!EditorSceneManager.SaveScene(office,Office))throw new Exception("Office save failed");
            if(!EditorBuildSettings.scenes.Any(s=>s.path==Office))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(Office,true)}).ToArray();
            File.WriteAllText(output,"{\"passed\":true,\"scene\":\""+Office+"\"}");EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(output,e.ToString());EditorApplication.Exit(1);}
    }
    private static void Discovery(JourneySceneEntry entry,string fact,string title,string content,Vector3 p,bool useF){var d=new GameObject(title).AddComponent<OfficeDiscovery>();d.entry=entry;d.fact=fact;d.title=title;d.content=content;d.useF=useF;d.transform.position=p;}
    private static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_"+name+".mat")??throw new Exception("Missing material "+name);
    private static void Box(string name,Vector3 p,Vector3 size,Material mat,bool solid=true){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root);g.transform.position=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
    private static void Sign(string text,Vector3 p,Quaternion rotation,float w,float h){var g=new GameObject(text,typeof(RectTransform),typeof(Canvas));g.transform.SetParent(root);g.transform.SetPositionAndRotation(p,rotation);g.transform.localScale=Vector3.one*.01f;g.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;g.GetComponent<RectTransform>().sizeDelta=new Vector2(w*100,h*100);var label=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));label.transform.SetParent(g.transform,false);var r=label.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;var t=label.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=24;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;}
    public static string Report(){var a=Environment.GetCommandLineArgs();return a[Array.IndexOf(a,"-validationReport")+1];}
}
