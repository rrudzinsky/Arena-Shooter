using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;

namespace ArenaShooter
{
    internal static class PulsePistolAsset
    {
        private const string AssetPath = "Assets/Models/CyberPulsePistol.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberPulsePistol.fbx";
        private const string ResourcePath = "Models/CyberPulsePistol";
        private const string ViewAssetPath = "Assets/Models/CyberPulsePistolView.fbx";
        private const string ViewResourceAssetPath = "Assets/Resources/Models/CyberPulsePistolView.fbx";
        private const string ViewResourcePath = "Models/CyberPulsePistolView";
        private const string GlowLensNameToken = "glow lens";
        private const string SightNameToken = "sight";
        private const string FrontSightBlockNameToken = "front squared compensator";

        public static bool TryBuildViewModel(Transform parent, ArenaTheme theme, out Transform muzzle)
        {
            muzzle = null;
            var wrapper = CreatePositionedInstance("Cyber Pulse Pistol View Model", parent, theme, true);
            if (wrapper == null)
            {
                return false;
            }

            wrapper.transform.localPosition = new Vector3(0.02f, -0.02f, 0.05f);
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;

            muzzle = FindDeepChild(wrapper.transform, "CPP_muzzle blue glow lens") ?? wrapper.transform;
            return true;
        }

        public static bool TryBuildPickupModel(Transform parent, ArenaTheme theme)
        {
            var wrapper = CreatePositionedInstance("Cyber Pulse Pistol Pickup Model", parent, theme, false);
            if (wrapper == null)
            {
                return false;
            }

            wrapper.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;
            return true;
        }

        private static GameObject CreatePositionedInstance(string name, Transform parent, ArenaTheme theme, bool viewModel)
        {
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);

            var instance = InstantiateModel("Imported Cyber Pulse Pistol Mesh", wrapper.transform, viewModel);
            if (instance == null)
            {
                ImportedModelUtility.DestroyObject(wrapper);
                return null;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ImportedModelUtility.TryNormalizeRendererBounds(
                wrapper,
                instance.transform,
                viewModel ? 0.58f : 0.9f,
                viewModel ? "Pulse pistol view model" : "Pulse pistol pickup model");
            DroidRenderSetup.Apply(wrapper, viewModel ? StylizedOutlineCategory.FirstPersonPistol : StylizedOutlineCategory.Gun);
            ApplyPistolOutlineOverrides(wrapper, viewModel);
            ApplyThemeMaterials(wrapper, theme);
            if (viewModel)
            {
                ApplyFirstPersonWeaponOccluder(wrapper);
            }

            ImportedModelUtility.LogModelInstanceDiagnostics(viewModel ? "Pulse pistol view model" : "Pulse pistol pickup model", wrapper);
            return wrapper;
        }

        private static GameObject InstantiateModel(string name, Transform parent, bool viewModel)
        {
            var prefab = LoadModelPrefab(viewModel);
            if (prefab == null)
            {
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = name;
            ImportedModelUtility.RemoveImportedCamerasAndLights(instance);
            ImportedModelUtility.RemoveColliders(instance);
            return instance;
        }

        private static GameObject LoadModelPrefab(bool viewModel)
        {
            var resourceAsset = Resources.Load<GameObject>(viewModel ? ViewResourcePath : ResourcePath);
            if (resourceAsset != null)
            {
                return resourceAsset;
            }

#if UNITY_EDITOR
            var resourceEditorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(viewModel ? ViewResourceAssetPath : ResourceAssetPath);
            if (resourceEditorAsset != null)
            {
                return resourceEditorAsset;
            }

            var editorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(viewModel ? ViewAssetPath : AssetPath);
            if (editorAsset != null)
            {
                return editorAsset;
            }
#endif
            return null;
        }

        private static void ApplyThemeMaterials(GameObject instance, ArenaTheme theme)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var material = ResolveThemeMaterial(theme);
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material ResolveThemeMaterial(ArenaTheme theme)
        {
            return theme.Pickup;
        }

        private static void ApplyPistolOutlineOverrides(GameObject instance, bool viewModel)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var category = ResolvePistolOutlineOverride(renderer, instance.transform, viewModel);
                if (category.HasValue)
                {
                    DroidRenderSetup.ApplyRenderer(renderer, category.Value);
                }
            }
        }

        private static StylizedOutlineCategory? ResolvePistolOutlineOverride(Renderer renderer, Transform root, bool viewModel)
        {
            if (IsGlowLensRenderer(renderer, root))
            {
                return StylizedOutlineCategory.None;
            }

            if (viewModel && IsIronSightRenderer(renderer, root))
            {
                return StylizedOutlineCategory.FirstPersonPistolSight;
            }

            return null;
        }

        private static bool IsGlowLensRenderer(Renderer renderer, Transform root)
        {
            return RendererMetadataContains(renderer, root, GlowLensNameToken);
        }

        private static bool IsIronSightRenderer(Renderer renderer, Transform root)
        {
            return RendererMetadataContains(renderer, root, SightNameToken) ||
                RendererMetadataContains(renderer, root, FrontSightBlockNameToken);
        }

        private static bool RendererMetadataContains(Renderer renderer, Transform root, string token)
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

        private static void ApplyFirstPersonWeaponOccluder(GameObject instance)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                DroidRenderSetup.AddFirstPersonWeaponOccluder(renderer);
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
