using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad] public static class ElevatorLobbyValidation
{
    private static ElevatorLobbyProbe probe;
    static ElevatorLobbyValidation(){if(SessionState.GetBool("ElevatorLobbyProbe",false))EditorApplication.update+=Observe;}
    public static void Run(){var args=Environment.GetCommandLineArgs();string output=args[Array.IndexOf(args,"-validationReport")+1];EditorSceneManager.OpenScene(CampusSetup.Floor,OpenSceneMode.Single);probe=new GameObject("Temporary_Elevator_Probe").AddComponent<ElevatorLobbyProbe>();probe.reportPath=output;SessionState.SetBool("ElevatorLobbyProbe",true);EditorApplication.update-=Observe;EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();}
    private static void Observe(){if(!EditorApplication.isPlaying)return;if(probe==null)probe=Resources.FindObjectsOfTypeAll<ElevatorLobbyProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());if(probe==null||!probe.Finished)return;SessionState.SetBool("ElevatorLobbyProbe",false);EditorApplication.update-=Observe;EditorApplication.Exit(probe.Passed?0:1);}
}
