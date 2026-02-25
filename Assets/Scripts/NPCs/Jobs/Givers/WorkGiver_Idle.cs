namespace Assets.Scripts.NPCs.Jobs.Givers
{
    /// <summary>
    /// Fallback WorkGiver that always returns null.
    ///
    /// Registered at the lowest priority (int.MaxValue) to serve as the
    /// bottom of the work-finding chain. When all other WorkGivers find
    /// nothing, this one confirms the pawn should idle.
    ///
    /// In a more advanced system, this could return a "wander" or "socialize"
    /// job instead of null, giving idle pawns something to do visually.
    /// </summary>
    public class WorkGiver_Idle : WorkGiver
    {
        public override int Priority => int.MaxValue;
        public override string Label => "Idle";

        public override Job TryGetJob(Pawn pawn)
        {
            return null;
        }
    }
}
