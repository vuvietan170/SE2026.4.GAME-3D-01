using UnityEngine;

namespace HUSEscape.Core
{
    /// <summary>Composes and preserves one active Core root without a global instance accessor.</summary>
    /// <remarks>Attach to a dedicated, active root GameObject with an enabled SceneLoader.</remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SceneLoader))]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private bool rejected;

        /// <summary>Gets this root's manager, or null if initialization was rejected.</summary>
        public GameManager Manager { get; private set; }

        /// <summary>Gets this root's scene loader, or null before Awake.</summary>
        public SceneLoader Loader { get; private set; }

        /// <summary>Gets whether this root successfully initialized its Core systems.</summary>
        public bool IsInitialized => Manager != null && Manager.IsInitialized;

        /// <summary>Retrieves services only from an initialized, enabled owner with an active loader.</summary>
        /// <param name="manager">The initialized manager, or null when unavailable.</param>
        /// <param name="loader">The initialized loader, or null when unavailable.</param>
        /// <returns>False before initialization, while disabled, or for a rejected duplicate.</returns>
        public bool TryGetServices(out GameManager manager, out SceneLoader loader)
        {
            manager = null;
            loader = null;
            if (rejected || !IsInitialized || !isActiveAndEnabled || Loader == null || !Loader.isActiveAndEnabled
                || !Loader.IsInitializedFor(Manager))
            {
                return false;
            }

            manager = Manager;
            loader = Loader;
            return true;
        }

        private void Awake()
        {
            if (enabled)
            {
                InitializeCore();
            }
        }

        private void InitializeCore()
        {
            if (rejected || IsInitialized)
            {
                return;
            }

            Loader = GetComponent<SceneLoader>();
            if (transform.parent != null || !Loader.enabled)
            {
                Reject("GameBootstrap requires a root GameObject and an enabled SceneLoader.");
                return;
            }

            // An initialized root wins across scene transitions, including temporary deactivation.
            // Otherwise elect the lowest active entity ID, independent of Awake execution order.
            GameBootstrap[] bootstraps = FindObjectsByType<GameBootstrap>(FindObjectsInactive.Include);
            GameBootstrap owner = SelectOwner(bootstraps);

            if (owner != this)
            {
                Reject("A GameBootstrap already owns Core initialization; this duplicate is disabled.");
                return;
            }

            foreach (GameBootstrap candidate in bootstraps)
            {
                if (candidate != this && candidate.isActiveAndEnabled)
                {
                    candidate.Reject("A GameBootstrap already owns Core initialization; this duplicate is disabled.");
                }
            }

            Manager = new GameManager();
            Loader.Initialize(Manager);
            DontDestroyOnLoad(gameObject);
            Manager.Initialize();
        }

        internal static GameBootstrap SelectOwner(GameBootstrap[] bootstraps)
        {
            System.Array.Sort(bootstraps, (left, right) => left.GetEntityId().CompareTo(right.GetEntityId()));
            GameBootstrap owner = null;
            foreach (GameBootstrap candidate in bootstraps)
            {
                if (candidate.IsInitialized)
                {
                    owner = candidate;
                    break;
                }

                if (owner == null && !candidate.rejected && candidate.isActiveAndEnabled && candidate.transform.parent == null
                    && candidate.GetComponent<SceneLoader>().enabled)
                {
                    owner = candidate;
                }
            }

            return owner;
        }

        private void OnEnable()
        {
            if (rejected)
            {
                Reject("This GameBootstrap did not initialize and cannot be enabled.");
                return;
            }

            if (IsInitialized)
            {
                Loader.enabled = true;
            }
            else
            {
                InitializeCore();
            }
        }

        private void OnDisable()
        {
            // Disabling suspends access, not ownership or manager state. Native loads still finish.
            if (IsInitialized && Loader != null)
            {
                Loader.enabled = false;
            }
        }

        private void Reject(string reason)
        {
            rejected = true;
            enabled = false;
            GetComponent<SceneLoader>().enabled = false;
            Debug.LogError(reason, this);
        }
    }
}
