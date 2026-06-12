using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    internal static class GrenadeAsset
    {
        private const string AssetPath = "Assets/Models/CyberPlasmaGrenade.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberPlasmaGrenade.fbx";
        private const string ResourcePath = "Models/CyberPlasmaGrenade";

        public static bool TryBuildViewModel(Transform parent, ArenaTheme theme, out Transform muzzle)
        {
            muzzle = null;
            var wrapper = CreatePositionedInstance("Cyber Plasma Grenade View Model", parent, theme, 0.30f, "Plasma grenade view model", true);
            if (wrapper == null)
            {
                return false;
            }

            wrapper.transform.localPosition = new Vector3(0.02f, -0.05f, 0.24f);
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;
            muzzle = wrapper.transform;
            return true;
        }

        public static bool TryBuildPickupModel(Transform parent, ArenaTheme theme)
        {
            var wrapper = CreatePositionedInstance("Cyber Plasma Grenade Pickup Model", parent, theme, 0.46f, "Plasma grenade pickup model", false);
            if (wrapper == null)
            {
                return false;
            }

            wrapper.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;
            return true;
        }

        public static bool TryBuildProjectileModel(Transform parent, ArenaTheme theme)
        {
            var wrapper = CreatePositionedInstance("Cyber Plasma Grenade Projectile Model", parent, theme, 0.27f, "Plasma grenade projectile model", false);
            if (wrapper == null)
            {
                return false;
            }

            wrapper.transform.localPosition = Vector3.zero;
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;
            return true;
        }

        private static GameObject CreatePositionedInstance(string name, Transform parent, ArenaTheme theme, float targetSize, string label, bool viewModel)
        {
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);

            var instance = InstantiateModel("Imported Cyber Plasma Grenade Mesh", wrapper.transform);
            if (instance == null)
            {
                ImportedModelUtility.DestroyObject(wrapper);
                return null;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ImportedModelUtility.TryNormalizeRendererBounds(wrapper, instance.transform, targetSize, label);
            DroidRenderSetup.Apply(wrapper, viewModel ? StylizedOutlineCategory.FirstPersonPistol : StylizedOutlineCategory.Gun);
            ApplyThemeMaterials(wrapper, theme);
            if (viewModel)
            {
                ApplyFirstPersonWeaponOccluder(wrapper);
            }

            ImportedModelUtility.LogModelInstanceDiagnostics(label, wrapper);
            return wrapper;
        }

        private static GameObject InstantiateModel(string name, Transform parent)
        {
            var prefab = LoadModelPrefab();
            if (prefab == null)
            {
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
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

        private static void ApplyFirstPersonWeaponOccluder(GameObject instance)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                DroidRenderSetup.AddFirstPersonWeaponOccluder(renderer);
            }
        }
    }
}
