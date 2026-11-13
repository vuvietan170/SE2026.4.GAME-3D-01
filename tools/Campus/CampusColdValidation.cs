using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad] public static class CampusColdValidation
{
    private static CampusColdProbe probe;
    static CampusColdValidation(){if(SessionState.GetBool("CampusColdProbe",false))EditorApplication.update+=Observe;}
    public static void Run(){var args=Environment.GetCommandLineArgs();EditorSceneManager.OpenScene(CampusSetup.Floor,OpenSceneMode.Single);probe=new GameObject("Temporary_Campus_Cold_Probe").AddComponent<CampusColdProbe>();probe.reportPath=args[Array.IndexOf(args,"-validationReport")+1];probe.checkpointDirectory=args[Array.IndexOf(args,"-checkpointDirectory")+1];SessionState.SetBool("CampusColdProbe",true);EditorApplication.update-=Observe;EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();}
    private static void Observe(){if(!EditorApplication.isPlaying)return;if(probe==null)probe=Resources.FindObjectsOfTypeAll<CampusColdProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());if(probe==null||!probe.Finished)return;SessionState.SetBool("CampusColdProbe",false);EditorApplication.update-=Observe;EditorApplication.Exit(probe.Passed?0:1);}
}
