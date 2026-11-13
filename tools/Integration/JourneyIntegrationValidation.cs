// Stage in Assets on a disposable project only. Never add this probe to authored scenes.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HUSEscape.Core;
using HUSEscape.Persistence;
using HUSEscape.Player;
using HUSEscape.UI;
using HUSEscape.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class JourneyIntegrationValidation
{
    static JourneyIntegrationValidation()
    {
        if (SessionState.GetBool("JourneyIntegrationRunning", false)) EditorApplication.update += Observe;
    }
    public static void Run()
    {
        // AssetDatabase resolves package scripts and prefab overrides as well as local GUIDs.
        foreach (var item in EditorBuildSettings.scenes)
        {
            if (!item.enabled || AssetDatabase.AssetPathToGUID(item.path) != item.guid.ToString())
                throw new Exception("Invalid build scene: " + item.path);
            var scene = EditorSceneManager.OpenScene(item.path, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) != 0) throw new Exception("Missing script: " + go.name);
                foreach (var component in go.GetComponents<Component>().Where(c => c != null))
                {
                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (property.objectReferenceValue == null && property.objectReferenceEntityIdValue != EntityId.None)
                            throw new Exception("Missing reference: " + go.name + "/" + property.propertyPath);
                        var target = property.objectReferenceValue as Component;
                        var targetGo = property.objectReferenceValue as GameObject ?? (target != null ? target.gameObject : null);
                        if (targetGo != null && targetGo.scene.IsValid() && targetGo.scene != scene)
                            throw new Exception("Cross-scene reference: " + go.name + "/" + property.propertyPath);
                    }
                }
            }
        }
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity", OpenSceneMode.Single);
        // Extra unsaved scene keeps the probe out of Bootstrap's temporary saved snapshot.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        new GameObject("JourneyIntegrationProbe").AddComponent<JourneyIntegrationProbe>();
        SessionState.SetBool("JourneyIntegrationRunning", true);
        EditorApplication.update += Observe;
        EditorApplication.EnterPlaymode();
    }
    private static void Observe()
    {
        if (!EditorApplication.isPlaying) return;
        var probe = UnityEngine.Object.FindAnyObjectByType<JourneyIntegrationProbe>();
        if (probe == null || !probe.Finished) return;
        SessionState.SetBool("JourneyIntegrationRunning", false);
        EditorApplication.Exit(probe.Passed ? 0 : 1);
    }
}

#endif
