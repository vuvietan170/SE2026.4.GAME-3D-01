using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class CameraCollisionValidation
{
    static CameraCollisionProbe probe;
    static CameraCollisionValidation(){if(SessionState.GetBool("CameraCollisionProbe",false))EditorApplication.update+=Observe;}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();string output=args[Array.IndexOf(args,"-validationReport")+1];
        try{EditorSceneManager.OpenScene("Assets/_Project/Scenes/Floor01_Prototype.unity",OpenSceneMode.Single);probe=new GameObject("Temporary_Camera_Collision_Probe").AddComponent<CameraCollisionProbe>();probe.reportPath=output;SessionState.SetBool("CameraCollisionProbe",true);EditorApplication.update-=Observe;EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();}
        catch(Exception e){File.WriteAllText(output,e.ToString());EditorApplication.Exit(1);}
    }
    static void Observe(){if(!EditorApplication.isPlaying)return;if(probe==null)probe=Resources.FindObjectsOfTypeAll<CameraCollisionProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());if(probe==null||!probe.Finished)return;SessionState.SetBool("CameraCollisionProbe",false);EditorApplication.update-=Observe;EditorApplication.Exit(probe.Passed?0:1);}
}
