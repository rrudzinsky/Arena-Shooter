using UnityEngine;

namespace ArenaShooter
{
    public static class DroidHitboxRig
    {
        public static void Build(Transform droid)
        {
            var root = new GameObject("Droid Hitbox Rig").transform;
            root.SetParent(droid, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;

            AddCapsule(root, "Droid Full Body Hitbox", new Vector3(0f, 0.1f, 0.04f), 0.43f, 1.95f);
            AddBox(root, "Droid Torso Hitbox", new Vector3(0f, 0.34f, 0.02f), new Vector3(0.74f, 0.86f, 0.46f), Vector3.zero);
            AddBox(root, "Droid Head Hitbox", new Vector3(0f, 0.98f, 0.07f), new Vector3(0.5f, 0.36f, 0.56f), new Vector3(8f, 0f, 0f));
            AddBox(root, "Droid Pelvis Hitbox", new Vector3(0f, -0.12f, 0.02f), new Vector3(0.62f, 0.34f, 0.4f), Vector3.zero);

            AddArm(root, -1f);
            AddArm(root, 1f);
            AddLeg(root, -1f);
            AddLeg(root, 1f);
        }

        private static void AddArm(Transform root, float side)
        {
            AddBox(root, side < 0f ? "Droid Left Shoulder Hitbox" : "Droid Right Shoulder Hitbox", new Vector3(0.31f * side, 0.58f, 0.02f), new Vector3(0.28f, 0.28f, 0.28f), Vector3.zero);
            AddBox(root, side < 0f ? "Droid Left Upper Arm Hitbox" : "Droid Right Upper Arm Hitbox", new Vector3(0.42f * side, 0.34f, 0.08f), new Vector3(0.25f, 0.5f, 0.25f), new Vector3(0f, 0f, -20f * side));
            AddBox(root, side < 0f ? "Droid Left Forearm Hitbox" : "Droid Right Forearm Hitbox", new Vector3(0.49f * side, 0.04f, 0.18f), new Vector3(0.24f, 0.46f, 0.24f), new Vector3(14f, 0f, -8f * side));
            AddBox(root, side < 0f ? "Droid Left Hand Hitbox" : "Droid Right Hand Hitbox", new Vector3(0.52f * side, -0.15f, 0.28f), new Vector3(0.28f, 0.22f, 0.24f), Vector3.zero);
        }

        private static void AddLeg(Transform root, float side)
        {
            AddBox(root, side < 0f ? "Droid Left Thigh Hitbox" : "Droid Right Thigh Hitbox", new Vector3(0.14f * side, -0.42f, 0.02f), new Vector3(0.17f, 0.5f, 0.18f), new Vector3(0f, 0f, 3f * side));
            AddBox(root, side < 0f ? "Droid Left Shin Hitbox" : "Droid Right Shin Hitbox", new Vector3(0.14f * side, -0.78f, 0.08f), new Vector3(0.15f, 0.38f, 0.16f), new Vector3(0f, 0f, -3f * side));
        }

        private static void AddBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Vector3 localRotation)
        {
            var hitbox = new GameObject(name);
            hitbox.transform.SetParent(parent, false);
            hitbox.transform.localPosition = localPosition;
            hitbox.transform.localRotation = Quaternion.Euler(localRotation);
            var collider = hitbox.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;
        }

        private static void AddCapsule(Transform parent, string name, Vector3 localPosition, float radius, float height)
        {
            var hitbox = new GameObject(name);
            hitbox.transform.SetParent(parent, false);
            hitbox.transform.localPosition = localPosition;
            var collider = hitbox.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.direction = 1;
            collider.radius = radius;
            collider.height = height;
        }
    }
}
