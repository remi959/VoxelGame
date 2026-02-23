// ============================================================================
// NPCUpdateManager.cs - Centralized NPC update loop
// ============================================================================
using System.Collections.Generic;
using Assets.Scripts.Core.Services;
using UnityEngine;

namespace Assets.Scripts.NPCs
{
    /// <summary>
    /// Centralized update manager for all NPCs.
    /// 
    /// Why this exists:
    /// - Unity's Update() has significant overhead per call (~0.1ms marshalling)
    /// - With 1000 NPCs, that's ~100ms wasted on interop alone
    /// - A single Update() iterating all NPCs is 10-100x faster
    /// 
    /// Performance characteristics:
    /// - Single Update() call regardless of NPC count
    /// - Direct List iteration (cache-friendly)
    /// - No virtual dispatch overhead
    /// - No Unity interop per-NPC
    /// 
    /// Usage:
    /// - NPCs register in OnEnable, unregister in OnDisable
    /// - NPCBase.UpdateNPC() is called instead of Update()
    /// - Order is registration order (deterministic)
    /// 
    /// PERSISTENCE:
    /// This component should be placed as a child of the [Services] GameObject.
    /// PersistentServices handles DontDestroyOnLoad for the entire hierarchy.
    /// </summary>
    public class NPCUpdateManager : MonoBehaviour
    {
        public static NPCUpdateManager Instance { get; private set; }

        // Use List for cache-friendly iteration (no enumerator allocation)
        private readonly List<NPCBase> npcs = new(256);  // Pre-allocate for typical game
        
        // Pending adds/removes to avoid modifying during iteration
        private readonly List<NPCBase> pendingAdd = new(32);
        private readonly List<NPCBase> pendingRemove = new(32);
        private bool isUpdating;

        // ========== Statistics (for debugging) ==========
        
        public int ActiveNPCCount => npcs.Count;
        public int PendingAddCount => pendingAdd.Count;
        public int PendingRemoveCount => pendingRemove.Count;

        // ========== Lifecycle ==========

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ServiceLocator.Register(this);
            
            // Register any NPCs that enabled before this manager was ready
            // This handles the case where NPCs exist in the scene before the manager
            foreach (var npc in NPCBase.All)
            {
                if (npc != null && npc.isActiveAndEnabled)
                {
                    Register(npc);
                }
            }
        }

        private void OnDestroy()
        {
            npcs.Clear();
            pendingAdd.Clear();
            pendingRemove.Clear();
            
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister<NPCUpdateManager>();
            }
        }

        private void Update()
        {
            // Process pending registrations
            ProcessPendingChanges();

            // Update all NPCs in single loop
            isUpdating = true;
            int count = npcs.Count;
            for (int i = 0; i < count; i++)
            {
                var npc = npcs[i];
                if (npc != null && npc.isActiveAndEnabled)
                {
                    npc.UpdateNPC();
                }
            }
            isUpdating = false;

            // Process any changes that occurred during update
            ProcessPendingChanges();
        }

        // ========== Registration ==========

        /// <summary>
        /// Register an NPC to receive updates.
        /// Safe to call during iteration.
        /// </summary>
        public void Register(NPCBase npc)
        {
            if (npc == null) return;

            if (isUpdating)
            {
                // Defer until after iteration
                if (!pendingAdd.Contains(npc))
                    pendingAdd.Add(npc);
            }
            else
            {
                if (!npcs.Contains(npc))
                    npcs.Add(npc);
            }
        }

        /// <summary>
        /// Unregister an NPC from updates.
        /// Safe to call during iteration.
        /// </summary>
        public void Unregister(NPCBase npc)
        {
            if (npc == null) return;

            if (isUpdating)
            {
                // Defer until after iteration
                if (!pendingRemove.Contains(npc))
                    pendingRemove.Add(npc);
            }
            else
            {
                npcs.Remove(npc);
            }
        }

        // ========== Internal ==========

        private void ProcessPendingChanges()
        {
            // Process removals first (more common during gameplay)
            if (pendingRemove.Count > 0)
            {
                foreach (var npc in pendingRemove)
                {
                    npcs.Remove(npc);
                }
                pendingRemove.Clear();
            }

            // Process additions
            if (pendingAdd.Count > 0)
            {
                foreach (var npc in pendingAdd)
                {
                    if (!npcs.Contains(npc))
                        npcs.Add(npc);
                }
                pendingAdd.Clear();
            }
        }
    }
}
