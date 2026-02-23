using Assets.Scripts.Shared.Utilities;
using UnityEngine;

namespace Assets.Scripts.Core.Input
{
    /// <summary>
    /// Cached layer indices and masks.
    /// Avoids repeated LayerMask.NameToLayer() and LayerMask.GetMask() calls.
    /// </summary>
    public static class LayerManager
    {
        #region Layer Indices

        /// <summary>Ground layer index</summary>
        public static int Ground { get; private set; }

        /// <summary>NPC layer index</summary>
        public static int NPC { get; private set; }

        /// <summary>Interactable layer index</summary>
        public static int Interactable { get; private set; }

        #endregion

        #region Layer Masks

        /// <summary>Mask for selectable objects (Ground + NPC)</summary>
        public static LayerMask SelectableMask { get; private set; }

        /// <summary>Mask for command targets (Ground + Interactable)</summary>
        public static LayerMask CommandMask { get; private set; }

        /// <summary>Mask for ground only</summary>
        public static LayerMask GroundMask { get; private set; }

        /// <summary>Mask for interactable objects only</summary>
        public static LayerMask InteractableMask { get; private set; }

        #endregion

        #region Initialization

        private static bool isInitialized = false;

        /// <summary>
        /// Initialize layer caches. Called automatically before scene load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (isInitialized) return;

            // Cache layer indices
            Ground = LayerMask.NameToLayer(Strings.GroundLayerName);
            NPC = LayerMask.NameToLayer(Strings.NPCLayerName);
            Interactable = LayerMask.NameToLayer(Strings.InteractableLayerName);

            // Cache layer masks
            SelectableMask = LayerMask.GetMask(Strings.GroundLayerName, Strings.NPCLayerName);
            CommandMask = LayerMask.GetMask(Strings.GroundLayerName, Strings.InteractableLayerName);
            GroundMask = LayerMask.GetMask(Strings.GroundLayerName);
            InteractableMask = LayerMask.GetMask(Strings.InteractableLayerName);

            isInitialized = true;

            Debug.Log($"[LayerManager] Initialized - Ground:{Ground}, NPC:{NPC}, Interactable:{Interactable}");
        }

        /// <summary>
        /// Force re-initialization (useful if layers change at runtime).
        /// </summary>
        public static void Reinitialize()
        {
            isInitialized = false;
            Initialize();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Check if a layer index matches the ground layer.
        /// </summary>
        public static bool IsGround(int layer) => layer == Ground;

        /// <summary>
        /// Check if a layer index matches the NPC layer.
        /// </summary>
        public static bool IsNPC(int layer) => layer == NPC;

        /// <summary>
        /// Check if a layer index matches the interactable layer.
        /// </summary>
        public static bool IsInteractable(int layer) => layer == Interactable;

        /// <summary>
        /// Check if a GameObject is on the ground layer.
        /// </summary>
        public static bool IsGround(GameObject obj) => obj.layer == Ground;

        /// <summary>
        /// Check if a GameObject is on the NPC layer.
        /// </summary>
        public static bool IsNPC(GameObject obj) => obj.layer == NPC;

        /// <summary>
        /// Check if a GameObject is on the interactable layer.
        /// </summary>
        public static bool IsInteractable(GameObject obj) => obj.layer == Interactable;

        #endregion
    }
}