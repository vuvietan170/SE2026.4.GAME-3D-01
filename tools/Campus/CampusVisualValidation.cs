using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HUSEscape.World;
using HUSEscape.Quests;
using HUSEscape.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[Serializable] public sealed class CampusFps { public string location;public float fps;public int frames;public double seconds; }
[Serializable] public sealed class CampusVisualReport {public bool passed;public string unity,gpu;public int images;public List<CampusFps> samples=new List<CampusFps>();public string error;}
public static class CampusVisualValidation
{
    private static CampusViewProbe probe;private static EditorWindow view;private static string output;private static int imageIndex,fpsIndex,firstFrame;private static double waitUntil;private static bool prepared;private static FloorGuiReadback readback;private static System.Diagnostics.Stopwatch watch;
    private static readonly Vector3[] places={new Vector3(0,1.08f,8),new Vector3(0,1.08f,20),new Vector3(-12,1.08f,28.5f)};
    private static readonly string[] names={"T3_Spawn","Courtyard","Campus_Map"};private static CampusVisualReport report;
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"-validationReport")+1];report=new CampusVisualReport{unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName};
        EditorSceneManager.OpenScene(CampusSetup.Floor,OpenSceneMode.Single);view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
        typeof(FloorGuiValidation).GetField("view",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,view);Resolution(1280,720);
        probe=new GameObject("Temporary_Campus_View").AddComponent<CampusViewProbe>();probe.checkpointDirectory=args[Array.IndexOf(args,"-checkpointDirectory")+1];EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();
    }
    private static void Resolution(int width,int height)=>typeof(FloorGuiValidation).GetMethod("UseResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{width,height});
    private static void Observe()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(probe==null)probe=Resources.FindObjectsOfTypeAll<CampusViewProbe>().FirstOrDefault(p=>p.gameObject.scene.IsValid());
            if(probe==null)return;if(probe.Error!=null)throw new Exception(probe.Error);if(!probe.Ready)return;
            var entry=UnityEngine.Object.FindObjectsByType<JourneySceneEntry>().Single();
            if(imageIndex<4)
            {
                int width=imageIndex==0?1280:1920,height=imageIndex==0?720:1080;
                if(!prepared)
                {
                    Resolution(width,height);entry.ui.CloseModal();
                    if(imageIndex==2){typeof(QuestPuzzleUI).GetMethod("Open",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(entry.ui,new object[]{QuestModal.Journal});typeof(QuestPuzzleUI).GetField("scroll",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(entry.ui,new Vector2(0,10000));}
                    prepared=true;waitUntil=EditorApplication.timeSinceStartup+3;return;
                }
                if(EditorApplication.timeSinceStartup<waitUntil)return;
                if(imageIndex==3&&readback==null){entry.ui.enabled=false;entry.viewCamera.GetComponent<ThirdPersonCameraFollow>().enabled=false;entry.viewCamera.orthographic=true;entry.viewCamera.orthographicSize=42;entry.viewCamera.transform.SetPositionAndRotation(new Vector3(0,80,25),Quaternion.Euler(90,0,0));}
                if(readback==null){readback=new GameObject("Temporary_Campus_Readback").AddComponent<FloorGuiReadback>();readback.Capture(Path.Combine(Path.GetDirectoryName(output),(imageIndex==2?"Campus_Journal":imageIndex==3?"Campus_Overview":"Campus_HUD")+"-"+width+"x"+height+".png"),width,height);return;}
                if(!readback.Done)return;if(readback.Error!=null)throw new Exception(readback.Error);UnityEngine.Object.Destroy(readback.gameObject);readback=null;imageIndex++;prepared=false;
                if(imageIndex==4){entry.viewCamera.orthographic=false;entry.viewCamera.GetComponent<ThirdPersonCameraFollow>().enabled=true;entry.ui.enabled=true;}
                return;
            }
            if(fpsIndex<places.Length)
            {
                if(!prepared){entry.player.enabled=false;entry.player.transform.SetPositionAndRotation(places[fpsIndex],Quaternion.identity);entry.player.enabled=true;waitUntil=EditorApplication.timeSinceStartup+3;prepared=true;return;}
                if(EditorApplication.timeSinceStartup<waitUntil)return;
                if(watch==null){firstFrame=Time.frameCount;watch=System.Diagnostics.Stopwatch.StartNew();return;}
                if(watch.Elapsed.TotalSeconds<5)return;
                if(EditorApplication.isPaused||Time.frameCount==firstFrame)throw new InvalidOperationException("Editor player loop stalled/paused; FPS is NOT TESTED at "+names[fpsIndex]);
                report.samples.Add(new CampusFps{location=names[fpsIndex],fps=(float)((Time.frameCount-firstFrame)/watch.Elapsed.TotalSeconds),frames=Time.frameCount-firstFrame,seconds=watch.Elapsed.TotalSeconds});fpsIndex++;prepared=false;watch=null;return;
            }
            report.passed=true;report.images=4;File.WriteAllText(output,JsonUtility.ToJson(report,true));EditorApplication.update-=Observe;EditorApplication.Exit(0);
        }
        catch(Exception e){report.error=e.ToString();File.WriteAllText(output,JsonUtility.ToJson(report,true));EditorApplication.update-=Observe;EditorApplication.Exit(1);}
    }
}
