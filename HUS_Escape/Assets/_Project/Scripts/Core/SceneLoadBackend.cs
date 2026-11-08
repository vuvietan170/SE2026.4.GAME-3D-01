using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HUSEscape.Core
{
    // Instance-scoped engine boundary. State, reservation, and event logic stays in SceneLoader.
    internal abstract class SceneLoadBackend
    {
        internal abstract int FindBuildIndex(string scenePath);
        internal abstract SceneLoadOperation StartLoad(int buildIndex);
        internal abstract bool IsSceneActive(string scenePath);
    }

    internal abstract class SceneLoadOperation
    {
        internal abstract float Progress { get; }
        internal abstract bool IsDone { get; }
        internal abstract event Action Completed;
    }

    internal sealed class UnitySceneLoadBackend : SceneLoadBackend
    {
        private readonly LoadSceneMode mode;

        // Single is always the production default. Additive permits isolated native integration tests.
        internal UnitySceneLoadBackend(LoadSceneMode mode = LoadSceneMode.Single)
        {
            this.mode = mode;
        }

        internal override int FindBuildIndex(string scenePath)
        {
            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                if (string.Equals(SceneUtility.GetScenePathByBuildIndex(index), scenePath, StringComparison.Ordinal)
                    && Application.CanStreamedLevelBeLoaded(index))
                {
                    return index;
                }
            }

            return -1;
        }

        internal override SceneLoadOperation StartLoad(int buildIndex)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex, mode);
            return operation == null ? null : new UnitySceneLoadOperation(operation);
        }

        internal override bool IsSceneActive(string scenePath)
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() && scene.isLoaded && string.Equals(scene.path, scenePath, StringComparison.Ordinal);
        }

        private sealed class UnitySceneLoadOperation : SceneLoadOperation
        {
            private readonly AsyncOperation operation;

            internal UnitySceneLoadOperation(AsyncOperation operation)
            {
                this.operation = operation;
                operation.completed += OnCompleted;
            }

            internal override float Progress => operation.progress;
            internal override bool IsDone => operation.isDone;
            internal override event Action Completed;

            private void OnCompleted(AsyncOperation completed)
            {
                operation.completed -= OnCompleted;
                Completed?.Invoke();
            }
        }
    }
}
