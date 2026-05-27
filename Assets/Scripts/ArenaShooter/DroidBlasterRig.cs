using UnityEngine;

namespace ArenaShooter
{
    public sealed class DroidBlasterRig : MonoBehaviour
    {
        private Transform blasterRoot;
        private Transform muzzle;
        private Transform supportArmRoot;
        private Transform visualShoulderAnchor;
        private Transform shoulderJoint;
        private Transform elbowJoint;
        private Transform wristJoint;
        private Transform upperArm;
        private Transform forearm;
        private Transform hand;
        private Vector3 standingBlasterPosition;
        private Vector3 crouchingBlasterPosition;
        private Vector3 lastAimPoint;
        private float crouchBlend;
        private bool crouching;
        private bool hasAimPoint;

        public Transform Muzzle => muzzle;

        public Transform Build(ArenaTheme theme)
        {
            blasterRoot = new GameObject("Droid Aimed Blaster").transform;
            blasterRoot.SetParent(transform, false);
            standingBlasterPosition = new Vector3(0.26f, 0.72f, 0.28f);
            crouchingBlasterPosition = new Vector3(0.24f, 0.32f, 0.72f);
            blasterRoot.localPosition = standingBlasterPosition;
            blasterRoot.localRotation = Quaternion.identity;

            CreatePart("Droid Blaster Stock", PrimitiveType.Cube, theme.DroidJoint, new Vector3(0f, 0f, -0.12f), new Vector3(0.12f, 0.11f, 0.22f), Vector3.zero);
            CreatePart("Droid Blaster Body", PrimitiveType.Cube, theme.DroidJoint, new Vector3(0f, 0f, 0.08f), new Vector3(0.16f, 0.13f, 0.32f), Vector3.zero);
            CreatePart("Droid Blaster Barrel", PrimitiveType.Cylinder, theme.DroidArmor, new Vector3(0f, 0f, 0.34f), new Vector3(0.035f, 0.18f, 0.035f), new Vector3(90f, 0f, 0f));
            CreatePart("Droid Blaster Grip", PrimitiveType.Cube, theme.DroidJoint, new Vector3(0f, -0.13f, -0.02f), new Vector3(0.075f, 0.18f, 0.085f), new Vector3(-14f, 0f, 0f));
            CreatePart("Droid Blaster Glow Rail", PrimitiveType.Cube, theme.DroidEye, new Vector3(0f, 0.075f, 0.08f), new Vector3(0.09f, 0.024f, 0.24f), Vector3.zero);
            BuildGripArm(theme);
            SuppressOriginalWeaponArm();

            var muzzleObject = new GameObject("Droid Blaster Muzzle");
            muzzleObject.transform.SetParent(blasterRoot, false);
            muzzleObject.transform.localPosition = new Vector3(0f, 0f, 0.46f);
            muzzle = muzzleObject.transform;
            DroidRenderSetup.Apply(blasterRoot.gameObject);
            if (supportArmRoot != null)
            {
                DroidRenderSetup.Apply(supportArmRoot.gameObject);
            }

            return muzzle;
        }

        public void SetCrouching(bool isCrouching)
        {
            crouching = isCrouching;
        }

        private void LateUpdate()
        {
            UpdateRigPose();
        }

        public void AimAt(Vector3 worldPoint)
        {
            if (blasterRoot == null)
            {
                return;
            }

            lastAimPoint = worldPoint;
            hasAimPoint = true;
            UpdateRigPose();
        }

        public void ClearAim()
        {
            hasAimPoint = false;
            UpdateRigPose();
        }

        private void UpdateRigPose()
        {
            if (blasterRoot == null)
            {
                return;
            }

            crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1f : 0f, Time.deltaTime * 8f);
            blasterRoot.localPosition = Vector3.Lerp(standingBlasterPosition, crouchingBlasterPosition, crouchBlend);

            if (!hasAimPoint)
            {
                blasterRoot.localRotation = Quaternion.identity;
                PoseGripArm();
                return;
            }

            var direction = lastAimPoint - blasterRoot.position;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            blasterRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            PoseGripArm();
        }

        private void BuildGripArm(ArenaTheme theme)
        {
            supportArmRoot = new GameObject("Droid Attached Blaster Arm").transform;
            supportArmRoot.SetParent(transform, false);

            shoulderJoint = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            shoulderJoint.name = "Droid Gun Shoulder Joint";
            shoulderJoint.SetParent(supportArmRoot, false);
            shoulderJoint.localScale = new Vector3(0.18f, 0.18f, 0.18f);
            if (shoulderJoint.TryGetComponent<Collider>(out var shoulderCollider))
            {
                Destroy(shoulderCollider);
            }

            if (shoulderJoint.TryGetComponent<Renderer>(out var shoulderRenderer))
            {
                shoulderRenderer.sharedMaterial = theme.DroidJoint;
            }

            upperArm = CreateArmPart("Droid Gun Upper Arm", theme.DroidArmor, 0.09f).transform;
            elbowJoint = CreateJoint("Droid Gun Elbow Joint", theme.DroidJoint, 0.12f);
            forearm = CreateArmPart("Droid Gun Forearm", theme.DroidArmor, 0.082f).transform;
            wristJoint = CreateJoint("Droid Gun Wrist Joint", theme.DroidJoint, 0.105f);
            hand = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            hand.name = "Droid Gun Grip Hand";
            hand.SetParent(supportArmRoot, false);
            hand.localScale = new Vector3(0.2f, 0.13f, 0.17f);
            if (hand.TryGetComponent<Collider>(out var handCollider))
            {
                Destroy(handCollider);
            }

            if (hand.TryGetComponent<Renderer>(out var handRenderer))
            {
                handRenderer.sharedMaterial = theme.DroidJoint;
            }

            PoseGripArm();
        }

        private Transform CreateJoint(string objectName, Material material, float size)
        {
            var joint = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            joint.name = objectName;
            joint.SetParent(supportArmRoot, false);
            joint.localScale = new Vector3(size, size, size);
            if (joint.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (joint.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }

            return joint;
        }

        private GameObject CreateArmPart(string objectName, Material material, float radius)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = objectName;
            part.transform.SetParent(supportArmRoot, false);
            part.transform.localScale = new Vector3(radius, 0.25f, radius);
            if (part.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (part.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }

            return part;
        }

        private void PoseGripArm()
        {
            if (supportArmRoot == null || blasterRoot == null || upperArm == null || forearm == null || hand == null)
            {
                return;
            }

            var shoulder = visualShoulderAnchor != null
                ? visualShoulderAnchor.position
                : transform.TransformPoint(Vector3.Lerp(new Vector3(0.34f, 0.72f, 0.03f), new Vector3(0.38f, 0.43f, 0.08f), crouchBlend));
            var grip = blasterRoot.TransformPoint(new Vector3(0f, -0.13f, -0.02f));
            var elbowOffset = Vector3.Lerp(new Vector3(0.08f, -0.08f, 0.08f), new Vector3(0.1f, 0.02f, 0.18f), crouchBlend);
            var elbow = Vector3.Lerp(shoulder, grip, Mathf.Lerp(0.5f, 0.44f, crouchBlend)) + transform.TransformDirection(elbowOffset);
            if (shoulderJoint != null)
            {
                shoulderJoint.position = shoulder;
            }

            if (elbowJoint != null)
            {
                elbowJoint.position = elbow;
            }

            if (wristJoint != null)
            {
                wristJoint.position = grip;
            }

            PoseLimbSegment(upperArm, shoulder, elbow);
            PoseLimbSegment(forearm, elbow, grip);
            hand.position = grip;
            hand.rotation = blasterRoot.rotation * Quaternion.Euler(8f, 0f, 0f);
        }

        private void SuppressOriginalWeaponArm()
        {
            var grip = blasterRoot != null ? blasterRoot.TransformPoint(new Vector3(0f, -0.13f, -0.02f)) : transform.TransformPoint(standingBlasterPosition);
            visualShoulderAnchor = FindNearestOriginalShoulder(grip);
            var weaponSideName = ResolveNamedSide(visualShoulderAnchor);
            var weaponSideSign = ResolveLocalSideSign(visualShoulderAnchor);

            var children = GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child == transform || child.IsChildOf(supportArmRoot) || child.IsChildOf(blasterRoot))
                {
                    continue;
                }

                var name = child.name.ToLowerInvariant();
                if (!IsOriginalWeaponArmSegment(child, name, weaponSideName, weaponSideSign))
                {
                    continue;
                }

                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }
            }
        }

        private Transform FindNearestOriginalShoulder(Vector3 grip)
        {
            Transform best = null;
            var bestDistance = float.PositiveInfinity;
            var children = GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child == transform || child.IsChildOf(supportArmRoot) || child.IsChildOf(blasterRoot))
                {
                    continue;
                }

                var name = child.name.ToLowerInvariant();
                if (!name.Contains("shoulder"))
                {
                    continue;
                }

                var distance = (child.position - grip).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = child;
                }
            }

            return best;
        }

        private static string ResolveNamedSide(Transform shoulder)
        {
            if (shoulder == null)
            {
                return string.Empty;
            }

            var name = shoulder.name.ToLowerInvariant();
            if (name.Contains("left"))
            {
                return "left";
            }

            if (name.Contains("right"))
            {
                return "right";
            }

            return string.Empty;
        }

        private float ResolveLocalSideSign(Transform shoulder)
        {
            if (shoulder == null)
            {
                return 1f;
            }

            var local = transform.InverseTransformPoint(shoulder.position);
            return local.x < 0f ? -1f : 1f;
        }

        private bool IsOriginalWeaponArmSegment(Transform child, string name, string weaponSideName, float weaponSideSign)
        {
            var isArmSegment = name.Contains("upper arm") ||
                               name.Contains("lower arm") ||
                               name.Contains("forearm") ||
                               name.Contains("elbow") ||
                               name.Contains("wrist") ||
                               name.Contains("claw hand") ||
                               name.Contains("claw") ||
                               name.Contains("servo hand") ||
                               name.Contains("hand joint") ||
                               name.Contains("arm joint") ||
                               name.Contains(" hand") ||
                               name.EndsWith("hand");
            if (!isArmSegment)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(weaponSideName))
            {
                if (name.Contains(weaponSideName))
                {
                    return true;
                }

                var localWithNameFallback = transform.InverseTransformPoint(child.position);
                return Mathf.Sign(localWithNameFallback.x == 0f ? weaponSideSign : localWithNameFallback.x) == Mathf.Sign(weaponSideSign);
            }

            var local = transform.InverseTransformPoint(child.position);
            return Mathf.Sign(local.x == 0f ? weaponSideSign : local.x) == Mathf.Sign(weaponSideSign);
        }

        private void PoseLimbSegment(Transform segment, Vector3 start, Vector3 end)
        {
            var midpoint = (start + end) * 0.5f;
            var direction = end - start;
            segment.position = midpoint;
            if (direction.sqrMagnitude > 0.001f)
            {
                segment.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            }

            segment.localScale = new Vector3(segment.localScale.x, direction.magnitude * 0.5f, segment.localScale.z);
        }

        private GameObject CreatePart(string objectName, PrimitiveType type, Material material, Vector3 localPosition, Vector3 localScale, Vector3 localRotation)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = objectName;
            part.transform.SetParent(blasterRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(localRotation);

            if (part.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (part.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }

            return part;
        }
    }
}
