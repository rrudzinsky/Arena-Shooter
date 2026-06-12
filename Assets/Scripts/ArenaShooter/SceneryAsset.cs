using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    /// <summary>
    /// Loads hill scenery prop variants (crystal spires, neon palms, foliage, barricades)
    /// from the multi-variant FBX files in Assets/Models. Each file holds several
    /// "*_variant <name>" roots; one is kept per instance and the rest are discarded.
    /// </summary>
    internal static class SceneryAsset
    {
        internal enum Kind
        {
            CrystalSpires,
            NeonPalm,
            NeonFoliage,
            HexBarricade,
            ShieldBarrier
        }

        private static Material shieldPaneMaterial;

        public static GameObject TryBuildVariant(Transform parent, ArenaTheme theme, Kind kind, string variantToken)
        {
            var prefab = LoadModelPrefab(kind);
            if (prefab == null)
            {
                return null;
            }

            var wrapper = new GameObject($"{kind} {variantToken}");
            wrapper.transform.SetParent(parent, false);

            var instance = Object.Instantiate(prefab, wrapper.transform, false);
            instance.name = "Imported Scenery";
            ImportedModelUtility.RemoveImportedCamerasAndLights(instance);
            ImportedModelUtility.RemoveColliders(instance);

            var variant = FindVariantRoot(instance.transform, variantToken);
            if (variant == null)
            {
                ImportedModelUtility.DestroyObject(wrapper);
                return null;
            }

            variant.SetParent(wrapper.transform, false);
            variant.localPosition = Vector3.zero;
            ImportedModelUtility.DestroyObject(instance);

            DroidRenderSetup.Apply(wrapper, StylizedOutlineCategory.Wall);
            ApplyThemeMaterials(wrapper, theme);
            return wrapper;
        }

        private static Transform FindVariantRoot(Transform root, string variantToken)
        {
            var queue = new System.Collections.Generic.Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.name.IndexOf("variant", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    current.name.IndexOf(variantToken, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return current;
                }

                foreach (Transform child in current)
                {
                    queue.Enqueue(child);
                }
            }

            return null;
        }

        private static GameObject LoadModelPrefab(Kind kind)
        {
            var fileName = kind switch
            {
                Kind.CrystalSpires => "CyberCrystalSpires",
                Kind.NeonPalm => "CyberNeonPalm",
                Kind.NeonFoliage => "CyberNeonFoliage",
                Kind.HexBarricade => "CyberHexBarricade",
                _ => "CyberShieldBarrier"
            };

            var resourceAsset = Resources.Load<GameObject>($"Models/{fileName}");
            if (resourceAsset != null)
            {
                return resourceAsset;
            }

#if UNITY_EDITOR
            var resourceEditorAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Models/{fileName}.fbx");
            if (resourceEditorAsset != null)
            {
                return resourceEditorAsset;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/{fileName}.fbx");
#else
            return null;
#endif
        }

        private static void ApplyThemeMaterials(GameObject instance, ArenaTheme theme)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                Material material;
                if (WeaponAssetStyling.RendererMetadataContains(renderer, instance.transform, "shield pane"))
                {
                    material = ResolveShieldPaneMaterial(theme);
                    DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    material = WeaponAssetStyling.ResolveThemeMaterial(renderer, instance.transform, theme);
                }

                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material ResolveShieldPaneMaterial(ArenaTheme theme)
        {
            if (shieldPaneMaterial != null)
            {
                return shieldPaneMaterial;
            }

            shieldPaneMaterial = new Material(theme.NeonA)
            {
                name = "Hard Light Shield Pane"
            };

            if (shieldPaneMaterial.HasProperty("_BaseColor"))
            {
                var color = shieldPaneMaterial.GetColor("_BaseColor");
                color.a = 0.22f;
                shieldPaneMaterial.SetColor("_BaseColor", color);
            }
            else if (shieldPaneMaterial.HasProperty("_Color"))
            {
                var color = shieldPaneMaterial.GetColor("_Color");
                color.a = 0.22f;
                shieldPaneMaterial.SetColor("_Color", color);
            }

            shieldPaneMaterial.SetFloat("_Surface", 1f);
            shieldPaneMaterial.SetFloat("_Blend", 1f);
            shieldPaneMaterial.SetFloat("_AlphaClip", 0f);
            shieldPaneMaterial.SetFloat("_ZWrite", 0f);
            shieldPaneMaterial.renderQueue = 3000;
            shieldPaneMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return shieldPaneMaterial;
        }
    }
}
