#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RepositoryValidation
{
    [Serializable] private sealed class AuditReport { public bool passed; public int assets, scenes, prefabs, resolvedGuidReferences; public string[] errors; }
    [Serializable] private sealed class BuildReportData { public bool passed; public string target, result, player; public int errors, warnings; public long bytes; }
    private static string Output => Directory.GetParent(Application.dataPath).FullName;
    private static string[] Scenes => EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
    public static void Audit()
    {
        var report=new AuditReport();var errors=new List<string>();var references=new HashSet<string>();
        var scenePaths=Scenes;
        if(scenePaths.Length!=6 || scenePaths[0]!="Assets/_Project/Scenes/Bootstrap/Bootstrap.unity")errors.Add("Expected six build scenes starting with Bootstrap");
        foreach(var path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(p)))
        {
            report.assets++;
            if(new[]{".unity",".prefab",".asset",".mat",".controller",".meta"}.Contains(Path.GetExtension(path)))
                foreach(Match m in Regex.Matches(File.ReadAllText(path),@"guid: ([0-9a-f]{32})"))references.Add(m.Groups[1].Value);
        }
        foreach(var guid in references)
        {
            if(guid.StartsWith("0000000000000000",StringComparison.Ordinal))continue;
            if(string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))errors.Add("Unresolved GUID "+guid);
            else report.resolvedGuidReferences++;
        }
        foreach(var path in scenePaths)
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);report.scenes++;
            foreach(var root in scene.GetRootGameObjects())CheckHierarchy(root,path,errors);
        }
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
            try{CheckHierarchy(root,path,errors);report.prefabs++;}finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        report.errors=errors.ToArray();report.passed=errors.Count==0;
        File.WriteAllText(Path.Combine(Output,"native-audit.json"),JsonUtility.ToJson(report,true));
        if(!report.passed)throw new Exception(string.Join("\n",errors));
    }
    private static void CheckHierarchy(GameObject root,string path,List<string> errors)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)errors.Add(path+": missing script on "+t.name);
            foreach(var component in t.GetComponents<MonoBehaviour>())
            {
                if(component==null)continue;
                var data=new SerializedObject(component);var p=data.GetIterator();
                while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue==null && p.objectReferenceEntityIdValue!=EntityId.None)
                    errors.Add(path+": broken reference "+t.name+"/"+p.propertyPath);
            }
        }
    }
    public static void BuildSmoke()
    {
        var target=EditorUserBuildSettings.activeBuildTarget;
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target))throw new Exception("Standalone build module missing: "+target);
        string extension=target==BuildTarget.StandaloneWindows64?".exe":target==BuildTarget.StandaloneLinux64?".x86_64":null;
        if(extension==null)throw new Exception("Validation runner currently supports standalone Windows64/Linux64 only");
        var directory=Path.Combine(Output,"BuildSmoke");Directory.CreateDirectory(directory);
        var player=Path.Combine(directory,"HUSEscape"+extension);
        var built=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=Scenes,locationPathName=player,target=target,options=BuildOptions.Development});
        var result=new BuildReportData{passed=built.summary.result==BuildResult.Succeeded,target=target.ToString(),result=built.summary.result.ToString(),
            player=player,errors=built.summary.totalErrors,warnings=built.summary.totalWarnings,bytes=(long)built.summary.totalSize};
        File.WriteAllText(Path.Combine(Output,"build-result.json"),JsonUtility.ToJson(result,true));
        if(!result.passed||!File.Exists(player))throw new Exception("Build failed: "+result.result+"; errors="+result.errors);
    }
}
#endif
