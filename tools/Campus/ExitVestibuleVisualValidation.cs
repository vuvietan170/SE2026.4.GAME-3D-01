using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HUSEscape.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[Serializable] public sealed class ExitVisualReport {public bool passed;public string unity,error;public int images;public bool finalDoorPrompt,campusActivated;}
public static class ExitVestibuleVisualValidation
{
    private static ExitVestibuleViewProbe probe;private static EditorWindow view;private static FloorGuiReadback readback;
    private static string output;private static int index;private static bool prepared,requestedCampus;private static double waitUntil;private static ExitVisualReport report;
    private static readonly string[] names={"Hall_Guide","Vestibule_Entrance","T3_ExitPrompt_720p","T3_ExitPrompt_1080p","Campus_Arrival"};
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"-validationReport")+1];report=new ExitVisualReport{unity=Application.unityVersion};
        EditorSceneManager.OpenScene(CampusSetup.Floor,OpenSceneMode.Single);view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
        typeof(FloorGuiValidation).GetField("view",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,view);
        probe=new GameObject("Temporary_Exit_View").AddComponent<ExitVestibuleViewProbe>();probe.checkpointDirectory=args[Array.IndexOf(args,"-checkpointDirectory")+1];EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();
    }
    private static void Observe()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(probe==null)probe=Resources.FindObjectsOfTypeAll<ExitVestibuleViewProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());if(probe==null)return;if(probe.Error!=null)throw new Exception(probe.Error);if(!probe.Ready)return;
            var entry=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().SingleOrDefault();if(entry==null)return;
            if(index==5){report.images=5;report.passed=true;File.WriteAllText(output,JsonUtility.ToJson(report,true));EditorApplication.update-=Observe;EditorApplication.Exit(0);return;}
            if(index==4&&!requestedCampus){if(!entry.Session.TryLeaveFloor())throw new Exception(entry.Session.LastMessage);requestedCampus=true;return;}
            if(index==4&&(!entry.isCampus||entry.Session.IsLoading))return;
            int width=index==3?1920:1280,height=index==3?1080:720;
            if(!prepared)
            {
                typeof(FloorGuiValidation).GetMethod("UseResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{width,height});
                if(index<4){entry.player.enabled=false;entry.player.transform.SetPositionAndRotation(new Vector3(0,1.08f,index==0?56:index==1?60.5f:70.5f),Quaternion.identity);entry.player.enabled=true;}
                prepared=true;waitUntil=EditorApplication.timeSinceStartup+3;return;
            }
            if(EditorApplication.timeSinceStartup<waitUntil)return;
            if(index==2||index==3){var exit=UnityEngine.Object.FindObjectsByType<BuildingExit>().Single();if(!exit.CanShowPrompt||!exit.PromptText.Contains("CAMPUS"))throw new Exception("Final door prompt not visible");report.finalDoorPrompt=true;}
            if(index==4){if(Vector3.Distance(entry.player.transform.position,new Vector3(0,1.08f,8))>.3f)throw new Exception("Incorrect T3 Campus arrival");report.campusActivated=true;}
            if(readback==null){readback=new GameObject("Temporary_Exit_Readback").AddComponent<FloorGuiReadback>();readback.Capture(Path.Combine(Path.GetDirectoryName(output),names[index]+".png"),width,height);return;}
            if(!readback.Done)return;if(readback.Error!=null)throw new Exception(readback.Error);UnityEngine.Object.Destroy(readback.gameObject);readback=null;prepared=false;index++;
        }
        catch(Exception e){report.error=e.ToString();File.WriteAllText(output,JsonUtility.ToJson(report,true));EditorApplication.update-=Observe;EditorApplication.Exit(1);}
    }
}
