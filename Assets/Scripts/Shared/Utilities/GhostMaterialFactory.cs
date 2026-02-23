// ============================================================================
// GhostMaterialFactory.cs - Shared utility for creating transparent preview materials
// ============================================================================
using UnityEngine;

namespace Assets.Scripts.Shared.Utilities
{
    /// <summary>
    /// Factory for creating transparent ghost/preview materials.
    /// Centralizes material creation to avoid code duplication across
    /// BuildingBlueprint and BuildingPreviewRenderer.
    /// </summary>
    public static class GhostMaterialFactory
    {
        private static Shader cachedStandardShader;

        /// <summary>
        /// Create a transparent material with the specified color.
        /// </summary>
        /// <param name="color">The color (with alpha) for the material</param>
        /// <param name="baseMaterial">Optional base material to clone from</param>
        /// <returns>A new transparent material instance</returns>
        public static Material CreateTransparentMaterial(Color color, Material baseMaterial = null)
        {
            Material mat;

            if (baseMaterial != null)
            {
                mat = new Material(baseMaterial);
            }
            else
            {
                mat = new Material(GetStandardShader());
                SetupTransparentMode(mat);
            }

            mat.color = color;
            return mat;
        }

        /// <summary>
        /// Create a ghost material for building blueprints.
        /// </summary>
        /// <param name="ghostColor">The ghost color (typically semi-transparent blue)</param>
        /// <param name="baseMaterial">Optional base material to use</param>
        public static Material CreateGhostMaterial(Color ghostColor, Material baseMaterial = null)
        {
            return CreateTransparentMaterial(ghostColor, baseMaterial);
        }

        /// <summary>
        /// Create a valid placement preview material (green).
        /// </summary>
        public static Material CreateValidPreviewMaterial(Color? color = null, Material baseMaterial = null)
        {
            return CreateTransparentMaterial(color ?? new Color(0f, 1f, 0f, 0.5f), baseMaterial);
        }

        /// <summary>
        /// Create an invalid placement preview material (red).
        /// </summary>
        public static Material CreateInvalidPreviewMaterial(Color? color = null, Material baseMaterial = null)
        {
            return CreateTransparentMaterial(color ?? new Color(1f, 0f, 0f, 0.5f), baseMaterial);
        }

        /// <summary>
        /// Apply the shared ghost material to all renderers on a GameObject.
        /// </summary>
        /// <param name="target">The GameObject to apply materials to</param>
        /// <param name="material">The material to apply</param>
        public static void ApplyToAllRenderers(GameObject target, Material material)
        {
            if (target == null || material == null) return;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[renderer.materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }
                renderer.materials = materials;
            }
        }

        /// <summary>
        /// Configure a material for transparent rendering.
        /// </summary>
        private static void SetupTransparentMode(Material mat)
        {
            mat.SetFloat("_Mode", 3); // Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        /// <summary>
        /// Get the standard shader, caching for performance.
        /// </summary>
        private static Shader GetStandardShader()
        {
            if (cachedStandardShader == null)
            {
                cachedStandardShader = Shader.Find("Standard");
            }
            return cachedStandardShader;
        }
    }
}
