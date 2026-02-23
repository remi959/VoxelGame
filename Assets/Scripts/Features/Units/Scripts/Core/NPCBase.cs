// ============================================================================
// NPCBase.cs - Base class for all NPCs
// ============================================================================
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.NPCs.States;
using UnityEngine;

namespace Assets.Scripts.NPCs
{
    /// <summary>
    /// Base class for all NPC units in the game.
    /// 
    /// Provides:
    /// - State machine integration
    /// - Motor (NavMesh navigation)
    /// - Selection handling
    /// - Centralized update registration
    /// - Ownership tracking for multi-player support
    /// 
    /// Subclasses (e.g., Worker) add specialized behavior.
    /// </summary>
    [RequireComponent(typeof(NPCMotor))]
    public abstract class NPCBase : MonoBehaviour
    {
        // ========== Static Registry ==========
        
        /// <summary>
        /// All active NPCs in the scene. Used for selection, queries, etc.
        /// </summary>
        private static readonly HashSet<NPCBase> allNPCs = new();
        
        /// <summary>
        /// Read-only access to all active NPCs.
        /// </summary>
        public static IEnumerable<NPCBase> All => allNPCs;
        
        /// <summary>
        /// Total count of active NPCs.
        /// </summary>
        public static int Count => allNPCs.Count;

        // ========== Components ==========

        protected NPCMotor motor;
        protected StateMachine stateMachine;

        // ========== Properties ==========

        [Header("NPC Settings")]
        [SerializeField] protected string npcName = "NPC";
        
        [Header("Ownership")]
        [Tooltip("Player who owns this NPC. Default 0 for single-player. Used for resource storage selection.")]
        [SerializeField] protected int ownerPlayerId = 0;

        [Header("Selection")]
        [Tooltip("Child GameObject that shows when this NPC is selected (e.g., a ring or highlight)")]
        [SerializeField] protected GameObject selectionIndicator;
        
        /// <summary>
        /// The NPC's navigation motor.
        /// </summary>
        public NPCMotor Motor => motor;

        /// <summary>
        /// The NPC's state machine.
        /// </summary>
        public StateMachine StateMachine => stateMachine;

        /// <summary>
        /// Display name for this NPC.
        /// </summary>
        public string NPCName => npcName;

        /// <summary>
        /// The player who owns this NPC.
        /// Used to determine which storage points to use, etc.
        /// </summary>
        public int OwnerPlayerId => ownerPlayerId;

        /// <summary>
        /// Whether this NPC is currently selected.
        /// </summary>
        public bool IsSelected { get; private set; }
        
        /// <summary>
        /// Set the owner of this NPC at runtime.
        /// Use sparingly - typically ownership is set in the Inspector or when spawning.
        /// </summary>
        public void SetOwner(int playerId)
        {
            ownerPlayerId = playerId;
        }

        // ========== Lifecycle ==========

        protected virtual void Awake()
        {
            motor = GetComponent<NPCMotor>();
        }

        protected virtual void Start()
        {
            InitializeStateMachine();
        }

        protected virtual void OnEnable()
        {
            // Register with static collection
            allNPCs.Add(this);
            
            // Ensure selection indicator starts hidden
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(false);
            }
            
            // Register with update manager for centralized updates
            if (NPCUpdateManager.Instance != null)
            {
                NPCUpdateManager.Instance.Register(this);
            }
        }

        protected virtual void OnDisable()
        {
            // Unregister from static collection
            allNPCs.Remove(this);
            
            // Unregister from update manager
            if (NPCUpdateManager.Instance != null)
            {
                NPCUpdateManager.Instance.Unregister(this);
            }
        }

        /// <summary>
        /// Initialize the state machine. Override in subclasses to add states.
        /// </summary>
        protected abstract void InitializeStateMachine();

        /// <summary>
        /// Called by NPCUpdateManager instead of Update().
        /// This avoids Unity's per-MonoBehaviour Update overhead.
        /// </summary>
        public virtual void UpdateNPC()
        {
            stateMachine?.Update();
        }

        // ========== Selection ==========

        /// <summary>
        /// Called when this NPC is selected.
        /// </summary>
        public virtual void OnSelected()
        {
            IsSelected = true;
            
            // Show selection indicator
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(true);
            }
        }

        /// <summary>
        /// Called when this NPC is deselected.
        /// </summary>
        public virtual void OnDeselected()
        {
            IsSelected = false;
            
            // Hide selection indicator
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(false);
            }
        }

        // ========== Commands ==========

        /// <summary>
        /// Move to a destination position.
        /// </summary>
        public virtual void MoveTo(Vector3 destination)
        {
            var moveState = stateMachine?.GetState<MoveToTargetState>();
            if (moveState != null)
            {
                moveState.SetTarget(destination, () => stateMachine.SetState<WorkerIdleState>());
                stateMachine.SetState<MoveToTargetState>();
            }
        }

        /// <summary>
        /// Interact with a target GameObject.
        /// Override in subclasses for specific interaction behavior.
        /// </summary>
        public virtual void InteractWith(GameObject target)
        {
            // Base implementation does nothing
            // Worker overrides this to handle resources, buildings, etc.
        }
    }
}
