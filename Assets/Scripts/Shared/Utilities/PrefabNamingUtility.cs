using Assets.Scripts.Shared.Enums;
using UnityEngine;

namespace Assets.Scripts.Shared.Utilities
{
    /// <summary>
    /// Utility for parsing prefab names to extract variant and piece type information.
    /// 
    /// Expected naming conventions:
    /// - "{Variant}{PieceType}Piece" → e.g., "OakTrunkPiece", "AcaciaTopPiece"
    /// - "{Variant}Piece" → e.g., "StonePiece" (defaults to Any type)
    /// - "{Variant}{PieceType}" → e.g., "OakTrunk", "AcaciaTop"
    /// - "{Variant}" → e.g., "Stone" (defaults to Any type)
    /// </summary>
    public static class PrefabNamingUtility
    {
        // Piece type suffixes in order of specificity (longer matches first)
        private static readonly (string suffix, EPieceType type)[] PieceTypeSuffixes =
        {
            ("TrunkPiece", EPieceType.Trunk),
            ("TopPiece", EPieceType.Top),
            ("BasePiece", EPieceType.Base),
            ("MiddlePiece", EPieceType.Middle),
            ("Trunk", EPieceType.Trunk),
            ("Top", EPieceType.Top),
            ("Base", EPieceType.Base),
            ("Middle", EPieceType.Middle),
            ("Piece", EPieceType.Any),  // Generic piece suffix
        };

        /// <summary>
        /// Parse a prefab name to extract variant ID and piece type.
        /// </summary>
        /// <param name="prefabName">The name of the prefab (e.g., "OakTrunkPiece")</param>
        /// <param name="variantId">Output: The extracted variant ID in lowercase (e.g., "oak")</param>
        /// <param name="pieceType">Output: The extracted piece type (e.g., Trunk)</param>
        public static void Parse(string prefabName, out string variantId, out EPieceType pieceType)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                variantId = "default";
                pieceType = EPieceType.Any;
                return;
            }

            // Remove common prefixes if present
            string name = prefabName;
            if (name.StartsWith("Prefab_")) name = name.Substring(7);
            if (name.StartsWith("P_")) name = name.Substring(2);

            // Try to match piece type suffixes
            foreach (var (suffix, type) in PieceTypeSuffixes)
            {
                if (name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                {
                    // Extract variant (everything before the suffix)
                    string variant = name.Substring(0, name.Length - suffix.Length);

                    if (!string.IsNullOrEmpty(variant))
                    {
                        variantId = variant.ToLowerInvariant();
                        pieceType = type;
                        return;
                    }
                }
            }

            // No recognized suffix - use whole name as variant
            variantId = name.ToLowerInvariant();
            pieceType = EPieceType.Any;
        }

        /// <summary>
        /// Parse a prefab's name directly from a GameObject.
        /// </summary>
        public static void Parse(GameObject prefab, out string variantId, out EPieceType pieceType)
        {
            Parse(prefab != null ? prefab.name : null, out variantId, out pieceType);
        }

        /// <summary>
        /// Get just the variant ID from a prefab name.
        /// </summary>
        public static string GetVariantId(string prefabName)
        {
            Parse(prefabName, out string variantId, out _);
            return variantId;
        }

        /// <summary>
        /// Get just the piece type from a prefab name.
        /// </summary>
        public static EPieceType GetPieceType(string prefabName)
        {
            Parse(prefabName, out _, out EPieceType pieceType);
            return pieceType;
        }

        /// <summary>
        /// Check if a piece type can be placed at a given index.
        /// </summary>
        /// <param name="pieceType">The type of piece</param>
        /// <param name="index">The index in the structure (0-based)</param>
        /// <param name="totalPieces">Total number of pieces in the structure</param>
        /// <returns>True if the piece can be placed at this index</returns>
        public static bool CanPlaceAt(EPieceType pieceType, int index, int totalPieces)
        {
            bool isFirst = index == 0;
            bool isLast = index == totalPieces - 1;
            bool isMiddle = !isFirst && !isLast;

            return pieceType switch
            {
                EPieceType.Any => true,
                EPieceType.Base => isFirst,
                EPieceType.Top => isLast,
                EPieceType.Middle => isMiddle,
                EPieceType.Trunk => !isLast,  // Can be anywhere except last
                _ => true
            };
        }

        /// <summary>
        /// Build a full variant key combining variant ID and piece type.
        /// Used for pooling when different piece types need different visuals.
        /// </summary>
        public static string BuildFullVariantKey(string variantId, EPieceType pieceType)
        {
            if (pieceType == EPieceType.Any) return variantId;

            return $"{variantId}_{pieceType.ToString().ToLowerInvariant()}";
        }
    }
}