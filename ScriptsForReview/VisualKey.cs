using Assets.Scripts.Shared.Enums;
using Assets.Scripts.Shared.Utilities;

namespace Assets.Scripts.Shared.Pooling
{
    /// <summary>
    /// Identifies a unique visual variant for object pooling.
    /// Can be used for any pooled object type (fragments, projectiles, effects, etc.)
    /// </summary>
    [System.Serializable]
    public struct VisualKey : System.IEquatable<VisualKey>
    {
        /// <summary>
        /// Category of the object (e.g., "Wood", "Stone", "Projectile", "Effect")
        /// </summary>
        public string Category;

        /// <summary>
        /// Specific variant within the category (e.g., "oak", "pine", "arrow", "fireball")
        /// </summary>
        public string VariantId;

        /// <summary>
        /// Optional piece type for structural pieces (e.g., Trunk, Top)
        /// </summary>
        public EPieceType PieceType;

        public VisualKey(string category, string variant = "default", EPieceType pieceType = EPieceType.Any)
        {
            Category = string.IsNullOrEmpty(category) ? "default" : category;
            VariantId = string.IsNullOrEmpty(variant) ? "default" : variant;
            PieceType = pieceType;
        }

        public readonly bool Equals(VisualKey other)
        {
            return Category == other.Category 
                && VariantId == other.VariantId 
                && PieceType == other.PieceType;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is VisualKey other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return System.HashCode.Combine(Category, VariantId, PieceType);
        }

        public override readonly string ToString()
        {
            if (PieceType == EPieceType.Any)
                return $"{Category}:{VariantId}";
            return $"{Category}:{VariantId}:{PieceType}";
        }

        public static bool operator ==(VisualKey left, VisualKey right) => left.Equals(right);
        public static bool operator !=(VisualKey left, VisualKey right) => !left.Equals(right);

        #region Factory Methods

        /// <summary>
        /// Create a key for resource fragments.
        /// </summary>
        public static VisualKey ForResource(EResourceType type, string variant = "default", EPieceType pieceType = EPieceType.Any)
        {
            return new VisualKey(type.ToString(), variant, pieceType);
        }

        /// <summary>
        /// Create a key for resource fragments, parsing variant and piece type from prefab name.
        /// </summary>
        public static VisualKey ForResourceFromPrefab(EResourceType type, string prefabName)
        {
            PrefabNamingUtility.Parse(prefabName, out string variantId, out EPieceType pieceType);
            return new VisualKey(type.ToString(), variantId, pieceType);
        }

        /// <summary>
        /// Create a key for resource fragments, parsing from a prefab GameObject.
        /// </summary>
        public static VisualKey ForResourceFromPrefab(EResourceType type, UnityEngine.GameObject prefab)
        {
            return ForResourceFromPrefab(type, prefab != null ? prefab.name : "default");
        }

        /// <summary>
        /// Create a key for projectiles.
        /// </summary>
        public static VisualKey ForProjectile(string projectileType, string variant = "default")
        {
            return new VisualKey($"Projectile_{projectileType}", variant);
        }

        /// <summary>
        /// Create a key for visual effects.
        /// </summary>
        public static VisualKey ForEffect(string effectType, string variant = "default")
        {
            return new VisualKey($"Effect_{effectType}", variant);
        }

        #endregion

        #region Matching

        /// <summary>
        /// Check if this key matches another, optionally ignoring piece type.
        /// </summary>
        public readonly bool Matches(VisualKey other, bool ignorePieceType = false)
        {
            if (Category != other.Category || VariantId != other.VariantId)
                return false;

            if (ignorePieceType)
                return true;

            return PieceType == other.PieceType;
        }

        /// <summary>
        /// Create a copy of this key with a different piece type.
        /// </summary>
        public readonly VisualKey WithPieceType(EPieceType newPieceType)
        {
            return new VisualKey(Category, VariantId, newPieceType);
        }

        /// <summary>
        /// Create a copy of this key ignoring piece type (set to Any).
        /// Useful for fallback lookups.
        /// </summary>
        public readonly VisualKey WithoutPieceType()
        {
            return new VisualKey(Category, VariantId, EPieceType.Any);
        }

        #endregion
    }
}