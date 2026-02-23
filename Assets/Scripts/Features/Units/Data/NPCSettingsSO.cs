using UnityEngine;

namespace Assets.Scripts.Data
{
    /// <summary>
    /// Configuration for NPC behavior settings.
    /// Centralizes magic numbers for designer-friendly editing.
    /// </summary>
    [CreateAssetMenu(fileName = "NPCSettings", menuName = "Config/NPC Settings")]
    public class NPCSettingsSO : ScriptableObject
    {
        [Header("Movement")]
        [Tooltip("Default distance at which NPCs consider themselves 'arrived' at a destination")]
        public float DefaultArrivalDistance = 2f;

        [Tooltip("Stopping distance for NavMeshAgent")]
        public float StoppingDistance = 0.5f;

        [Tooltip("Default movement speed")]
        public float DefaultMoveSpeed = 5f;

        [Tooltip("Time before an NPC is considered stuck")]
        public float StuckDetectionTime = 2f;

        [Tooltip("Minimum movement per frame to not be considered stuck")]
        public float StuckMovementThreshold = 0.01f;

        [Header("Gathering")]
        [Tooltip("Distance threshold for 'close enough' to continue working")]
        public float CloseEnoughDistance = 2f;

        [Tooltip("Default interaction distance for gathering resources")]
        public float DefaultInteractionDistance = 2f;

        [Header("Pickup")]
        [Tooltip("Small delay after pickup before deciding next action")]
        public float PostPickupDelay = 0.1f;

        [Tooltip("Threshold for detecting if a target has moved significantly")]
        public float TargetMoveThreshold = 0.1f;

        [Header("Building")]
        [Tooltip("Distance at which workers interact with building slots")]
        public float BuildingInteractionDistance = 3f;

        #region Singleton Instance

        private static NPCSettingsSO _instance;

        /// <summary>
        /// Runtime instance loaded from Resources folder.
        /// Create a NPCSettings asset in Resources/Config/ folder.
        /// </summary>
        public static NPCSettingsSO Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Resources.Load<NPCSettingsSO>("Config/NPCSettings");
                    if (_instance == null)
                    {
                        Debug.LogWarning("NPCSettingsSO not found in Resources/Config/. Using defaults.");
                        _instance = CreateInstance<NPCSettingsSO>();
                    }
                }
                return _instance;
            }
        }

        #endregion
    }
}
