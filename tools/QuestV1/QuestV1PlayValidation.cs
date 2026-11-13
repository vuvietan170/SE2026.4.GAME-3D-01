using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class QuestV1PlayValidation
{
    private static QuestV1PlayProbe probe;
    static QuestV1PlayValidation(){if(SessionState.GetBool("QuestV1Probe",false)){EditorApplication.update+=Observe;}}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();string output=args[Array.IndexOf(args,"-validationReport")+1];
        try{EditorSceneManager.OpenScene("Assets/_Project/Scenes/Floor01_Prototype.unity",OpenSceneMode.Single);probe=new GameObject("Temporary_Floor01_Probe").AddComponent<QuestV1PlayProbe>();probe.reportPath=output;SessionState.SetBool("QuestV1Probe",true);EditorApplication.update-=Observe;EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();}
        catch(Exception e){File.WriteAllText(output,e.ToString());EditorApplication.Exit(1);}
    }
    private static void Observe(){if(!EditorApplication.isPlaying){return;}if(probe==null){probe=Resources.FindObjectsOfTypeAll<QuestV1PlayProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());}if(probe==null||!probe.Finished){return;}SessionState.SetBool("QuestV1Probe",false);EditorApplication.update-=Observe;EditorApplication.Exit(probe.Passed?0:1);}
}
