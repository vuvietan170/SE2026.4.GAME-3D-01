using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using HUSEscape.Quests;
using HUSEscape.Persistence;
using UnityEditor.SceneManagement;

public static class FloorGuiValidation
{
    private static EditorWindow view;
    private static int warmup, index;
    private static string output;
    private static readonly string[] Modes={"HUD","Journal","Clue","Code","Sequence","Lab","Progress"};
    private static System.Diagnostics.Stopwatch fps;
    private static int firstFrame;
    private static bool prepared;
    private static FloorGuiReadback readback;
    private static double settleUntil;
    private static readonly System.Collections.Generic.Dictionary<int,int> sizeIndices=new System.Collections.Generic.Dictionary<int,int>();
    private static void UseResolution(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        object sizes=sizesType.BaseType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        object groupType=view.GetType().GetProperty("currentSizeGroupType",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static).GetValue(view);
        object group=sizesType.GetMethod("GetGroup",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sizes,new[]{groupType});
        if(!sizeIndices.TryGetValue(width,out int index))
        {
            var type=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
            object size=Activator.CreateInstance(type,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{Enum.Parse(kind,"FixedResolution"),width,height,"UI verification "+width},null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            index=(int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group,null)+(int)group.GetType().GetMethod("GetCustomCount").Invoke(group,null)-1;sizeIndices[width]=index;
        }
        view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,index);
    }
    public static void Capture()
    {
        var args=Environment.GetCommandLineArgs();output=args[Array.IndexOf(args,"-validationReport")+1];
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Floor01_Prototype.unity",OpenSceneMode.Single);
        view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();
        warmup=0;index=0;prepared=false;settleUntil=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Observe;EditorApplication.EnterPlaymode();
    }
    private static void Observe()
    {
        if(!EditorApplication.isPlaying)return;
        try
        {
            if(warmup++<35||EditorApplication.timeSinceStartup<settleUntil)return;
            var ui=UnityEngine.Object.FindObjectsByType<QuestPuzzleUI>().Single();
            var play=typeof(Editor).Assembly.GetType("UnityEditor.PlayModeView");
            if(index<14)
            {
                int width=index<7?1280:1920,height=index<7?720:1080;string mode=Modes[index%7];
                if(!prepared)
                {
                ui.CloseModal();
                var quests=UnityEngine.Object.FindObjectsByType<QuestManager>().Single();
                if(mode=="HUD") {quests.RestoreFacts(Array.Empty<string>());ui.ProgressRestored(true);}
                else
                {
                    quests.RestoreFacts(quests.Definitions.Select(q=>q.CompletionEvent).Where(f=>f!="lab:complete").ToArray());
                    typeof(QuestPuzzleUI).GetMethod("Open",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{Enum.Parse(typeof(QuestModal),mode)});
                    if(mode=="Clue")Set(ui,"currentNote",UnityEngine.Object.FindObjectsByType<ClueNote>().Single(n=>n.name=="A101_ClueNote"));
                    if(mode=="Code"){Set(ui,"currentCabinet",UnityEngine.Object.FindObjectsByType<CodeLockCabinet>().Single());for(int i=0;i<4;i++)ui.AppendDigit(9);ui.SubmitCode();}
                    if(mode=="Sequence"){Set(ui,"currentSequence",UnityEngine.Object.FindObjectsByType<SequenceLockPanel>().Single());ui.SelectEvent(0);ui.SelectEvent(1);ui.SelectEvent(2);ui.SubmitSequence();}
                    if(mode=="Lab")
                    {
                        var lab=UnityEngine.Object.FindObjectsByType<LabRestoreSystem>().Single();lab.RestoreState(true,true,3,true,false);
                        Set(ui,"currentLab",UnityEngine.Object.FindObjectsByType<LabInteractable>().Single(t=>t.Kind==LabInteractionKind.Computer));Set(ui,"message",lab.Interact(LabInteractionKind.Computer));
                    }
                }
                play.GetProperty("targetSize",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,new Vector2(width,height));
                UseResolution(width,height);
                play.GetProperty("renderIMGUI",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,true);
                prepared=true;warmup=27;return;
                }
                if(readback==null){readback=new GameObject("Temporary_GUI_Readback").AddComponent<FloorGuiReadback>();readback.Capture(Path.Combine(Path.GetDirectoryName(output),mode+"-"+width+"x"+height+".png"),width,height);return;}
                if(!readback.Done)return;
                if(readback.Error!=null)throw new InvalidOperationException(readback.Error);
                UnityEngine.Object.Destroy(readback.gameObject);readback=null;
                index++;prepared=false;warmup=20;return;
            }
            if(fps==null){ui.CloseModal();Time.captureDeltaTime=0;firstFrame=Time.frameCount;fps=System.Diagnostics.Stopwatch.StartNew();}
            if(fps.Elapsed.TotalSeconds<5)return;
            File.WriteAllText(output,"{\"passed\":true,\"images\":14,\"fps\":"+((Time.frameCount-firstFrame)/fps.Elapsed.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"frames\":"+(Time.frameCount-firstFrame)+",\"seconds\":"+fps.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"device\":\""+SystemInfo.graphicsDeviceName+"\",\"vsync\":"+QualitySettings.vSyncCount+",\"targetFrameRate\":"+Application.targetFrameRate+"}");
            EditorApplication.update-=Observe;EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(output,e.ToString());EditorApplication.update-=Observe;EditorApplication.Exit(1);}
    }
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    public static void Dump()
    {
        var args=Environment.GetCommandLineArgs();string path=args[Array.IndexOf(args,"-validationReport")+1];var text=new StringBuilder();
        foreach(string name in new[]{"UnityEditor.GameView","UnityEditor.PlayModeView","UnityEditor.GUIView","UnityEditor.GameViewSizes","UnityEditor.GameViewSizeGroup","UnityEditor.GameViewSize"})
        {
            var type=typeof(Editor).Assembly.GetType(name);text.AppendLine(name);
            foreach(var member in type.GetMembers(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static).Where(m=>m.Name.IndexOf("render",StringComparison.OrdinalIgnoreCase)>=0||m.Name.IndexOf("size",StringComparison.OrdinalIgnoreCase)>=0||m.Name.IndexOf("pixel",StringComparison.OrdinalIgnoreCase)>=0||m.Name.IndexOf("texture",StringComparison.OrdinalIgnoreCase)>=0))text.AppendLine(member.ToString());
        }
        File.WriteAllText(path,text.ToString());EditorApplication.Exit(0);
    }
}
