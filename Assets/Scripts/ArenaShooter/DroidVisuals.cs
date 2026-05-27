using UnityEngine;

namespace ArenaShooter
{
    public static class DroidVisuals
    {
        public static void Build(Transform parent, ArenaTheme theme)
        {
            if (BattleDroidAsset.TryBuild(parent, theme))
            {
                return;
            }

            var root = new GameObject("Droid Combat Frame Model").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, -0.55f, 0f);
            root.localScale = Vector3.one * 0.82f;

            CreatePart("Droid Pelvis", PrimitiveType.Cube, theme.DroidJoint, root, new Vector3(0f, 0.72f, 0f), new Vector3(0.44f, 0.2f, 0.24f), Vector3.zero);
            CreatePart("Droid Chest Plate", PrimitiveType.Cube, theme.DroidArmor, root, new Vector3(0f, 1.23f, 0f), new Vector3(0.5f, 0.72f, 0.22f), new Vector3(-5f, 0f, 0f));
            CreatePart("Droid Spine", PrimitiveType.Cylinder, theme.DroidJoint, root, new Vector3(0f, 0.98f, 0f), new Vector3(0.08f, 0.26f, 0.08f), new Vector3(0f, 0f, 0f));
            CreatePart("Droid Neck", PrimitiveType.Cylinder, theme.DroidJoint, root, new Vector3(0f, 1.68f, 0f), new Vector3(0.055f, 0.16f, 0.055f), Vector3.zero);
            CreatePart("Droid Long Head", PrimitiveType.Cube, theme.DroidArmor, root, new Vector3(0f, 1.86f, 0.05f), new Vector3(0.28f, 0.2f, 0.46f), new Vector3(8f, 0f, 0f));

            BuildArm(root, theme, -1f);
            BuildArm(root, theme, 1f);
            BuildLeg(root, theme, -1f);
            BuildLeg(root, theme, 1f);

            AddFallbackEye(root, theme);
            DroidRenderSetup.Apply(root.gameObject);
        }

        private static void AddFallbackEye(Transform root, ArenaTheme theme)
        {
            var eye = CreatePart("Droid Single Red Optic Eye", PrimitiveType.Sphere, theme.DroidEye, root, new Vector3(0f, 1.88f, 0.335f), new Vector3(0.12f, 0.12f, 0.035f), Vector3.zero);
            eye.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            var light = new GameObject("Droid Face Direction Light");
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0f, 1.88f, 0.43f);
            var faceLight = light.AddComponent<Light>();
            faceLight.type = LightType.Point;
            faceLight.shadows = LightShadows.None;
            faceLight.color = new Color(1f, 0.08f, 0.02f);
            faceLight.range = 1.9f;
            faceLight.intensity = 0.42f;
        }

        private static void BuildArm(Transform root, ArenaTheme theme, float side)
        {
            var shoulderX = 0.342f * side;
            CreatePart(side < 0f ? "Droid Left Shoulder" : "Droid Right Shoulder", PrimitiveType.Sphere, theme.DroidJoint, root, new Vector3(shoulderX, 1.5f, 0f), new Vector3(0.15f, 0.15f, 0.15f), Vector3.zero);
            CreatePart(side < 0f ? "Droid Left Elbow Joint" : "Droid Right Elbow Joint", PrimitiveType.Sphere, theme.DroidJoint, root, new Vector3(shoulderX + 0.14f * side, 1.06f, 0.09f), new Vector3(0.11f, 0.11f, 0.11f), Vector3.zero);
            CreatePart(side < 0f ? "Droid Left Wrist Joint" : "Droid Right Wrist Joint", PrimitiveType.Sphere, theme.DroidJoint, root, new Vector3(shoulderX + 0.21f * side, 0.69f, 0.21f), new Vector3(0.095f, 0.095f, 0.095f), Vector3.zero);
            CreatePart(side < 0f ? "Droid Left Upper Arm" : "Droid Right Upper Arm", PrimitiveType.Cylinder, theme.DroidArmor, root, new Vector3(shoulderX + 0.12f * side, 1.22f, 0.04f), new Vector3(0.055f, 0.29f, 0.055f), new Vector3(13f, 0f, -18f * side));
            CreatePart(side < 0f ? "Droid Left Forearm" : "Droid Right Forearm", PrimitiveType.Cylinder, theme.DroidArmor, root, new Vector3(shoulderX + 0.18f * side, 0.9f, 0.14f), new Vector3(0.05f, 0.28f, 0.05f), new Vector3(25f, 0f, -6f * side));
            CreatePart(side < 0f ? "Droid Left Claw Hand" : "Droid Right Claw Hand", PrimitiveType.Cube, theme.DroidJoint, root, new Vector3(shoulderX + 0.21f * side, 0.62f, 0.24f), new Vector3(0.16f, 0.09f, 0.11f), Vector3.zero);
        }

        private static void BuildLeg(Transform root, ArenaTheme theme, float side)
        {
            var hipX = 0.16f * side;
            CreatePart(side < 0f ? "Droid Left Hip Joint" : "Droid Right Hip Joint", PrimitiveType.Sphere, theme.DroidJoint, root, new Vector3(hipX, 0.62f, 0f), new Vector3(0.12f, 0.12f, 0.12f), Vector3.zero);
            CreatePart(side < 0f ? "Droid Left Knee Joint" : "Droid Right Knee Joint", PrimitiveType.Sphere, theme.DroidJoint, root, new Vector3(hipX, 0.18f, 0.02f), new Vector3(0.115f, 0.115f, 0.115f), Vector3.zero);
            CreatePart(side < 0f ? "Droid Left Ankle Joint" : "Droid Right Ankle Joint", PrimitiveType.Sphere, theme.DroidJoint, root, new Vector3(hipX, -0.31f, 0.08f), new Vector3(0.1f, 0.1f, 0.1f), Vector3.zero);
            CreatePart(side < 0f ? "Droid Left Thigh" : "Droid Right Thigh", PrimitiveType.Cylinder, theme.DroidArmor, root, new Vector3(hipX, 0.42f, 0f), new Vector3(0.065f, 0.34f, 0.065f), new Vector3(5f, 0f, 4f * side));
            CreatePart(side < 0f ? "Droid Left Shin" : "Droid Right Shin", PrimitiveType.Cylinder, theme.DroidArmor, root, new Vector3(hipX, -0.02f, 0.03f), new Vector3(0.055f, 0.35f, 0.055f), new Vector3(-3f, 0f, -3f * side));
            CreatePart(side < 0f ? "Droid Left Foot" : "Droid Right Foot", PrimitiveType.Cube, theme.DroidJoint, root, new Vector3(hipX, -0.39f, 0.12f), new Vector3(0.18f, 0.07f, 0.32f), Vector3.zero);
        }

        private static GameObject CreatePart(string objectName, PrimitiveType type, Material material, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localRotation)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(localRotation);

            if (part.TryGetComponent<Collider>(out var collider))
            {
                Object.Destroy(collider);
            }

            if (part.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }

            return part;
        }
    }
}
