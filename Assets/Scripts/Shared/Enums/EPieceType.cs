namespace Assets.Scripts.Shared.Enums
{
    /// <summary>
    /// Defines where a piece can be placed in a resource structure.
    /// </summary>
    public enum EPieceType
    {
        /// <summary>
        /// Can be placed anywhere (default for simple resources like stone)
        /// </summary>
        Any,

        /// <summary>
        /// Can only be placed at the bottom (index 0)
        /// </summary>
        Base,

        /// <summary>
        /// Can be placed in the middle section (not first, not last)
        /// </summary>
        Middle,

        /// <summary>
        /// Can be placed from start to second-to-last position
        /// </summary>
        Trunk,

        /// <summary>
        /// Can only be placed at the top (last position)
        /// </summary>
        Top
    }
}