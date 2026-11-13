using System;
using System.IO;
using System.Linq;
using HUSEscape.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-time editor authoring of the ground-level T3 exit. Never runs during gameplay.</summary>
public static class ExitVestibuleSetup
{
    private static Transform root;
    private static Material plaster, trim, metal, door, floor, yellow;
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();string report=args[Array.IndexOf(args,"-validationReport")+1];
        try
        {
            var scene=EditorSceneManager.OpenScene(CampusSetup.Floor,OpenSceneMode.Single);
            if(GameObject.Find("T3_ExitVestibule")!=null)throw new InvalidOperationException("Vestibule already authored; refusing to run twice.");
            var oldWall=GameObject.Find("Corridor_End_Wall").transform;
            if(Mathf.Abs(GameObject.Find("Corridor_Floor").GetComponent<Collider>().bounds.max.y)>.01f||oldWall.localScale!=new Vector3(14.4f,5.4f,.2f))throw new InvalidOperationException("Unexpected floor elevation or end-wall layout; inspect before editing.");
            var exit=UnityEngine.Object.FindObjectsByType<BuildingExit>().Single();var entry=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().Single();
            if(entry.isCampus||entry.quests.Definitions.Count!=17)throw new InvalidOperationException("Expected original floor composition.");
            plaster=Load("Plaster");trim=Load("TrimIvory");metal=Load("Metal");door=Load("Door");floor=Load("TileHall");yellow=Load("Key");
            root=new GameObject("T3_ExitVestibule").transform;
            oldWall.position=new Vector3(-4.65f,2.7f,62.1f);oldWall.localScale=new Vector3(5.1f,5.4f,.2f);
            Box("Corridor_End_Wall_Right",new Vector3(4.65f,2.7f,62.1f),new Vector3(5.1f,5.4f,.2f),plaster);
            Box("Corridor_End_Wall_Lintel",new Vector3(0,4.5f,62.1f),new Vector3(4.2f,1.8f,.2f),plaster);
            var skirts=UnityEngine.Object.FindObjectsByType<Transform>().Where(t=>t.name=="Skirting"&&Mathf.Abs(t.position.x)<.01f&&Mathf.Abs(t.position.z-62.1f)<.2f&&Mathf.Abs(t.lossyScale.x-14.4f)<.01f).ToArray();
            if(skirts.Length!=2)throw new InvalidOperationException("Unexpected exit skirting; refusing broad edits.");
            foreach(var skirt in skirts){var p=skirt.position;skirt.position=new Vector3(-4.65f,p.y,p.z);skirt.localScale=new Vector3(5.1f,.24f,.04f);Box("Exit_Skirting_Right",new Vector3(4.65f,p.y,p.z),new Vector3(5.1f,.24f,.04f),trim,false);}
            Box("Vestibule_Floor_Y0",new Vector3(0,-.1f,67.15f),new Vector3(6.4f,.2f,10.5f),floor);
            Box("Vestibule_Ceiling",new Vector3(0,5.5f,67.2f),new Vector3(6.6f,.2f,10.8f),plaster);
            foreach(float x in new[]{-3.2f,3.2f})Box("Vestibule_SideWall",new Vector3(x,2.7f,67.2f),new Vector3(.2f,5.4f,10.8f),plaster);
            foreach(float x in new[]{-2.51f,2.51f})Box("T3_Exit_EndJamb",new Vector3(x,2.7f,72.15f),new Vector3(1.38f,5.4f,.2f),plaster);
            Box("T3_Exit_EndLintel",new Vector3(0,4.5f,72.15f),new Vector3(3.64f,1.8f,.2f),plaster);
            foreach(float x in new[]{-2.18f,2.18f})Box("Entrance_DoorJamb",new Vector3(x,1.8f,62.1f),new Vector3(.16f,3.6f,.28f),trim);
            Box("Entrance_DoorHeader",new Vector3(0,3.52f,62.1f),new Vector3(4.52f,.16f,.28f),trim);
            foreach(int side in new[]{-1,1})
            {
                var hinge=new GameObject(side<0?"Entrance_Left_HeldOpen":"Entrance_Right_HeldOpen").transform;hinge.SetParent(root);hinge.position=new Vector3(side*2.1f,0,62.25f);
                var leaf=Box("Entrance_DoorLeaf",Vector3.zero,new Vector3(2,3.3f,.12f),door);leaf.transform.SetParent(hinge,false);leaf.transform.localPosition=new Vector3(-side,1.65f,0);
                var handle=Box("Entrance_Handle",Vector3.zero,new Vector3(.08f,.4f,.08f),metal,false);handle.transform.SetParent(hinge,false);handle.transform.localPosition=new Vector3(-side*1.8f,1.15f,-.15f);
                hinge.rotation=Quaternion.Euler(0,side*90,0);
            }
            exit.transform.position=new Vector3(0,1,72);
            Box("Exit_PushBar",new Vector3(0,1.25f,71.9f),new Vector3(2.3f,.09f,.16f),metal,false);
            var zone=new GameObject("Exit_InteractionZone").AddComponent<BoxCollider>();zone.transform.SetParent(exit.transform);zone.transform.position=new Vector3(0,1.5f,70.7f);zone.size=new Vector3(3.4f,3,2.4f);zone.isTrigger=true;
            var data=new SerializedObject(exit);data.FindProperty("interactionZone").objectReferenceValue=zone;data.ApplyModifiedPropertiesWithoutUndo();
            Sign("LOI RA CAMPUS ->",new Vector3(-6.985f,3.1f,59),Quaternion.Euler(0,-90,0),3.8f,.85f);
            Sign("SANH THOAT T3 / TANG TRET\nDI QUA CUA MO - CAMPUS THANG PHIA TRUOC",new Vector3(0,4.2f,61.98f),Quaternion.identity,5.8f,.85f);
            Sign("LOI RA CAMPUS ->\nDI VE PHIA CUOI HANH LANG LAB",new Vector3(-6.85f,3.1f,54),Quaternion.Euler(0,-90,0),4.8f,1.2f);
            Sign("CUA RA TOA NHA T3\nE - DEN CAMPUS SAU KHI HOAN THANH 17 MUC TIEU",new Vector3(-3.085f,2.8f,68),Quaternion.Euler(0,-90,0),3.6f,1.2f);
            foreach(float z in new[]{55.5f,58.5f,60.2f,65.5f})
            {
                Box("ExitRoute_ArrowShaft",new Vector3(0,.024f,z),new Vector3(.12f,.02f,1),yellow,false);
                foreach(int side in new[]{-1,1}){var wing=Box("ExitRoute_ArrowHead",new Vector3(side*.22f,.024f,z+.3f),new Vector3(.12f,.02f,.65f),yellow,false);wing.transform.rotation=Quaternion.Euler(0,-side*45,0);}
            }
            foreach(float z in new[]{65.2f,70.3f})
            {
                Box("Exit_CeilingFixture",new Vector3(0,5.32f,z),new Vector3(1.4f,.08f,.5f),trim,false);
                var light=new GameObject("Exit_Vestibule_Light",typeof(Light)).GetComponent<Light>();light.transform.SetParent(root);light.transform.position=new Vector3(0,4.8f,z);light.type=LightType.Point;light.range=10;light.intensity=4;light.shadows=LightShadows.None;
            }
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save authored floor scene.");
            File.WriteAllText(report,"{\"passed\":true,\"floorSurfaceY\":0,\"entranceZ\":62.1,\"finalDoorZ\":72,\"stairs\":false,\"groundLevelVestibule\":true}");EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(report,e.ToString());EditorApplication.Exit(1);}
    }
    private static Material Load(string suffix)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_"+suffix+".mat")??throw new InvalidOperationException("Missing existing material "+suffix);
    private static GameObject Box(string name,Vector3 position,Vector3 scale,Material material,bool solid=true){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    private static void Sign(string text,Vector3 p,Quaternion rotation,float width,float height)
    {
        var go=new GameObject("ExitRoute_Sign",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(root);go.transform.SetPositionAndRotation(p,rotation);go.transform.localScale=Vector3.one*.01f;go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<RectTransform>().sizeDelta=new Vector2(width*100,height*100);
        var bg=new GameObject("Backing",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));bg.transform.SetParent(go.transform,false);Stretch(bg.GetComponent<RectTransform>());bg.GetComponent<Image>().color=new Color(.025f,.12f,.065f);
        var label=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));label.transform.SetParent(go.transform,false);Stretch(label.GetComponent<RectTransform>());label.transform.localPosition=new Vector3(0,0,-1);var t=label.GetComponent<Text>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=38;t.resizeTextForBestFit=true;t.resizeTextMinSize=20;t.resizeTextMaxSize=42;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;
    }
    private static void Stretch(RectTransform t){t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero;}
}
