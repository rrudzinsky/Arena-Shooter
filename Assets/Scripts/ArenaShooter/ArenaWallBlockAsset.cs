using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    internal static class ArenaWallBlockAsset
    {
        private const string AssetPath = "Assets/Models/CyberArenaWallBlock.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberArenaWallBlock.fbx";
        private const string ResourcePath = "Models/CyberArenaWallBlock";

        public static bool TryBuild(Transform parent, ArenaTheme theme, Vector3 position, Vector3 scale, Material baseMaterial, out GameObject wrapper)
        {
            wrapper = null;
            var prefab = LoadModelPrefab();
            if (prefab == null)
            {
                return false;
            }

            wrapper = new GameObject("Cyber Arena Wall Block Model");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = position;
            wrapper.transform.rotation = Quaternion.identity;
            wrapper.transform.localScale = scale;

            var instance = Object.Instantiate(prefab, wrapper.transform, false);
            instance.name = "Imported Cyber Arena Wall Block Mesh";
            instance.transform.localPosition = Vector3.zero;
            ApplyVisualTransform(instance.transform);
            ImportedModelUtility.RemoveColliders(instance);

            var box = wrapper.AddComponent<BoxCollider>();
            box.size = Vector3.one;
            ApplyThemeMaterials(wrapper, theme, baseMaterial);
            DroidRenderSetup.Apply(wrapper, StylizedOutlineCategory.Wall);
            ImportedModelUtility.LogModelInstanceDiagnostics("Arena wall block imported model", wrapper);
            return true;
        }

        private static void ApplyVisualTransform(Transform visual)
        {
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
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

        private static void ApplyThemeMaterials(GameObject instance, ArenaTheme theme, Material baseMaterial)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = ResolveThemeMaterial(renderer.name, materials[i], theme, baseMaterial);
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material ResolveThemeMaterial(string rendererName, Material source, ArenaTheme theme, Material baseMaterial)
        {
            var materialName = source != null ? source.name : string.Empty;
            var key = $"{rendererName} {materialName}".ToLowerInvariant();

            return baseMaterial != null ? baseMaterial : theme.Wall;
        }
    }
}
