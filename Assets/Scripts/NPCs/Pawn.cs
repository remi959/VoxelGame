using System.Collections.Generic;
using Assets.Scripts.Events;
using Assets.Scripts.NPCs.Jobs;
using Assets.Scripts.NPCs.Jobs.Givers;
using Assets.Scripts.NPCs.Modules;
using UnityEngine;

namespace Assets.Scripts.NPCs
{
    /// <summary>
    /// The central identity of an NPC. Acts as a hub that wires together
    /// all pawn subsystems (motor, health, jobs, selection).
    ///
    /// Intentionally thin — behavior lives in Jobs, state lives in Modules.
    /// This mirrors RimWorld's Pawn: a container, not a god class.
    /// </summary>
    [RequireComponent(typeof(PawnMotor))]
    [RequireComponent(typeof(PawnHealth))]
    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(JobTracker))]
    public class Pawn : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string pawnName = "Colonist";

        public string PawnName => pawnName;
        public float WorkSpeed => 1f; // Base work speed (can be modified by health conditions, etc.)

        // Component references (resolved once in Awake)
        public PawnMotor Motor { get; private set; }
        public PawnHealth Health { get; private set; }
        public Selectable Selectable { get; private set; }
        public JobTracker Jobs { get; private set; }

        // Global registry of all living pawns
        private static readonly HashSet<Pawn> allPawns = new();
        public static IReadOnlyCollection<Pawn> All => allPawns;

        private void Awake()
        {
            Motor = GetComponent<PawnMotor>();
            Health = GetComponent<PawnHealth>();
            Selectable = GetComponent<Selectable>();
            Jobs = GetComponent<JobTracker>();

            Health.OnDeath += HandleDeath;
        }

        private void Start()
        {
            Jobs.RegisterWorkGiver(new WorkGiver_Mine());
            Jobs.RegisterWorkGiver(new WorkGiver_Farm());
        }

        private void OnEnable()
        {
            allPawns.Add(this);
            EventBus.Publish(new NPCSpawnedEvent { NPC = gameObject });
        }

        private void OnDisable()
        {
            allPawns.Remove(this);
        }

        private void OnDestroy()
        {
            Health.OnDeath -= HandleDeath;
        }

        private void HandleDeath()
        {
            Jobs.EndCurrentJob();
            Selectable.Deselect();
            Destroy(gameObject);
        }
    }
}
