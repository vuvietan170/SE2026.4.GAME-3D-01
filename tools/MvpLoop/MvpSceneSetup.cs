#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HUSEscape.World;
using HUSEscape.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MvpSceneSetup
{
    public static void Run()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Campus_Prototype.unity",OpenSceneMode.Single);
            if(UnityEngine.Object.FindObjectsByType<MainGateExit>(FindObjectsInactive.Include).Length!=0)throw new Exception("Gate already authored; refusing duplicate setup.");
            var entry=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().Single();
            var root=new GameObject("MVP_MainGate_Proposed");root.transform.position=new Vector3(0,1.08f,59.3f);
            var gate=root.AddComponent<MainGateExit>();gate.entry=entry;
            var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_Metal.mat");
            foreach(var spec in new[]{(new Vector3(-3.2f,1.8f,61.2f),new Vector3(.4f,3.6f,.45f)),
                (new Vector3(3.2f,1.8f,61.2f),new Vector3(.4f,3.6f,.45f)),
                (new Vector3(0,3.7f,61.2f),new Vector3(6.8f,.4f,.45f))})
                Box(root,"Gate_Frame",spec.Item1,spec.Item2,metal);
            for(int x=-5;x<=5;x++)Box(root,"Gate_Bar",new Vector3(x*.5f,1.5f,61.2f),new Vector3(.08f,3,.12f),metal);
            Sign(root,"CONG CHINH — E",new Vector3(0,4.3f,60.9f));
            Sign(root,"CONG CHINH ↑",new Vector3(0,2.8f,49.8f));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            var office=EditorSceneManager.OpenScene("Assets/_Project/Scenes/TrainingOffice_Prototype.unity",OpenSceneMode.Single);
            var computer=UnityEngine.Object.FindObjectsByType<OfficeDiscovery>().Single(d=>d.fact=="office:computer");
            computer.content="Đã xác nhận tuyến đường ra cổng chính. Trở lại Campus qua cửa sảnh, theo đường trung tâm về phía Bắc và nhấn E tại Cổng chính để hoàn thành hành trình.";
            EditorSceneManager.MarkSceneDirty(office);EditorSceneManager.SaveScene(office);
            File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"mvp-scene-setup.txt"),"PASS: authored one Campus primitive gate; updated Office computer route; Build Settings unchanged\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void RepairSigns()
    {
        var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Campus_Prototype.unity",OpenSceneMode.Single);
        var gate=UnityEngine.Object.FindObjectsByType<MainGateExit>().Single();
        foreach(var label in gate.GetComponentsInChildren<TextMesh>())
        {
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial=label.font.material;
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
    static void Box(GameObject root,string name,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,true);
        go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
    }
    static void Sign(GameObject root,string text,Vector3 position)
    {
        var go=new GameObject("MainGate_RouteSign",typeof(TextMesh));go.transform.SetParent(root.transform,true);go.transform.position=position;
        var label=go.GetComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
        label.fontSize=64;label.characterSize=.08f;label.color=Color.white;
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<Renderer>().sharedMaterial=label.font.material;
    }
}
#endif
