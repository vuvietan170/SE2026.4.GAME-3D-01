using System;
using System.Linq;
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
        private void Start()
        {
            var sessions = FindObjectsByType<JourneySession>();
            if (sessions.Length > 1) { Debug.LogError("Multiple journey owners; scene binding refused."); return; }
            if (sessions.Length == 1) Session = sessions[0];
            else
            {
                var cores = FindObjectsByType<GameBootstrap>().Where(b => b.IsInitialized).ToArray();
                if (cores.Length > 1) { Debug.LogError("Multiple Core owners."); return; }
                var core = cores.Length == 1 ? cores[0] : new GameObject("Gameplay_Core").AddComponent<GameBootstrap>();
                Session = new GameObject("Journey_Session").AddComponent<JourneySession>(); Session.Initialize(core);
            }
            Session.Attach(this);
        }
        internal void Bind(JourneySession session) { Session = session; ui.BindJourney(session); }
        private void OnDestroy() { if (Session != null) Session.Detach(this); }
    }
}
