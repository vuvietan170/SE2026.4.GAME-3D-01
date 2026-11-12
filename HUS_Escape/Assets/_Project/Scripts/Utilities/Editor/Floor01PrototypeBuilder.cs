using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HUSEscape.Editor
{
    /// <summary>Authors a persistent prototype scene using existing gameplay and prefab assets.</summary>
    public static class Floor01PrototypeBuilder
    {
        public const string Output = "Assets/_Project/Scenes/Floor01_Prototype.unity";
        private const string Sample = "Assets/Scenes/SampleScene.unity";
        private const float Height = 5.4f;
        private static Material floor, wall, ceiling, door, metal, key, furniture;

        [MenuItem("HUS Escape/Build Floor 01 Prototype (new scene only)")]
        public static void Build()
        {
            if (File.Exists(Output)) { throw new InvalidOperationException("Floor01_Prototype already exists. Open it to edit; this builder never overwrites it."); }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            BuildScene();
        }

        /// <summary>Batch entry point; uses the same authoring path without touching build settings.</summary>
        public static void BuildForValidation()
        {
            var args = Environment.GetCommandLineArgs();
            string report = args[Array.IndexOf(args, "-validationReport") + 1];
            try
            {
                if (File.Exists(Output)) { throw new InvalidOperationException("Refusing to overwrite an existing floor scene."); }
                BuildScene();
                File.WriteAllText(report, "{\"passed\":true,\"scene\":\"" + Output + "\",\"unity\":\"" + Application.unityVersion + "\"}");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText(report, JsonUtility.ToJson(new Failure { error = e.ToString() }, true)); EditorApplication.Exit(1); }
        }

        [Serializable] private sealed class Failure { public string error; }

        private static void BuildScene()
        {
            Scene source = EditorSceneManager.OpenScene(Sample, OpenSceneMode.Single);
            Scene scene = default;
            try
            {
                GameObject sourcePlayer = source.GetRootGameObjects().Single(go => go.GetComponent<CharacterController>() != null);
                GameObject sourceCamera = source.GetRootGameObjects().Single(go => go.GetComponent<Camera>() != null);
                GameObject doorAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interactables/Door_Locked.prefab");
                GameObject keyAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/Key_Pickup.prefab");
                if (doorAsset == null || keyAsset == null) { throw new InvalidOperationException("Existing door/key prefabs are required."); }
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);

                floor = MaterialAsset("Floor", new Color(.29f, .34f, .4f));
                wall = MaterialAsset("Wall", new Color(.79f, .79f, .7f));
                ceiling = MaterialAsset("Ceiling", new Color(.86f, .87f, .88f));
                door = MaterialAsset("Door", new Color(.16f, .38f, .54f));
                metal = MaterialAsset("Metal", new Color(.11f, .14f, .18f));
                key = MaterialAsset("Key", new Color(.95f, .67f, .12f));
                furniture = MaterialAsset("Furniture", new Color(.48f, .33f, .21f));

                Transform architecture = Group("Floor01_Architecture");
                Transform hall = Group("Main_Corridor_14m_Wide", architecture);
                Box("Corridor_Floor", hall, new Vector3(0, -.1f, 31), new Vector3(14, .2f, 62), floor);
                Box("Corridor_Ceiling", hall, new Vector3(0, Height + .1f, 31), new Vector3(14, .2f, 62), ceiling);
                Box("Corridor_West_Wall", hall, new Vector3(-7.1f, Height / 2, 31), new Vector3(.2f, Height, 62.4f), wall);
                Box("Corridor_Start_Wall", hall, new Vector3(0, Height / 2, -.1f), new Vector3(14.4f, Height, .2f), wall);
                Box("Corridor_End_Wall", hall, new Vector3(0, Height / 2, 62.1f), new Vector3(14.4f, Height, .2f), wall);
                foreach (var interval in new[] {new Vector2(0,10), new Vector2(24,28), new Vector2(42,46), new Vector2(60,62)})
                { FrontWall(hall, "Corridor_East_Solid", interval.x, interval.y, 0, Height); }

                Transform interactables = Group("Floor01_Doors_And_Keys");
                Room("A101", 10, 17, architecture, interactables, doorAsset);
                Room("A102", 28, 35, architecture, interactables, doorAsset);
                Room("LAB", 46, 53, architecture, interactables, doorAsset);
                PlaceKey("Key_A101", new Vector3(0, .79f, 11.5f), interactables, keyAsset);
                PlaceKey("Key_A102", new Vector3(12, .79f, 21), interactables, keyAsset);
                PlaceKey("Key_Lab", new Vector3(12, .79f, 39), interactables, keyAsset);
                Desk("A101_Key_Desk", architecture, new Vector3(12, .36f, 21), new Vector3(1.2f, .72f, .8f));
                Desk("A102_Key_Desk", architecture, new Vector3(12, .36f, 39), new Vector3(1.2f, .72f, .8f));
                Desk("Corridor_Key_Stand", architecture, new Vector3(0, .36f, 11.5f), new Vector3(1.2f, .72f, .8f));

                GameObject player = UnityEngine.Object.Instantiate(sourcePlayer);
                player.name = "Player";
                SceneManager.MoveGameObjectToScene(player, scene);
                player.transform.SetPositionAndRotation(new Vector3(0, 1.08f, 8), Quaternion.identity);
                GameObject camera = UnityEngine.Object.Instantiate(sourceCamera);
                camera.name = "Main Camera";
                SceneManager.MoveGameObjectToScene(camera, scene);
                camera.transform.SetPositionAndRotation(new Vector3(0, 4.43f, 2.08f), Quaternion.Euler(20, 0, 0));
                Component movement = player.GetComponents<MonoBehaviour>().Single(c => c != null && c.GetType().Name == "PlayerController1");
                SetReference(movement, "cameraTransform", camera.transform);
                Component follow = camera.GetComponents<MonoBehaviour>().Single(c => c != null && c.GetType().Name == "ThirdPersonCameraFollow");
                SetReference(follow, "target", player.transform);
                AppendInventoryLabel(player, "Key_A102", "Chia khoa A102");
                AppendInventoryLabel(player, "Key_Lab", "Chia khoa LAB");

                Transform markers = Group("Floor01_Signs_And_Spawn");
                var spawn = new GameObject("Player_Start");
                spawn.transform.SetParent(markers, false); spawn.transform.position = player.transform.position;
                Sign("Instructions", markers, new Vector3(-3.5f, 2.3f, 10), Quaternion.identity,
                    "TANG 1 - A101 > A102 > LAB\nWASD: di chuyen | E: cua | F: nhat | Tab: tui do", 4.2f, 1);
                Sign("Finish", markers, new Vector3(22.8f, 2.4f, 53), Quaternion.Euler(0, 90, 0), "LAB\nHOAN THANH LO TRINH", 3.2f, .85f);
                Lighting();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, Output)) { throw new IOException("Could not save prototype scene."); }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                // Source is read-only: never save or replace SampleScene.
                if (source.IsValid() && source.isLoaded) { EditorSceneManager.CloseScene(source, true); }
                if (scene.IsValid() && scene.isLoaded) { SceneManager.SetActiveScene(scene); }
            }
        }

        private static void Room(string name, float startZ, float doorZ, Transform parent, Transform interactables, GameObject asset)
        {
            Transform room = Group("Room_" + name, parent);
            Box(name + "_Floor", room, new Vector3(15, -.1f, startZ + 7), new Vector3(16, .2f, 14), floor);
            Box(name + "_Ceiling", room, new Vector3(15, Height + .1f, startZ + 7), new Vector3(16, .2f, 14), ceiling);
            Box(name + "_East_Wall", room, new Vector3(23.1f, Height / 2, startZ + 7), new Vector3(.2f, Height, 14.4f), wall);
            foreach (float z in new[] { startZ - .1f, startZ + 14.1f }) { Box(name + "_Side_Wall", room, new Vector3(15, Height / 2, z), new Vector3(16.4f, Height, .2f), wall); }
            FrontWall(room, name + "_Front_Left", startZ, doorZ - 1.1f, 0, Height);
            FrontWall(room, name + "_Front_Right", doorZ + 1.1f, startZ + 14, 0, Height);
            FrontWall(room, name + "_Lintel", doorZ - 1.1f, doorZ + 1.1f, 3, Height - 3);
            foreach (float z in new[] {doorZ-1.06f,doorZ+1.06f}) { Box(name+"_Frame_Jamb",room,new Vector3(7,1.5f,z),new Vector3(.28f,3,.12f),metal); }
            Box(name+"_Frame_Head",room,new Vector3(7,2.95f,doorZ),new Vector3(.28f,.12f,2.24f),metal);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,interactables);
            instance.name="Door_"+name;
            instance.transform.SetPositionAndRotation(new Vector3(7,1.4f,doorZ-.9f),Quaternion.Euler(0,-90,0));
            Transform leaf=instance.transform.GetChild(0);
            leaf.localPosition=new Vector3(.9f,0,0); leaf.localScale=new Vector3(1.8f,2.8f,.12f);
            leaf.GetComponent<MeshRenderer>().sharedMaterial=door;
            var component=instance.GetComponent<DoorInteraction>();
            var serialized=new SerializedObject(component);
            serialized.FindProperty("requiredKeyId").stringValue=name=="LAB"?"Key_Lab":"Key_"+name;
            serialized.FindProperty("consumeKey").boolValue=false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(leaf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(leaf.GetComponent<MeshRenderer>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            foreach(float face in new[]{-.1f,.1f})
            {
                Box(name+"_Handle_Plate",instance.transform,new Vector3(1.57f,-.25f,face),new Vector3(.12f,.24f,.025f),key,true,true);
                Box(name+"_Handle",instance.transform,new Vector3(1.5f,-.25f,face*1.5f),new Vector3(.24f,.045f,.07f),key,true,true);
            }
            Sign(name+"_Name",room,new Vector3(6.82f,3.55f,doorZ),Quaternion.Euler(0,90,0),name=="LAB"?"PHONG MAY TINH":name,3,.6f);
            for(int row=0;row<2;row++) for(int column=0;column<2;column++)
            {
                float x=15+column*4, z=startZ+3+row*8;
                Desk(name+"_Desk",room,new Vector3(x,.4f,z),new Vector3(2.1f,.8f,1));
                if(name=="LAB")
                {
                    Box("Computer_Base",room,new Vector3(x,.87f,z),new Vector3(.5f,.08f,.35f),metal);
                    Box("Computer_Monitor",room,new Vector3(x,1.15f,z+.15f),new Vector3(.9f,.5f,.1f),door);
                }
            }
        }

        private static void PlaceKey(string id,Vector3 position,Transform parent,GameObject asset)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
            instance.name=id; instance.transform.position=position; instance.transform.rotation=Quaternion.identity; instance.transform.localScale=new Vector3(.3f,.12f,.35f);
            instance.GetComponent<MeshRenderer>().sharedMaterial=key;
            var component=instance.GetComponent<PickupItem>(); var serialized=new SerializedObject(component);
            serialized.FindProperty("itemId").stringValue=id;serialized.FindProperty("itemName").stringValue=id;
            serialized.FindProperty("interactionDistance").floatValue=1.6f;serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<MeshRenderer>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            Sign(id+"_Label",parent,position+new Vector3(0,.65f,-.15f),Quaternion.identity,id+" (F)",1.5f,.3f);
        }

        private static void Desk(string name,Transform parent,Vector3 center,Vector3 size) => Box(name,parent,center,size,furniture);
        private static void FrontWall(Transform parent,string name,float from,float to,float bottom,float height)
            => Box(name,parent,new Vector3(7,bottom+height/2,(from+to)/2),new Vector3(.2f,height,to-from),wall);
        private static Transform Group(string name,Transform parent=null){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        private static GameObject Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool local=false,bool decorative=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            if(local){go.transform.localPosition=position;}else{go.transform.position=position;}
            go.transform.localScale=scale;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            if(decorative){UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}
            return go;
        }
        private static Material MaterialAsset(string suffix,Color color)
        {
            string path="Assets/_Project/Materials/Floor01_"+suffix+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null){return existing;}
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null){throw new InvalidOperationException("Existing URP/Lit shader required.");}
            var material=new Material(shader){name="Floor01_"+suffix};material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.15f);
            AssetDatabase.CreateAsset(material,path);return material;
        }
        private static void Sign(string name,Transform parent,Vector3 position,Quaternion rotation,string text,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas));go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=Vector3.one*.01f;
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(width*100,height*100);
            var background=new GameObject("Backing",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));background.transform.SetParent(go.transform,false);
            var br=background.GetComponent<RectTransform>();br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.offsetMin=br.offsetMax=Vector2.zero;background.GetComponent<Image>().color=new Color(.06f,.1f,.15f);
            var label=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));label.transform.SetParent(go.transform,false);
            var tr=label.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(5,3);tr.offsetMax=new Vector2(-5,-3);tr.localPosition=new Vector3(tr.localPosition.x,tr.localPosition.y,-1);
            var t=label.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.fontSize=30;t.resizeTextForBestFit=true;t.resizeTextMinSize=14;t.resizeTextMaxSize=40;t.raycastTarget=false;
        }
        private static void SetReference(Component component,string field,UnityEngine.Object value){var s=new SerializedObject(component);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        private static void AppendInventoryLabel(GameObject player,string id,string name)
        {
            var component=player.GetComponents<MonoBehaviour>().Single(c=>c!=null&&c.GetType().Name=="InventoryUI");var s=new SerializedObject(component);var list=s.FindProperty("itemVisuals");
            if(Enumerable.Range(0,list.arraySize).Any(i=>list.GetArrayElementAtIndex(i).FindPropertyRelative("itemId").stringValue==id)){return;}
            int index=list.arraySize;list.InsertArrayElementAtIndex(index);var item=list.GetArrayElementAtIndex(index);item.FindPropertyRelative("itemId").stringValue=id;item.FindPropertyRelative("displayName").stringValue=name;item.FindPropertyRelative("description").stringValue="Mo cua phong tuong ung.";item.FindPropertyRelative("icon").objectReferenceValue=null;s.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Lighting()
        {
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.73f);RenderSettings.reflectionIntensity=.3f;
            Transform lights=Group("Floor01_Lighting");var sun=new GameObject("Soft_Directional",typeof(Light));sun.transform.SetParent(lights,false);sun.transform.rotation=Quaternion.Euler(55,-30,0);var directional=sun.GetComponent<Light>();directional.type=LightType.Directional;directional.intensity=1;directional.shadows=LightShadows.None;
            foreach(float z in new[]{8f,20f,32f,44f,56f}){Point(lights,new Vector3(0,4.7f,z));}
            foreach(float z in new[]{17f,35f,53f}){Point(lights,new Vector3(12,4.7f,z));Point(lights,new Vector3(20,4.7f,z));}
        }
        private static void Point(Transform parent,Vector3 position){var go=new GameObject("Ceiling_Light",typeof(Light));go.transform.SetParent(parent,false);go.transform.position=position;var light=go.GetComponent<Light>();light.type=LightType.Point;light.intensity=4;light.range=14;light.shadows=LightShadows.None;}
    }
}
