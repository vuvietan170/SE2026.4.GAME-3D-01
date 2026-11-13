#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class TransactionValidation
{
    static TransactionValidation() { if (SessionState.GetBool("SessionTransactions", false)) EditorApplication.update += Observe; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("TransactionTests").AddComponent<TransactionProbe>();
        SessionState.SetBool("SessionTransactions", true);
        EditorApplication.update += Observe;
        EditorApplication.EnterPlaymode();
    }
    static void Observe()
    {
        if (!EditorApplication.isPlaying) return;
        var probe = Object.FindAnyObjectByType<TransactionProbe>();
        if (probe == null || !probe.Finished) return;
        SessionState.SetBool("SessionTransactions", false);
        EditorApplication.Exit(probe.Passed ? 0 : 1);
    }
}
#endif
