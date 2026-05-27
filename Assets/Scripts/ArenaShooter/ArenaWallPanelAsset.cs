using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    internal static class ArenaWallPanelAsset
    {
        private const string AssetPath = "Assets/Models/CyberArenaWallPanel.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberArenaWallPanel.fbx";
        private const string ResourcePath = "Models/CyberArenaWallPanel";

        public static bool TryBuild(Transform parent, ArenaTheme theme, Vector3 position, Quaternion rotation)
        {
            var prefab = LoadModelPrefab();
            if (prefab == null)
            {
                return false;
            }

            var wrapper = new GameObject("Cyber Arena Wall Panel Model");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = position;
            wrapper.transform.rotation = rotation;
            wrapper.transform.localScale = Vector3.one;

            var instance = Object.Instantiate(prefab, wrapper.transform, false);
            instance.name = "Imported Cyber Arena Wall Panel Mesh";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ImportedModelUtility.RemoveColliders(instance);

            ApplyThemeMaterials(wrapper, theme);
            DroidRenderSetup.Apply(wrapper, StylizedOutlineCategory.Wall);
            ImportedModelUtility.LogModelInstanceDiagnostics("Arena wall panel imported model", wrapper);
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
