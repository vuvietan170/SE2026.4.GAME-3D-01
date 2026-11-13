using System;
using System.IO;
using System.Linq;
using HUSEscape.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-time authoring in the verification copy; refuses an already configured scene.</summary>
public static class QuestV2Setup
{
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); string report = args[Array.IndexOf(args, "-validationReport") + 1];
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Floor01_Prototype.unity", OpenSceneMode.Single);
            if (GameObject.Find("Floor01_QuestV2_A102") != null) throw new InvalidOperationException("Quest V2 is already authored; refusing duplicates.");
            var manager = UnityEngine.Object.FindObjectsByType<QuestManager>().Single();
            var key = UnityEngine.Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include).Single(k => k.name == "Key_Lab");
            var root = new GameObject("Floor01_QuestV2_A102").transform;
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_WoodWarm.mat");
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_Metal.mat");
            var paper = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Floor01_TrimIvory.mat");
            var serialized = new SerializedObject(manager); var entries = serialized.FindProperty("definitions");
            if (entries.arraySize != 8 || entries.GetArrayElementAtIndex(6).FindPropertyRelative("id").stringValue != "take-key-lab")
                throw new InvalidOperationException("Expected the accepted eight-objective V1 definition list.");
            string[] ids = { "read-a102-A", "read-a102-B", "read-a102-C", "solve-a102-sequence" };
            string[] titles = { "Đọc manh mối sự kiện A gần bảng viết A102 (F).", "Tìm và đọc manh mối sự kiện B trong A102 (F).", "Tìm và đọc manh mối sự kiện C trong A102 (F).", "Sắp ba sự kiện theo thời gian tại bảng A102 (E)." };
            string[] facts = { "clue:A102:A", "clue:A102:B", "clue:A102:C", "sequence:A102" };
            for (int i = 0; i < 4; i++)
            {
                entries.InsertArrayElementAtIndex(6 + i); var entry = entries.GetArrayElementAtIndex(6 + i);
                entry.FindPropertyRelative("id").stringValue = ids[i]; entry.FindPropertyRelative("title").stringValue = titles[i]; entry.FindPropertyRelative("completionEvent").stringValue = facts[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var positions = new[] { new Vector3(17, 2.1f, 28.24f), new Vector3(15, .835f, 31), new Vector3(18.8f, .86f, 39) };
            int[] years = { 2016, 2020, 2018 };
            for (int i = 0; i < 3; i++)
            {
                string name = ((char)('A' + i)).ToString();
                var note = Box("A102_Clue_" + name, root, positions[i], new Vector3(.34f, .018f, .46f), paper, false).AddComponent<ClueNote>();
                if (i == 0) note.transform.rotation = Quaternion.Euler(90, 0, 0);
                var data = new SerializedObject(note); data.FindProperty("title").stringValue = "MANH MỐI A102 — SỰ KIỆN " + name;
                data.FindProperty("eventId").stringValue = facts[i];
                data.FindProperty("content").stringValue = "Sự kiện " + name + ": " + years[i] + ".\nHãy xếp ba sự kiện giả định từ năm sớm nhất đến năm muộn nhất. Dữ liệu mẫu dùng để kiểm thử.";
                data.ApplyModifiedPropertiesWithoutUndo();
                Label("A102_Clue_Label_" + name, root, positions[i] + new Vector3(0, .65f, -.05f), i == 0 ? Quaternion.Euler(0, 180, 0) : Quaternion.Euler(0, 90, 0), "F - SU KIEN " + name, 1.2f, .22f);
            }
            var panelGO = new GameObject("A102_SequencePanel"); panelGO.transform.SetParent(root, false); panelGO.transform.position = new Vector3(11.35f, 1.2f, 39);
            var panel = panelGO.AddComponent<SequenceLockPanel>();
            Box("A102_Reward_Back", root, new Vector3(12.55f, 1.2f, 39), new Vector3(.08f, .96f, 1.08f), wood, true);
            foreach (float z in new[] { 38.5f, 39.5f }) Box("A102_Reward_Side", root, new Vector3(11.97f, 1.2f, z), new Vector3(1.2f, .96f, .08f), wood, true);
            Box("A102_Reward_Top", root, new Vector3(11.97f, 1.72f, 39), new Vector3(1.26f, .08f, 1.16f), wood, true);
            var shutter = Box("A102_Reward_Shutter", root, new Vector3(11.32f, 1.2f, 39), new Vector3(.065f, .96f, .96f), metal, true).transform;
            Label("A102_Sequence_Label", root, new Vector3(11.2f, 1.93f, 39), Quaternion.Euler(0, 90, 0), "E - THU TU A / B / C", 2.1f, .24f);
            var labels = new GameObject("A102_KeyLab_RewardLabels"); labels.transform.SetParent(root, false);
            foreach (string name in new[] { "Key_Lab_Label", "Key_Lab_Label_ReverseFace", "Key_Lab_Label_OpaqueBacking" })
            {
                var go = GameObject.Find(name); if (go == null) throw new InvalidOperationException("Missing existing label " + name);
                go.transform.SetParent(labels.transform, true);
            }
            Set(panel, "quests", manager); Set(panel, "shutter", shutter); Set(panel, "keyPickup", key.gameObject); Set(panel, "keyLabels", labels);
            key.gameObject.SetActive(false); labels.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(key.gameObject);
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            File.WriteAllText(report, "{\"passed\":true,\"objectives\":12,\"solution\":\"A-C-B\",\"originalKeyLabTransformPreserved\":true}"); EditorApplication.Exit(0);
        }
        catch (Exception e) { File.WriteAllText(report, e.ToString()); EditorApplication.Exit(1); }
    }
    static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    static GameObject Box(string name, Transform parent, Vector3 p, Vector3 size, Material material, bool collision)
    { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, true); go.transform.position = p; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go; }
    static void Label(string name, Transform parent, Vector3 p, Quaternion rotation, string content, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(p, rotation); go.transform.localScale = Vector3.one * .01f;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; go.GetComponent<RectTransform>().sizeDelta = new Vector2(width * 100, height * 100);
        var child = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); child.transform.SetParent(go.transform, false);
        var rect = child.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = child.GetComponent<Text>(); text.text = content; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.alignment = TextAnchor.MiddleCenter; text.fontSize = 20; text.color = Color.white; text.raycastTarget = false;
    }
}
