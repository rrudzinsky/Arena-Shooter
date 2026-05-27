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
            foreach (var collider in wrapper.GetComponentsInChildren<Collider>(true))
            {
                ImportedModelUtility.DestroyObject(collider);
            }

            ApplyThemeMaterials(wrapper, theme);
            DroidRenderSetup.Apply(wrapper);
            ImportedModelUtility.LogModelInstanceDiagnostics("Battle droid imported model", wrapper);
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
                    materials[i] = ResolveThemeMaterial(theme);
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material ResolveThemeMaterial(ArenaTheme theme)
        {
            return theme.DroidArmor;
        }
    }
}
