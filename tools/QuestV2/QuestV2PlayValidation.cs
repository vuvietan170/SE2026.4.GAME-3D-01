using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class QuestV2PlayValidation
{
    private static QuestV2PlayProbe probe;
    static QuestV2PlayValidation(){if(SessionState.GetBool("QuestV2Probe",false)){EditorApplication.update+=Observe;}}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();string output=args[Array.IndexOf(args,"-validationReport")+1];
        try{EditorSceneManager.OpenScene("Assets/_Project/Scenes/Floor01_Prototype.unity",OpenSceneMode.Single);probe=new GameObject("Temporary_Floor01_Probe").AddComponent<QuestV2PlayProbe>();probe.reportPath=output;SessionState.SetBool("QuestV2Probe",true);EditorApplication.update-=Observe;EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();}
        catch(Exception e){File.WriteAllText(output,e.ToString());EditorApplication.Exit(1);}
    }
    private static void Observe(){if(!EditorApplication.isPlaying){return;}if(probe==null){probe=Resources.FindObjectsOfTypeAll<QuestV2PlayProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());}if(probe==null||!probe.Finished){return;}SessionState.SetBool("QuestV2Probe",false);EditorApplication.update-=Observe;EditorApplication.Exit(probe.Passed?0:1);}
}
