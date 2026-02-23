namespace Assets.Scripts.Shared.Enums
{
    /// <summary>
    /// Types of interactions available in the game.
    /// Used by IInteractable to identify the interaction type.
    /// </summary>
    public enum InteractionType
    {
        /// <summary>No interaction / default.</summary>
        None,

        /// <summary>Harvestable resource (trees, rocks, etc.).</summary>
        Resource,

        /// <summary>Building blueprint under construction.</summary>
        Blueprint,

        /// <summary>Resource fragment that can be picked up.</summary>
        Fragment,

        /// <summary>Storage point for depositing resources.</summary>
        Storage,

        /// <summary>Crafting bench for creating parts.</summary>
        CraftingBench,

        /// <summary>World resource pickup (dropped resources).</summary>
        Pickup,

        /// <summary>Completed building that can be used.</summary>
        Building
    }
}
