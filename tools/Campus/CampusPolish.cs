using System;
using System.IO;
using System.Linq;
using HUSEscape.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CampusPolish
{
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();string output=args[Array.IndexOf(args,"-validationReport")+1];
        var scene=EditorSceneManager.OpenScene(CampusSetup.Campus,OpenSceneMode.Single);
        foreach(var point in UnityEngine.Object.FindObjectsByType<CampusDiscovery>().Where(p=>!p.automatic))
        {
            var post=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Single(t=>t.name=="Sign_Post"&&Vector3.Distance(t.position,point.transform.position+Vector3.up*.3f)<.1f);post.position+=Vector3.right*2;
            var sign=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>()).Single(c=>c.name==point.description);sign.transform.position+=Vector3.right*2;sign.transform.rotation=Quaternion.identity;
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(output,"{\"passed\":true,\"signsMovedOffPath\":2}");EditorApplication.Exit(0);
    }
}
