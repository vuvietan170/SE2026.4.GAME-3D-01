using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HUSEscape.World;
using HUSEscape.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class ElevatorLobbyVisualValidation
{
    private static ExitVestibuleViewProbe probe;
    private static FloorGuiReadback readback;
    private static string output;
    private static double until;
    private static int step;
    private static Keyboard keyboard;
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"-validationReport")+1];
        EditorSceneManager.OpenScene(CampusSetup.Floor,OpenSceneMode.Single);
        var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
        typeof(FloorGuiValidation).GetField("view",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,view);
        probe=new GameObject("Temporary_Elevator_View").AddComponent<ExitVestibuleViewProbe>();probe.checkpointDirectory=args[Array.IndexOf(args,"-checkpointDirectory")+1];EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();
    }
    private static void Observe()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(probe==null)probe=Resources.FindObjectsOfTypeAll<ExitVestibuleViewProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());if(probe==null)return;if(probe.Error!=null)throw new Exception(probe.Error);if(!probe.Ready)return;
            var entry=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().Single();
            if(step==0)
            {
                typeof(FloorGuiValidation).GetMethod("UseResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1920,1080});
                entry.player.enabled=false;entry.player.transform.SetPositionAndRotation(new Vector3(1.8f,1.08f,68.05f),Quaternion.Euler(0,90,0));entry.player.enabled=true;
                typeof(ThirdPersonCameraFollow).GetField("yaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(entry.viewCamera.GetComponent<ThirdPersonCameraFollow>(),90f);
                var settings=UnityEngine.Object.Instantiate(InputSystem.settings);settings.hideFlags=HideFlags.HideAndDontSave;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=settings;
                keyboard=InputSystem.AddDevice<Keyboard>();until=EditorApplication.timeSinceStartup+3;step++;return;
            }
            if(EditorApplication.timeSinceStartup<until)return;
            if(step==1){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));until=EditorApplication.timeSinceStartup+.2;step++;return;}
            if(step==2){InputSystem.QueueStateEvent(keyboard,new KeyboardState());var panel=UnityEngine.Object.FindObjectsByType<ElevatorUnavailable>().Single();if(!panel.CanInteract||panel.Message!="Thang máy chưa khả dụng")throw new Exception("Panel message missing");readback=new GameObject("Temporary_Elevator_Readback").AddComponent<FloorGuiReadback>();readback.Capture(Path.Combine(Path.GetDirectoryName(output),"Elevator_Unavailable_1080p.png"),1920,1080);step++;return;}
            if(!readback.Done)return;if(readback.Error!=null)throw new Exception(readback.Error);
            File.WriteAllText(output,"{\"passed\":true,\"image\":\"Elevator_Unavailable_1080p.png\",\"width\":1920,\"height\":1080,\"nativeMessageVerified\":true}");EditorApplication.update-=Observe;EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(output,e.ToString());EditorApplication.update-=Observe;EditorApplication.Exit(1);}
    }
}
