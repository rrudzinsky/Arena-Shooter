using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    internal static class ArenaCornerPostAsset
    {
        private const string AssetPath = "Assets/Models/CyberArenaCornerPost.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberArenaCornerPost.fbx";
        private const string ResourcePath = "Models/CyberArenaCornerPost";

        public static bool TryBuild(Transform parent, Vector3 position, Vector3 scale, out GameObject wrapper)
        {
            wrapper = null;
            var prefab = LoadModelPrefab();
            if (prefab == null)
            {
                return false;
            }

            wrapper = new GameObject("Cyber Arena Corner Post Model");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = position;
            wrapper.transform.rotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;

            var instance = Object.Instantiate(prefab, wrapper.transform, false);
            instance.name = "Imported Cyber Arena Corner Post Mesh";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = scale;
            ImportedModelUtility.RemoveColliders(instance);

            var box = wrapper.AddComponent<BoxCollider>();
            box.size = scale;
            DroidRenderSetup.Apply(wrapper, StylizedOutlineCategory.Wall);
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

    }
}
