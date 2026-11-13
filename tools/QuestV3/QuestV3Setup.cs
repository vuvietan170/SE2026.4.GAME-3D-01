using System;
using System.IO;
using System.Linq;
using HUSEscape.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-time LAB authoring in a verification copy, preserving all prior quest definitions.</summary>
public static class QuestV3Setup
{
    public static void AdjustNewDevices()
    {
        var args = Environment.GetCommandLineArgs(); string report = args[Array.IndexOf(args, "-validationReport") + 1];
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Floor01_Prototype.unity", OpenSceneMode.Single);
            foreach (string letter in new[] { "B", "C" })
            {
                if (Mathf.Abs(GameObject.Find("LAB_Device_" + letter).transform.position.x - 21.6f) > .01f)
                    throw new InvalidOperationException("Only the initial prototype positions can be adjusted once.");
                foreach (string suffix in new[] { "", "_Indicator", "_Label" })
                {
                    var go = GameObject.Find("LAB_Device_" + letter + suffix);
                    if (go == null) throw new InvalidOperationException("Missing new device part.");
                    go.transform.position += Vector3.right * .6f;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText(report, "{\"passed\":true,\"newDevicesMovedEast\":0.6}"); EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText(report, e.ToString()); EditorApplication.Exit(1); }
    }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); string report = args[Array.IndexOf(args, "-validationReport") + 1];
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Floor01_Prototype.unity", OpenSceneMode.Single);
            if (GameObject.Find("Floor01_QuestV3_LAB") != null) throw new InvalidOperationException("LAB content already exists; refusing duplicate authoring.");
            var manager = UnityEngine.Object.FindObjectsByType<QuestManager>().Single();
            var data = new SerializedObject(manager); var definitions = data.FindProperty("definitions");
            if (definitions.arraySize != 12 || definitions.GetArrayElementAtIndex(11).FindPropertyRelative("completionEvent").stringValue != "room:LAB")
                throw new InvalidOperationException("Expected the accepted twelve-objective V2 scene.");
            string[] ids = { "lab-inspect", "lab-power", "lab-connect", "lab-data", "floor01-complete" };
            string[] titles = { "Kiểm tra máy tính trung tâm trong LAB (E).", "Tìm cầu dao LAB và khôi phục nguồn (E).", "Xem hướng dẫn trên máy tính rồi kết nối các thiết bị A → B → C (E).", "Quay lại máy tính trung tâm để truy cập dữ liệu (E).", "Đọc hướng dẫn thoát LAB và xác nhận hoàn thành tầng 1." };
            string[] events = { "lab:inspect", "lab:power", "lab:connected", "lab:data", "lab:complete" };
            definitions.arraySize = 17;
            for (int i = 0; i < 5; i++)
            {
                var entry = definitions.GetArrayElementAtIndex(12 + i); entry.FindPropertyRelative("id").stringValue = ids[i];
                entry.FindPropertyRelative("title").stringValue = titles[i]; entry.FindPropertyRelative("completionEvent").stringValue = events[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var root = new GameObject("Floor01_QuestV3_LAB").transform;
            var system = root.gameObject.AddComponent<LabRestoreSystem>(); Set(system, "quests", manager);
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_Metal.mat");
            var trim = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_TrimIvory.mat");
            var terminal = GameObject.Find("Teacher_Terminal");
            if (terminal == null || terminal.GetComponent<LabInteractable>() != null) throw new InvalidOperationException("Expected one original teacher terminal without LAB interaction.");
            var computer = terminal.AddComponent<LabInteractable>(); Configure(computer, system, LabInteractionKind.Computer, 1.9f);
            var screen = Box("LAB_Central_StatusScreen", root, new Vector3(19, 1.18f, 47.48f), new Vector3(.77f, .45f, .015f), metal, false).GetComponent<Renderer>();
            Label("LAB_Computer_Label", root, new Vector3(19, 1.95f, 47.4f), Quaternion.identity, "MAY TINH TRUNG TAM - E", 2.6f, .24f);
            var breaker = Box("LAB_Breaker", root, new Vector3(7.3f, 1.4f, 48), new Vector3(.24f, 1.1f, .8f), metal, true);
            Configure(breaker.AddComponent<LabInteractable>(), system, LabInteractionKind.Breaker, 1.6f);
            var breakerIndicator = Box("LAB_Breaker_Indicator", root, new Vector3(7.44f, 1.65f, 48), new Vector3(.035f, .25f, .4f), trim, false).GetComponent<Renderer>();
            Label("LAB_Breaker_Label", root, new Vector3(7.46f, 2.1f, 48), Quaternion.Euler(0, -90, 0), "CAU DAO - E", 1.6f, .25f);
            var positions = new[] { new Vector3(9.4f, .75f, 58.6f), new Vector3(22.2f, .75f, 53), new Vector3(22.2f, 1, 58.6f) };
            var indicators = new Renderer[3]; string[] names = { "NGUON DU PHONG", "BO DIEU KHIEN", "MAY CHU DU LIEU" };
            for (int i = 0; i < 3; i++)
            {
                string letter = ((char)('A' + i)).ToString();
                var device = Box("LAB_Device_" + letter, root, positions[i], i == 2 ? new Vector3(.9f, 1.8f, .8f) : new Vector3(.7f, 1.4f, .7f), metal, true);
                Configure(device.AddComponent<LabInteractable>(), system, (LabInteractionKind)((int)LabInteractionKind.DeviceA + i), 1.6f);
                var offset = i == 0 ? new Vector3(0, .25f, -.37f) : new Vector3(-.47f, .25f, 0);
                indicators[i] = Box("LAB_Device_" + letter + "_Indicator", root, positions[i] + offset, i == 0 ? new Vector3(.4f, .18f, .03f) : new Vector3(.03f, .18f, .4f), trim, false).GetComponent<Renderer>();
                Label("LAB_Device_" + letter + "_Label", root, positions[i] + new Vector3(0, i == 2 ? 1.3f : 1.05f, 0), i == 0 ? Quaternion.identity : Quaternion.Euler(0, 90, 0), letter + " - " + names[i] + " (E)", 2.9f, .28f);
            }
            Set(system, "screen", screen); Set(system, "breakerIndicator", breakerIndicator);
            var refs = new SerializedObject(system); var list = refs.FindProperty("deviceIndicators"); list.arraySize = 3;
            for (int i = 0; i < 3; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = indicators[i]; refs.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            File.WriteAllText(report, "{\"passed\":true,\"objectives\":17,\"physicalTargets\":5,\"solution\":\"A-B-C\",\"originalComputerReused\":true}"); EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText(report, e.ToString()); EditorApplication.Exit(1); }
    }
    private static void Configure(LabInteractable target, LabRestoreSystem system, LabInteractionKind kind, float distance)
    { var data = new SerializedObject(target); data.FindProperty("system").objectReferenceValue = system; data.FindProperty("kind").enumValueIndex = (int)kind; data.FindProperty("interactionDistance").floatValue = distance; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static GameObject Box(string name, Transform parent, Vector3 p, Vector3 size, Material material, bool collider)
    { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, true); go.transform.position = p; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go; }
    private static void Label(string name, Transform parent, Vector3 p, Quaternion rotation, string value, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(p, rotation); go.transform.localScale = Vector3.one * .01f;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; go.GetComponent<RectTransform>().sizeDelta = new Vector2(width * 100, height * 100);
        var child = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); child.transform.SetParent(go.transform, false);
        var rect = child.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = child.GetComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 20; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
    }
}
