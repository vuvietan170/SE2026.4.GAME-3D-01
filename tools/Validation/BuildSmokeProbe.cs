using System;
using System.IO;
using System.Linq;
using HUSEscape.Core;
using HUSEscape.UI;
using HUSEscape.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Staged only into the disposable validation project; absent from the repository's game Assets.
public sealed class BuildSmokeProbe : MonoBehaviour
{
    [Serializable] private sealed class Report { public bool passed; public string unityVersion, stage, error; public int coreOwners, journeyOwners; }
    private string reportPath,stage="menu";private float deadline;private int floorFrame;private JourneySession previous;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        var args=Environment.GetCommandLineArgs();if(!args.Contains("--hus-build-smoke"))return;
        var index=Array.IndexOf(args,"--hus-smoke-report");
        if(index<0||index+1==args.Length)throw new Exception("--hus-smoke-report is required");
        var probe=new GameObject("BuildSmokeProbe").AddComponent<BuildSmokeProbe>();
        probe.reportPath=args[index+1];probe.deadline=Time.realtimeSinceStartup+90;
        DontDestroyOnLoad(probe.gameObject);Time.captureDeltaTime=1f/60f;Application.logMessageReceived+=probe.Log;
    }
    private void Log(string message,string trace,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Finish(false,message+"\n"+trace);}
    private void Update()
    {
        try
        {
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Player smoke stage: "+stage);
            var core=FindObjectsByType<GameBootstrap>().SingleOrDefault();if(core==null||!core.IsInitialized||core.Loader.IsLoading)return;
            if(stage=="returning")
            {
                // Destroy is deferred to end-of-frame; a menu may bind before that cleanup runs.
                if(SceneManager.GetActiveScene().path!=JourneySession.MenuPath || previous!=null)return;
                stage="menu-again";
            }
            if(stage=="menu"||stage=="menu-again")
            {
                var view=FindObjectsByType<MainMenuView>().SingleOrDefault();if(view==null||!view.CanStart)return;
                if(core.Manager.CurrentState!=GameState.Ready||FindObjectsByType<JourneySession>().Length!=0)throw new Exception("Menu retained gameplay session");
                if(stage=="menu-again"&&(Time.timeScale!=1||!Cursor.visible))throw new Exception("Menu environment not restored");
                stage=stage=="menu"?"floor":"fresh-floor";
                var button=view.GetComponentInChildren<Button>(true);button.onClick.Invoke();button.onClick.Invoke();button.onClick.Invoke();floorFrame=Time.frameCount;
            }
            else
            {
                var entry=FindObjectsByType<JourneySceneEntry>().SingleOrDefault();if(entry==null||entry.Session==null||Time.frameCount-floorFrame<20||entry.Gate.IsBlocked)return;
                if(entry.Session.Area!=JourneyArea.Floor01||entry.quests.CurrentIndex!=0||entry.inventory.ItemIds.Count!=0||FindObjectsByType<JourneySession>().Length!=1||core.Manager.CurrentState!=GameState.Playing)
                    throw new Exception("Floor01 not fresh or duplicate owners");
                if(stage=="floor")
                {
                    previous=entry.Session;
                    stage="returning";if(!previous.TryReturnToMenu())throw new Exception(previous.LastMessage);
                }
                else if(stage=="fresh-floor")
                {if(entry.Session==previous)throw new Exception("Menu reused old JourneySession");Finish(true,null);}
            }
        }
        catch(Exception error){Finish(false,error.ToString());}
    }
    private void Finish(bool passed,string error)
    {
        if(!enabled)return;enabled=false;Application.logMessageReceived-=Log;
        var report=new Report{passed=passed,unityVersion=Application.unityVersion,stage=stage,error=error,
            coreOwners=FindObjectsByType<GameBootstrap>().Length,journeyOwners=FindObjectsByType<JourneySession>().Length};
        File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));Application.Quit(passed?0:1);
    }
}
