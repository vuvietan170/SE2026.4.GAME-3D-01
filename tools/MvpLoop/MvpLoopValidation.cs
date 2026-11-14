#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HUSEscape.World;
using HUSEscape.Quests;
[InitializeOnLoad]
public static class MvpLoopValidation
{
    static MvpLoopValidation(){if(SessionState.GetBool("MvpLoopTests",false))EditorApplication.update+=Observe;}
    public static void Run()
    {
        AuditScenes();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("MvpLoopProbe").AddComponent<MvpLoopProbe>();
        SessionState.SetBool("MvpLoopTests",true);EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();
    }
    static void AuditScenes()
    {
        var rows=new List<string>();
        foreach(var path in EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path))
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)!=0)throw new Exception(path+": missing script");
            if(path.EndsWith("Campus_Prototype.unity"))
            {
                var gate=UnityEngine.Object.FindObjectsByType<MainGateExit>().Single();
                if(gate.entry==null||!gate.entry.isCampus||gate.entry.guards.Length!=2)throw new Exception("Gate composition invalid");
                if(gate.GetComponentsInChildren<TextMesh>().Length!=2||gate.GetComponentsInChildren<TextMesh>().Any(t=>t.font==null||t.GetComponent<Renderer>().sharedMaterial==null))throw new Exception("Gate signs missing font/material");
                if(gate.GetComponentsInChildren<BoxCollider>().Length<10)throw new Exception("Gate primitive geometry missing");
            }
            if(path.EndsWith("TrainingOffice_Prototype.unity"))
            {
                var point=UnityEngine.Object.FindObjectsByType<OfficeDiscovery>().Single(d=>d.fact=="office:computer");
                if(point.content.Contains("chưa")||!point.content.Contains("Cổng chính"))throw new Exception("Office computer route stale");
            }
            rows.Add("PASS "+path+": imported references and scripts valid");
        }
        File.WriteAllLines(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"mvp-scene-audit.txt"),rows);
    }
    static void Observe()
    {
        if(!EditorApplication.isPlaying)return;
        var probe=UnityEngine.Object.FindAnyObjectByType<MvpLoopProbe>();if(probe==null||!probe.Finished)return;
        SessionState.SetBool("MvpLoopTests",false);EditorApplication.Exit(probe.Passed?0:1);
    }
}
#endif
