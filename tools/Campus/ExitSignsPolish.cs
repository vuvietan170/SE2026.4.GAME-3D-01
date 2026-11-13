using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class ExitSignsPolish
{
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();string output=args[Array.IndexOf(args,"-validationReport")+1];var scene=EditorSceneManager.OpenScene(CampusSetup.Floor,OpenSceneMode.Single);int changed=0;
        foreach(var text in GameObject.Find("T3_ExitVestibule").GetComponentsInChildren<Text>())
        {
            var rect=text.transform.parent.GetComponent<RectTransform>();
            if(text.text=="LOI RA CAMPUS"||text.text=="LOI RA CAMPUS ->"){text.text="LOI RA CAMPUS ->";rect.SetPositionAndRotation(new Vector3(-6.985f,3.1f,59),Quaternion.Euler(0,-90,0));rect.sizeDelta=new Vector2(380,85);changed++;}
            else if(text.text.StartsWith("SANH THOAT T3")){rect.SetPositionAndRotation(new Vector3(0,4.2f,61.98f),Quaternion.identity);rect.sizeDelta=new Vector2(580,85);changed++;}
            else if(text.text.StartsWith("CUA RA TOA NHA T3")){rect.SetPositionAndRotation(new Vector3(-3.085f,2.8f,68),Quaternion.Euler(0,-90,0));rect.sizeDelta=new Vector2(360,120);changed++;}
        }
        if(changed!=3)throw new InvalidOperationException("Unexpected sign layout; inspect before saving.");EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(output,"{\"passed\":true,\"wallMountedSigns\":3}");EditorApplication.Exit(0);
    }
}
