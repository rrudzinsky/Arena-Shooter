using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    internal static class BattleDroidAsset
    {
        private const string AssetPath = "Assets/Models/CyberBattleDroid.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberBattleDroid.fbx";
        private const string ResourcePath = "Models/CyberBattleDroid";
        private const float TargetDroidMaxDimension = 1.85f;
        private const float MinimumVisibleBoundsDimension = 0.05f;
        private const string DiagnosticLabel = "Battle droid imported model";

        public static bool TryBuild(Transform parent, ArenaTheme theme)
        {
            var prefab = LoadModelPrefab();
            if (prefab == null)
            {
                return false;
            }

            var wrapper = new GameObject("Droid Combat Frame Model");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = new Vector3(0f, -0.93f, 0f);
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;

            var instance = Object.Instantiate(prefab, wrapper.transform, false);
            instance.name = "Imported Cyber Battle Droid Mesh";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            ForceVisibleHierarchy(wrapper);
            ImportedModelUtility.RemoveImportedCamerasAndLights(wrapper);
            ImportedModelUtility.RemoveColliders(wrapper);
            ImportedModelUtility.TryNormalizeRendererBounds(wrapper, instance.transform, TargetDroidMaxDimension, DiagnosticLabel);
            ApplyThemeMaterials(wrapper, theme);
            DroidRenderSetup.Apply(wrapper);
            ApplyStatusLightRenderingOverrides(wrapper);
            ImportedModelUtility.LogModelInstanceDiagnostics(DiagnosticLabel, wrapper);

            if (!HasUsableVisibleRenderer(wrapper))
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning("[Arena Shooter Model Diagnostics] Imported battle droid rejected; using procedural fallback body.");
                }

                ImportedModelUtility.DestroyObject(wrapper);
                return false;
            }

            return true;
        }

        private static GameObject LoadModelPrefab()
        {
            var resourceAsset = Resources.Load<GameObject>(ResourcePath);
            if (resourceAsset != null)
            {
                return resourceAsset;
            }

#if UNITY_EDITOR
            var resourceEditorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ResourceAssetPath);
            if (resourceEditorAsset != null)
            {
                return resourceEditorAsset;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
#else
            return null;
#endif
        }

        private static void ApplyThemeMaterials(GameObject instance, ArenaTheme theme)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var material = IsRedStatusLight(renderer) ? theme.DroidStatusLight : ResolveThemeMaterial(theme);
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static bool IsRedStatusLight(Renderer renderer)
        {
            return renderer != null && renderer.name.ToLowerInvariant().Contains("red status");
        }

        // The status light renders emissive red on the Default layer (weapon glow lens
        // precedent) so the droid outline band cannot wrap it in a gold ring.
        private static void ApplyStatusLightRenderingOverrides(GameObject instance)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (IsRedStatusLight(renderer))
                {
                    renderer.renderingLayerMask = DroidRenderSetup.DefaultRenderingLayer;
                }
            }
        }

        private static Material ResolveThemeMaterial(ArenaTheme theme)
        {
            return theme.DroidArmor;
        }

        private static void ForceVisibleHierarchy(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.SetActive(true);
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
            }
        }

        private static bool HasUsableVisibleRenderer(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var size = renderer.bounds.size;
                if (Mathf.Max(size.x, size.y, size.z) >= MinimumVisibleBoundsDimension)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
