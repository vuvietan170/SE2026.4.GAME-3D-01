using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class FloorColdValidation
{
    private static FloorColdProbe probe;
    static FloorColdValidation(){if(SessionState.GetBool("FloorCold",false))EditorApplication.update+=Observe;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();string path=args[Array.IndexOf(args,"-validationReport")+1];
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Floor01_Prototype.unity",OpenSceneMode.Single);
        probe=new GameObject("Temporary_ColdLoad_Probe").AddComponent<FloorColdProbe>();probe.reportPath=path;
        int checkpointIndex=Array.IndexOf(args,"-checkpointDirectory");
        if(checkpointIndex<0)throw new ArgumentException("Cold load requires -checkpointDirectory from a completed route run.");
        probe.saveDirectory=args[checkpointIndex+1];SessionState.SetBool("FloorCold",true);EditorApplication.update-=Observe;EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();
    }
    private static void Observe(){if(!EditorApplication.isPlaying)return;if(probe==null)probe=Resources.FindObjectsOfTypeAll<FloorColdProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());if(probe==null||!probe.Finished)return;SessionState.SetBool("FloorCold",false);EditorApplication.update-=Observe;EditorApplication.Exit(probe.Passed?0:1);}
}
