using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    internal static class FirstPersonHandsAsset
    {
        private const string AssetPath = "Assets/Models/CyberFirstPersonHands.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberFirstPersonHands.fbx";
        private const string ResourcePath = "Models/CyberFirstPersonHands";

        public static bool TryBuild(
            Transform parent,
            ArenaTheme theme,
            out Transform leftForearm,
            out Transform rightForearm,
            out Transform leftGlove,
            out Transform rightGlove)
        {
            leftForearm = null;
            rightForearm = null;
            leftGlove = null;
            rightGlove = null;

            var wrapper = new GameObject("Cyber First Person Hands Model");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = Vector3.zero;
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;

            var instance = InstantiateModel("Imported Cyber First Person Hands Mesh", wrapper.transform);
            if (instance == null)
            {
                ImportedModelUtility.DestroyObject(wrapper);
                return false;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ApplyThemeMaterials(wrapper, theme);
            DroidRenderSetup.Apply(wrapper, StylizedOutlineCategory.None);

            leftForearm = FindDeepChild(wrapper.transform, "FPH_Left Connected Forearm Main Armor");
            rightForearm = FindDeepChild(wrapper.transform, "FPH_Right Connected Forearm Main Armor");
            leftGlove = FindDeepChild(wrapper.transform, "FPH_Left Connected Palm Wedge");
            rightGlove = FindDeepChild(wrapper.transform, "FPH_Right Connected Palm Wedge");

            if (leftForearm == null || rightForearm == null || leftGlove == null || rightGlove == null)
            {
                ImportedModelUtility.DestroyObject(wrapper);
                leftForearm = null;
                rightForearm = null;
                leftGlove = null;
                rightGlove = null;
                return false;
            }

            wrapper.SetActive(false);
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
            ImportedModelUtility.RemoveImportedCamerasAndLights(instance);
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

            if (key.Contains("cyan"))
            {
                return theme.NeonA;
            }

            if (key.Contains("magenta"))
            {
                return theme.NeonB;
            }

            if (key.Contains("gray") || key.Contains("metal") || key.Contains("joint") || key.Contains("rubber"))
            {
                return theme.Wall;
            }

            return theme.Player;
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
