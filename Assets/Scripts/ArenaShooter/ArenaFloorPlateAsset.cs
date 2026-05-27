using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    internal static class ArenaFloorPlateAsset
    {
        private const string AssetPath = "Assets/Models/CyberArenaFloorPlate.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberArenaFloorPlate.fbx";
        private const string ResourcePath = "Models/CyberArenaFloorPlate";

        public static bool TryBuild(Transform parent, ArenaTheme theme, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var prefab = LoadModelPrefab();
            if (prefab == null)
            {
                return false;
            }

            var wrapper = new GameObject("Cyber Arena Floor Plate Model");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = position;
            wrapper.transform.rotation = rotation;
            wrapper.transform.localScale = scale;

            var instance = Object.Instantiate(prefab, wrapper.transform, false);
            instance.name = "Imported Cyber Arena Floor Plate Mesh";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ImportedModelUtility.RemoveColliders(instance);

            ApplyThemeMaterials(wrapper, theme);
            DroidRenderSetup.Apply(wrapper, StylizedOutlineCategory.None);
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

            return theme.Floor;
        }
    }
}
