using System;
using System.Collections;
using System.Linq;
using HUSEscape.World;
using UnityEngine;
public sealed class ExitVestibuleViewProbe : MonoBehaviour
{
    public string checkpointDirectory;
    public bool Ready{get;private set;}public string Error{get;private set;}
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);Application.runInBackground=true;Time.captureDeltaTime=1f/60f;
        for(int i=0;i<30;i++)yield return null;
        try{var entry=FindObjectsByType<JourneySceneEntry>().Single();entry.floorSave.StorageDirectoryOverride=checkpointDirectory;entry.Session.StorageDirectoryOverride=checkpointDirectory;if(!entry.floorSave.TryContinue())throw new Exception(entry.floorSave.LastMessage);}
        catch(Exception e){Error=e.ToString();yield break;}
        for(int i=0;i<90;i++)yield return null;Time.captureDeltaTime=0;Ready=true;
    }
}
