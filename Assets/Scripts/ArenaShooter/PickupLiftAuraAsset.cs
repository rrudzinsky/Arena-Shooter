using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    internal static class PickupLiftAuraAsset
    {
        private const string AssetPath = "Assets/Models/CyberPickupLiftAura.fbx";
        private const string ResourceAssetPath = "Assets/Resources/Models/CyberPickupLiftAura.fbx";
        private const string ResourcePath = "Models/CyberPickupLiftAura";

        public static bool TryBuild(Transform parent, Material sourceMaterial, Color auraColor, float lightRange)
        {
            var prefab = LoadModelPrefab();
            if (prefab == null)
            {
                return false;
            }

            BuildPersistentEmitterPad(parent, sourceMaterial, auraColor);

            var wrapper = new GameObject("Halo Lift Pickup Aura");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = new Vector3(0f, -0.52f, 0f);
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;

            var instance = Object.Instantiate(prefab, wrapper.transform, false);
            instance.name = "Imported Halo Lift Aura Mesh";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ImportedModelUtility.RemoveColliders(instance);

            var animator = wrapper.AddComponent<PickupLiftAuraAnimator>();
            animator.Configure(sourceMaterial, auraColor, lightRange);
            ImportedModelUtility.LogModelInstanceDiagnostics("Pickup lift aura imported authoring mesh", wrapper);
            return true;
        }

        private static void BuildPersistentEmitterPad(Transform pickupParent, Material sourceMaterial, Color auraColor)
        {
            var pad = new GameObject("Spent Pickup Emitter Pad");
            var padParent = pickupParent.parent;
            pad.transform.SetParent(padParent, false);
            pad.transform.position = pickupParent.TransformPoint(new Vector3(0f, -0.54f, 0f));
            pad.transform.rotation = pickupParent.rotation;

            var metal = CreatePadMaterial(sourceMaterial);
            var glow = CreatePadGlowMaterial(sourceMaterial, Color.Lerp(new Color(0.55f, 1f, 0.94f), auraColor, 0.12f));

            CreatePadPrimitive("Emitter Pad Low Metal Base", PrimitiveType.Cylinder, metal, pad.transform, Vector3.zero, new Vector3(0.46f, 0.018f, 0.46f));
            CreatePadPrimitive("Emitter Pad Recessed Dark Core", PrimitiveType.Cylinder, metal, pad.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.28f, 0.012f, 0.28f));
            CreatePadPrimitive("Emitter Pad Soft Active Lens", PrimitiveType.Cylinder, glow, pad.transform, new Vector3(0f, 0.024f, 0f), new Vector3(0.16f, 0.006f, 0.16f));
        }

        private static GameObject CreatePadPrimitive(string name, PrimitiveType primitiveType, Material material, Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;

            if (primitive.TryGetComponent<Collider>(out var collider))
            {
                ImportedModelUtility.DestroyObject(collider);
            }

            if (primitive.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private static Material CreatePadMaterial(Material sourceMaterial)
        {
            var shader = sourceMaterial != null ? sourceMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = "Spent Pickup Pad Gunmetal" };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.18f, 0.19f, 0.20f));
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.18f, 0.19f, 0.20f));
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.72f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.44f);
            }

            return material;
        }

        private static Material CreatePadGlowMaterial(Material sourceMaterial, Color auraColor)
        {
            var shader = sourceMaterial != null ? sourceMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = "Spent Pickup Pad Dim Lens" };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", auraColor * 0.28f);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", auraColor * 0.28f);
            }

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", auraColor * 0.12f);
            }

            return material;
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
