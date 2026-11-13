using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad] public static class CampusPlayValidation
{
    private static CampusPlayProbe probe;
    static CampusPlayValidation(){if(SessionState.GetBool("CampusProbe",false))EditorApplication.update+=Observe;}
    public static void Run(){var args=Environment.GetCommandLineArgs();string output=args[Array.IndexOf(args,"-validationReport")+1];EditorSceneManager.OpenScene(CampusSetup.Floor,OpenSceneMode.Single);probe=new GameObject("Temporary_Campus_Probe").AddComponent<CampusPlayProbe>();probe.reportPath=output;SessionState.SetBool("CampusProbe",true);EditorApplication.update-=Observe;EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();}
    private static void Observe(){if(!EditorApplication.isPlaying)return;if(probe==null)probe=Resources.FindObjectsOfTypeAll<CampusPlayProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());if(probe==null||!probe.Finished)return;SessionState.SetBool("CampusProbe",false);EditorApplication.update-=Observe;EditorApplication.Exit(probe.Passed?0:1);}
}
