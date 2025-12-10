using Assets.Scripts.Events;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class DebugManager : MonoBehaviour
    {
        public static DebugManager Instance { get; private set; }

        [Header("Resource System")]
        [Tooltip("Logs for resource generation/spawning")]
        public bool EnableResourceSpawningDebug = false;

        [Tooltip("Logs for resource gathering, stages, and fragments")]
        public bool EnableResourceGatheringDebug = false;

        [Tooltip("Logs for fragment pool operations")]
        public bool EnablePoolingDebug = false;

        [Header("NPC System")]
        [Tooltip("Logs for NPC state machine transitions")]
        public bool EnableNPCStateDebug = false;

        [Tooltip("Logs for NPC movement and navigation")]
        public bool EnableNPCMovementDebug = false;

        [Tooltip("Logs for worker inventory operations")]
        public bool EnableInventoryDebug = false;

        [Header("Selection & Input")]
        [Tooltip("Logs for selection events")]
        public bool EnableSelectionDebug = false;

        [Tooltip("Logs for input and commands")]
        public bool EnableInputDebug = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() => EventBus.Clear();

        #region Static Log Methods

        /// <summary>
        /// Log a resource spawning/generation message.
        /// </summary>
        public static void LogSpawning(string message)
        {
            if (Instance != null && Instance.EnableResourceSpawningDebug)
                Debug.Log($"[Spawning] {message}");
        }

        /// <summary>
        /// Log a resource gathering message.
        /// </summary>
        public static void LogGathering(string message)
        {
            if (Instance != null && Instance.EnableResourceGatheringDebug)
                Debug.Log($"[Gathering] {message}");
        }

        /// <summary>
        /// Log a pooling operation message.
        /// </summary>
        public static void LogPooling(string message)
        {
            if (Instance != null && Instance.EnablePoolingDebug)
                Debug.Log($"[Pooling] {message}");
        }

        /// <summary>
        /// Log an NPC state transition message.
        /// </summary>
        public static void LogState(string message)
        {
            if (Instance != null && Instance.EnableNPCStateDebug)
                Debug.Log($"[State] {message}");
        }

        /// <summary>
        /// Log an NPC movement message.
        /// </summary>
        public static void LogMovement(string message)
        {
            if (Instance != null && Instance.EnableNPCMovementDebug)
                Debug.Log($"[Movement] {message}");
        }

        /// <summary>
        /// Log an inventory operation message.
        /// </summary>
        public static void LogInventory(string message)
        {
            if (Instance != null && Instance.EnableInventoryDebug)
                Debug.Log($"[Inventory] {message}");
        }

        /// <summary>
        /// Log a selection event message.
        /// </summary>
        public static void LogSelection(string message)
        {
            if (Instance != null && Instance.EnableSelectionDebug)
                Debug.Log($"[Selection] {message}");
        }

        /// <summary>
        /// Log an input/command message.
        /// </summary>
        public static void LogInput(string message)
        {
            if (Instance != null && Instance.EnableInputDebug)
                Debug.Log($"[Input] {message}");
        }

        /// <summary>
        /// Log a warning (always shown).
        /// </summary>
        public static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        /// <summary>
        /// Log an error (always shown).
        /// </summary>
        public static void LogError(string message)
        {
            Debug.LogError(message);
        }

        #endregion
    }
}