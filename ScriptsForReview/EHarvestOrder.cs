namespace Assets.Scripts.Shared.Enums
{
    public enum EHarvestOrder
    {
        Random,         // Pieces are harvested in random order
        Sequential,     // Pieces are harvested in hierarchy order (first child first)
        ReverseSequential, // Pieces are harvested in reverse hierarchy order (last child first)
        Closest,             // Pieces are harvested by closest to the worker
        ClosestEndFirst
    }
}