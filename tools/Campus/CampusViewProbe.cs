using System;
using System.Collections;
using System.IO;
using System.Linq;
using HUSEscape.World;
using UnityEngine;
public sealed class CampusViewProbe : MonoBehaviour
{
    public string checkpointDirectory;public bool Ready{get;private set;}public string Error{get;private set;}
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);Application.runInBackground=true;Time.captureDeltaTime=1f/60f;
        for(int i=0;i<30;i++)yield return null;
        JourneySession session=null;
        try{var entry=FindObjectsByType<JourneySceneEntry>().Single();session=entry.Session;session.StorageDirectoryOverride=checkpointDirectory;entry.floorSave.StorageDirectoryOverride=checkpointDirectory;if(!entry.floorSave.TryContinue())throw new Exception(entry.floorSave.LastMessage);if(!session.TryLeaveFloor())throw new Exception(session.LastMessage);}
        catch(Exception e){Error=e.ToString();yield break;}
        float until=Time.realtimeSinceStartup+25;while(session.IsLoading){if(Time.realtimeSinceStartup>until){Error="Native scene transition timeout";yield break;}yield return null;}
        for(int i=0;i<90;i++)yield return null;Time.captureDeltaTime=0;Ready=true;
    }
}
