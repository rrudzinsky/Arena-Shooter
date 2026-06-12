using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;

namespace ArenaShooter
{
    internal static class ShotgunAsset
    {
        private const string AssetPath = "Assets/Models/CyberScatterShotgun.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberScatterShotgun.fbx";
        private const string ResourcePath = "Models/CyberScatterShotgun";
        private const string GlowLensNameToken = "glow lens";
        private const string SightNameToken = "sight";

        public static bool TryBuildViewModel(Transform parent, ArenaTheme theme, out Transform muzzle)
        {
            muzzle = null;
            var wrapper = CreatePositionedInstance("Cyber Scatter Shotgun View Model", parent, theme, true);
            if (wrapper == null)
            {
                return false;
            }

            wrapper.transform.localPosition = new Vector3(0.02f, -0.01f, 0.02f);
            // source model faces -Z; first person weapons face +Z
            wrapper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            wrapper.transform.localScale = Vector3.one;

            muzzle = FindDeepChild(wrapper.transform, "CSG_muzzle wide glow lens") ?? wrapper.transform;
            return true;
        }

        public static bool TryBuildPickupModel(Transform parent, ArenaTheme theme)
        {
            var wrapper = CreatePositionedInstance("Cyber Scatter Shotgun Pickup Model", parent, theme, false);
            if (wrapper == null)
            {
                return false;
            }

            wrapper.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            wrapper.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            wrapper.transform.localScale = Vector3.one;
            return true;
        }

        private static GameObject CreatePositionedInstance(string name, Transform parent, ArenaTheme theme, bool viewModel)
        {
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);

            var instance = InstantiateModel("Imported Cyber Scatter Shotgun Mesh", wrapper.transform);
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
                viewModel ? 0.66f : 1.02f,
                viewModel ? "Scatter shotgun view model" : "Scatter shotgun pickup model");
            DroidRenderSetup.Apply(wrapper, viewModel ? StylizedOutlineCategory.FirstPersonPistol : StylizedOutlineCategory.Gun);
            ApplyOutlineOverrides(wrapper, viewModel);
            ApplyThemeMaterials(wrapper, theme);
            if (viewModel)
            {
                ApplyFirstPersonWeaponOccluder(wrapper);
            }

            ImportedModelUtility.LogModelInstanceDiagnostics(viewModel ? "Scatter shotgun view model" : "Scatter shotgun pickup model", wrapper);
            return wrapper;
        }

        private static GameObject InstantiateModel(string name, Transform parent)
        {
            var prefab = LoadModelPrefab();
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

            var editorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
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
                var material = WeaponAssetStyling.ResolveThemeMaterial(renderer, instance.transform, theme);
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static void ApplyOutlineOverrides(GameObject instance, bool viewModel)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (WeaponAssetStyling.RendererMetadataContains(renderer, instance.transform, GlowLensNameToken))
                {
                    DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);
                    continue;
                }

                if (viewModel && WeaponAssetStyling.RendererMetadataContains(renderer, instance.transform, SightNameToken))
                {
                    DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.FirstPersonPistolSight);
                }
            }
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
