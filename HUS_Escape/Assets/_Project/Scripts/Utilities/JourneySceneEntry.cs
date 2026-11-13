using System;
using System.Linq;
using System.Collections;
using HUSEscape.Core;
using HUSEscape.Inventory;
using HUSEscape.Player;
using HUSEscape.Quests;
using HUSEscape.Persistence;
using HUSEscape.AI;
using UnityEngine;

namespace HUSEscape.World
{
    /// <summary>Explicit scene composition. Actor assets are reused; scene-local actors never persist across loads.</summary>
    public sealed class JourneySceneEntry : MonoBehaviour
    {
        public bool isCampus;
        public bool isTrainingOffice;
        public string officePath = "Assets/_Project/Scenes/TrainingOffice_Prototype.unity";
        public string floorPath = "Assets/_Project/Scenes/Floor01_Prototype.unity";
        public string campusPath = "Assets/_Project/Scenes/Campus_Prototype.unity";
        public GameObject actorRoot;
        public CharacterController player;
        public Camera viewCamera;
        public PlayerInventory inventory;
        public QuestManager quests;
        public QuestPuzzleUI ui;
        public Floor01SaveSystem floorSave;
        public Transform spawn;
        public PatrolGuard[] guards = Array.Empty<PatrolGuard>();
        public JourneySession Session { get; private set; }
        public GameplayInputGate Gate => player.GetComponent<GameplayInputGate>();
        private IEnumerator Start()
        {
            // A native scene can run Start before SceneLoader commits Playing. Keep
            // actors gated until completion; never create another Core to bypass Loading.
            if (Session != null) yield break; // Completion may already have attached us.
            if (actorRoot == null || actorRoot.activeInHierarchy) Gate.Block();
            var cores = FindObjectsByType<GameBootstrap>(FindObjectsInactive.Include)
                .Where(b => b.IsInitialized).ToArray();
            if (cores.Length > 1) { Debug.LogError("Multiple Core owners.", this); yield break; }
            var core = cores.SingleOrDefault();
            if (core == null)
            {
                if (FindObjectsByType<GameBootstrap>(FindObjectsInactive.Include).Length != 0)
                { Debug.LogError("Existing Core owner is unavailable.", this); yield break; }
                core = new GameObject("Gameplay_Core").AddComponent<GameBootstrap>();
            }
            while (core != null && core.Loader != null && core.Loader.IsLoading) yield return null;
            if (Session != null) yield break; // Session.Loaded owns travel restoration.
            if (core == null || !core.TryGetServices(out var manager, out _)
                || (manager.CurrentState != GameState.Ready && manager.CurrentState != GameState.Playing))
            { Debug.LogError("Journey requires an available, stable Core owner.", this); yield break; }

            var sessions = FindObjectsByType<JourneySession>(FindObjectsInactive.Include);
            if (sessions.Length > 1) { Debug.LogError("Multiple journey owners; scene binding refused.", this); yield break; }
            if (sessions.Length == 1) Session = sessions[0];
            else
            {
                Session = new GameObject("Journey_Session").AddComponent<JourneySession>();
                Session.Initialize(core);
            }
            Session.Attach(this);
        }
        internal void Bind(JourneySession session) { Session = session; ui.BindJourney(session); }
        private void OnDestroy() { if (Session != null) Session.Detach(this); }
    }
}
