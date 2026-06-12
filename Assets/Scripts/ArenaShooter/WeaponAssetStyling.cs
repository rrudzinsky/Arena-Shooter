using System;
using UnityEngine;

namespace ArenaShooter
{
    /// <summary>
    /// Shared name-token material mapping for imported weapon meshes. Parts named with
    /// cyan/glow/energy tokens light up with NeonA, magenta tokens with NeonB, the rest
    /// stay matte black so the stylized outline pass draws their edges.
    /// </summary>
    internal static class WeaponAssetStyling
    {
        private static readonly string[] CyanTokens = { "cyan", "glow lens", "energy", "charge", "glow band", "glow dot", "bead", "arming button" };
        private static readonly string[] MagentaTokens = { "magenta" };
        private static readonly string[] GoldTokens = { "gold" };

        public static Material ResolveThemeMaterial(Renderer renderer, Transform root, ArenaTheme theme)
        {
            foreach (var token in MagentaTokens)
            {
                if (RendererMetadataContains(renderer, root, token))
                {
                    return theme.NeonB;
                }
            }

            foreach (var token in GoldTokens)
            {
                if (RendererMetadataContains(renderer, root, token))
                {
                    return theme.Scrap;
                }
            }

            foreach (var token in CyanTokens)
            {
                if (RendererMetadataContains(renderer, root, token))
                {
                    return theme.NeonA;
                }
            }

            return theme.Pickup;
        }

        public static bool RendererMetadataContains(Renderer renderer, Transform root, string token)
        {
            if (renderer == null)
            {
                return false;
            }

            if (TransformHierarchyContains(renderer.transform, root, token) ||
                RendererMeshNameContains(renderer, token))
            {
                return true;
            }

            foreach (var material in renderer.sharedMaterials)
            {
                if (NameContains(material != null ? material.name : null, token))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TransformHierarchyContains(Transform transform, Transform root, string token)
        {
            var current = transform;
            while (current != null)
            {
                if (NameContains(current.name, token))
                {
                    return true;
                }

                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool RendererMeshNameContains(Renderer renderer, string token)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer &&
                NameContains(skinnedRenderer.sharedMesh != null ? skinnedRenderer.sharedMesh.name : null, token))
            {
                return true;
            }

            var meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null &&
                NameContains(meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : null, token);
        }

        private static bool NameContains(string name, string token)
        {
            return !string.IsNullOrEmpty(name) &&
                name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
