#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HUSEscape.Player;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
[InitializeOnLoad]
public static class PlayerPauseValidation
{
    static PlayerPauseValidation() { if (SessionState.GetBool("PlayerPauseTests", false)) EditorApplication.update += Observe; }
    public static void Run()
    {
        AuditSerializedScenes();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("PlayerPauseProbe").AddComponent<PlayerPauseProbe>();
        SessionState.SetBool("PlayerPauseTests", true); EditorApplication.update += Observe; EditorApplication.EnterPlaymode();
    }
    static void AuditSerializedScenes()
    {
        var rows=new List<string>();
        var paths=new[]{"Assets/Scenes/SampleScene.unity", "Assets/_Project/Scenes/Floor01_Prototype.unity",
            "Assets/_Project/Scenes/Campus_Prototype.unity", "Assets/_Project/Scenes/TrainingOffice_Prototype.unity"};
        foreach(var path in paths)
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerController>(true)).ToArray();
            if(components.Length!=1 || !(components[0] is PlayerController1))throw new Exception(path+": controller migration failed");
            var player=components[0];var data=new SerializedObject(player);
            foreach(var field in new[]{"inputActions","cameraTransform","animator"})
                if(data.FindProperty(field)?.objectReferenceValue==null)throw new Exception(path+": lost "+field);
            var values=new Dictionary<string,float>{{"animationSmoothTime",.1f},{"moveSpeed",4},{"sprintMultiplier",1.6f},
                {"rotationSharpness",12},{"crouchSpeed",2},{"jumpHeight",1.2f},{"gravity",-20},{"groundedStickForce",2}};
            foreach(var pair in values)if(!Mathf.Approximately(data.FindProperty(pair.Key).floatValue,pair.Value))throw new Exception(path+": changed "+pair.Key);
            if(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(player)))!="ca679e99278289644b7f404dea81c8fb")throw new Exception(path+": changed script GUID");
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception(path+": missing script "+t.name);
            rows.Add("PASS "+path+": one PlayerController1; inherited references and eight tuning values preserved; no missing scripts");
        }
        File.WriteAllLines(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"player-serialization-results.txt"),rows);
    }
    static void Observe()
    {
        if (!EditorApplication.isPlaying) return;
        var probe=UnityEngine.Object.FindAnyObjectByType<PlayerPauseProbe>();
        if(probe==null || !probe.Finished)return;
        SessionState.SetBool("PlayerPauseTests",false); EditorApplication.Exit(probe.Passed?0:1);
    }
}
#endif
