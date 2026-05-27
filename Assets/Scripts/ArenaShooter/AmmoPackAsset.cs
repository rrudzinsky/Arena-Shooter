using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    internal static class AmmoPackAsset
    {
        private const string AssetPath = "Assets/Models/CyberAmmoPack.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberAmmoPack.fbx";
        private const string ResourcePath = "Models/CyberAmmoPack";

        public static bool TryBuildPickupModel(Transform parent, ArenaTheme theme)
        {
            var wrapper = new GameObject("Cyber Ammo Pack Pickup Model");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;

            var instance = InstantiateModel("Imported Cyber Ammo Pack Mesh", wrapper.transform);
            if (instance == null)
            {
                Object.Destroy(wrapper);
                return false;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ApplyThemeMaterials(wrapper, theme);
            DroidRenderSetup.Apply(wrapper, StylizedOutlineCategory.Ammo);
            ImportedModelUtility.LogModelInstanceDiagnostics("Ammo pickup imported model", wrapper);
            return true;
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

            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
#else
            return null;
#endif
        }

        private static void ApplyThemeMaterials(GameObject instance, ArenaTheme theme)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = ResolveThemeMaterial(renderer.name, materials[i], theme);
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material ResolveThemeMaterial(string rendererName, Material source, ArenaTheme theme)
        {
            var materialName = source != null ? source.name : string.Empty;
            var key = $"{rendererName} {materialName}".ToLowerInvariant();

            return theme.Wall;
        }
    }
}
