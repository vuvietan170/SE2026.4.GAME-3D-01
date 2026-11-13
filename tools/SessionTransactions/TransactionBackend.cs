#if UNITY_EDITOR
using System;
using System.Reflection;
namespace HUSEscape.Core
{
    // Staged into Core on a disposable project. Only the engine boundary is controlled.
    public static class TransactionBackendControl
    {
        private sealed class Backend : SceneLoadBackend
        {
            internal Operation Pending;
            internal bool Success;
            internal bool Reject;
            internal override int FindBuildIndex(string path) => Reject ? -1 : 1;
            internal override SceneLoadOperation StartLoad(int index) => Pending = new Operation();
            internal override bool IsSceneActive(string path) => Success;
        }
        private sealed class Operation : SceneLoadOperation
        {
            internal override float Progress => 0;
            internal override bool IsDone => false;
            internal override event Action Completed;
            internal void Complete() => Completed?.Invoke();
        }
        public static object Install(SceneLoader loader)
        {
            var backend = new Backend();
            typeof(SceneLoader).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(loader, backend);
            return backend;
        }
        public static void UseNative(SceneLoader loader) => typeof(SceneLoader).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(loader, new UnitySceneLoadBackend());
        public static void Reject(object value, bool reject) => ((Backend)value).Reject = reject;
        public static void Complete(object value, bool success)
        { var backend = (Backend)value; backend.Success = success; backend.Pending.Complete(); }
    }
}
#endif
