using UnityEngine;

namespace ArenaShooter
{
    public sealed class FirstPersonViewModel : MonoBehaviour
    {
        private Transform root;
        private Transform weaponRoot;
        private Transform ammoCore;
        private Transform muzzleGlow;
        private Transform pistolGripHandRoot;
        private Transform leftForearm;
        private Transform rightForearm;
        private Transform leftGlove;
        private Transform rightGlove;
        private Transform leftSprintArmRoot;
        private Transform rightSprintArmRoot;
        private Transform leftSprintForearm;
        private Transform rightSprintForearm;
        private Transform leftSprintFist;
        private Transform rightSprintFist;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Vector3 weaponBasePosition;
        private Quaternion weaponBaseRotation;
        private Vector3 leftForearmBasePosition;
        private Vector3 rightForearmBasePosition;
        private Vector3 leftGloveBasePosition;
        private Vector3 rightGloveBasePosition;
        private Quaternion leftForearmBaseRotation;
        private Quaternion rightForearmBaseRotation;
        private Quaternion leftGloveBaseRotation;
        private Quaternion rightGloveBaseRotation;
        private Vector3 leftForearmBaseScale;
        private Vector3 rightForearmBaseScale;
        private Vector3 leftGloveBaseScale;
        private Vector3 rightGloveBaseScale;
        private Vector3 lastWorldPosition;
        private float recoil;
        private float ammoMax = 1f;
        private float walkPhase;
        private float smoothedWalk;
        private bool hasWeapon;
        private bool aiming;
        private bool moving;
        private bool sprinting;
        private bool hasLastWorldPosition;

        public Transform Muzzle { get; private set; }

        public void Build(Transform cameraTransform, ArenaTheme theme)
        {
            root = new GameObject("First Person Arms").transform;
            root.SetParent(cameraTransform, false);
            basePosition = new Vector3(0.32f, -0.34f, 0.58f);
            baseRotation = Quaternion.Euler(0f, -2f, 0f);
            root.localPosition = basePosition;
            root.localRotation = baseRotation;

            if (!FirstPersonHandsAsset.TryBuild(root, theme, out leftForearm, out rightForearm, out leftGlove, out rightGlove))
            {
                leftForearm = CreateLimb("Left Forearm", theme.Player, new Vector3(-0.22f, -0.08f, 0.06f), new Vector3(0.075f, 0.34f, 0.075f), new Vector3(72f, 18f, -22f)).transform;
                rightForearm = CreateLimb("Right Forearm", theme.Player, new Vector3(0.12f, -0.06f, 0.05f), new Vector3(0.075f, 0.38f, 0.075f), new Vector3(76f, -12f, 17f)).transform;
                leftGlove = CreatePrimitive("Left Glove", PrimitiveType.Sphere, theme.NeonA, new Vector3(-0.13f, -0.02f, 0.31f), new Vector3(0.12f, 0.09f, 0.11f), Vector3.zero, root).transform;
                rightGlove = CreatePrimitive("Right Glove", PrimitiveType.Sphere, theme.NeonA, new Vector3(0.06f, -0.03f, 0.34f), new Vector3(0.12f, 0.09f, 0.11f), Vector3.zero, root).transform;
            }
            leftForearmBasePosition = leftForearm.localPosition;
            rightForearmBasePosition = rightForearm.localPosition;
            leftForearmBaseRotation = leftForearm.localRotation;
            rightForearmBaseRotation = rightForearm.localRotation;
            leftGloveBaseRotation = leftGlove.localRotation;
            rightGloveBaseRotation = rightGlove.localRotation;
            leftForearmBaseScale = leftForearm.localScale;
            rightForearmBaseScale = rightForearm.localScale;
            leftGloveBasePosition = leftGlove.localPosition;
            rightGloveBasePosition = rightGlove.localPosition;
            leftGloveBaseScale = leftGlove.localScale;
            rightGloveBaseScale = rightGlove.localScale;

            weaponRoot = new GameObject("Pulse Pistol View").transform;
            weaponRoot.SetParent(root, false);
            weaponBasePosition = new Vector3(0f, 0.02f, 0.34f);
            weaponBaseRotation = Quaternion.identity;
            weaponRoot.localPosition = weaponBasePosition;
            weaponRoot.localRotation = weaponBaseRotation;

            if (!PulsePistolAsset.TryBuildViewModel(weaponRoot, theme, out muzzleGlow))
            {
                CreatePrimitive("Pistol Frame", PrimitiveType.Cube, theme.Wall, new Vector3(0.02f, 0.02f, 0.05f), new Vector3(0.18f, 0.16f, 0.44f), Vector3.zero, weaponRoot);
                CreatePrimitive("Pistol Barrel", PrimitiveType.Cube, theme.NeonB, new Vector3(0.02f, 0.08f, 0.34f), new Vector3(0.11f, 0.08f, 0.34f), Vector3.zero, weaponRoot);
                CreatePrimitive("Grip", PrimitiveType.Cube, theme.Wall, new Vector3(0.03f, -0.16f, -0.05f), new Vector3(0.12f, 0.28f, 0.12f), new Vector3(-16f, 0f, 0f), weaponRoot);
                ammoCore = CreatePrimitive("Ammo Core", PrimitiveType.Cube, theme.Pickup, new Vector3(-0.09f, 0.03f, 0.04f), new Vector3(0.035f, 0.18f, 0.28f), Vector3.zero, weaponRoot).transform;
                muzzleGlow = CreatePrimitive("Muzzle Glow", PrimitiveType.Sphere, theme.Beam, new Vector3(0.02f, 0.08f, 0.54f), new Vector3(0.09f, 0.09f, 0.09f), Vector3.zero, weaponRoot).transform;
                DroidRenderSetup.Apply(weaponRoot.gameObject, StylizedOutlineCategory.FirstPersonPistol);
                MarkFirstPersonWeaponOccluders(weaponRoot);
            }

            Muzzle = muzzleGlow;
            BuildPistolGripHand(theme);
            BuildSprintHands(theme);
            SetWeaponVisible(false);
        }

        public void SetWeaponVisible(bool visible)
        {
            hasWeapon = visible;
            if (weaponRoot != null)
            {
                weaponRoot.gameObject.SetActive(visible);
            }

            ApplyVisibilityState();
        }

        public void SetAmmo(int ammo, int maxAmmo)
        {
            ammoMax = Mathf.Max(1f, maxAmmo);
            if (ammoCore == null)
            {
                return;
            }

            var ratio = Mathf.Clamp01(ammo / ammoMax);
            ammoCore.localScale = new Vector3(0.035f, Mathf.Lerp(0.035f, 0.18f, ratio), 0.28f);
            ammoCore.localPosition = new Vector3(-0.09f, Mathf.Lerp(-0.045f, 0.03f, ratio), 0.04f);
        }

        public void SetAiming(bool isAiming)
        {
            aiming = isAiming;
        }

        public void SetMovementState(bool isMoving, bool isSprinting)
        {
            moving = isMoving;
            sprinting = isSprinting;
        }

        public void PlayFire()
        {
            recoil = 1f;
        }

        private void Update()
        {
            if (root == null)
            {
                return;
            }

            var targetPosition = ResolveRootTargetPosition();
            var targetRotation = aiming ? Quaternion.Euler(0f, 0f, 0f) : baseRotation;
            var walk = UpdateWalkAmount();
            var bob = hasWeapon && !aiming ? Mathf.Sin(walkPhase * 2f) * 0.012f * walk : 0f;
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
            root.localPosition = Vector3.Lerp(root.localPosition, targetPosition + new Vector3(0f, bob, -recoil * 0.07f), Time.deltaTime * 16f);
            root.localRotation = Quaternion.Slerp(root.localRotation, targetRotation * Quaternion.Euler(-recoil * 7f, recoil * 1.5f, 0f), Time.deltaTime * 16f);
            AnimateArms(walk);

            if (muzzleGlow != null)
            {
                muzzleGlow.localScale = Vector3.one * Mathf.Lerp(0.055f, 0.16f, recoil);
            }
        }

        private Vector3 ResolveRootTargetPosition()
        {
            if (hasWeapon)
            {
                return aiming ? new Vector3(0.02f, -0.25f, 0.52f) : basePosition;
            }

            return sprinting ? new Vector3(0f, -0.18f, 0.62f) : new Vector3(0f, -1.05f, 0.46f);
        }

        private float UpdateWalkAmount()
        {
            if (!hasLastWorldPosition)
            {
                lastWorldPosition = root.position;
                hasLastWorldPosition = true;
                return smoothedWalk;
            }

            var delta = root.position - lastWorldPosition;
            lastWorldPosition = root.position;
            delta.y = 0f;
            var speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            var targetWalk = aiming ? 0f : Mathf.Clamp01(speed / 2.2f);
            smoothedWalk = Mathf.MoveTowards(smoothedWalk, targetWalk, Time.deltaTime * 5.5f);
            walkPhase += Time.deltaTime * Mathf.Lerp(5.5f, 9.5f, smoothedWalk);
            return smoothedWalk;
        }

        private void AnimateArms(float walk)
        {
            ApplyVisibilityState();

            if (hasWeapon)
            {
                AnimateWeaponHand(walk);
                return;
            }

            if (!sprinting)
            {
                return;
            }

            AnimateSprintHands(walk);
        }

        private void AnimateWeaponHand(float walk)
        {
            var swing = Mathf.Sin(walkPhase) * walk;
            var counterSwing = Mathf.Sin(walkPhase + Mathf.PI) * walk;
            var lift = Mathf.Abs(Mathf.Sin(walkPhase)) * walk;

            if (weaponRoot != null)
            {
                weaponRoot.localPosition = weaponBasePosition + new Vector3(swing * 0.006f, lift * 0.008f, counterSwing * 0.012f);
                weaponRoot.localRotation = weaponBaseRotation * Quaternion.Euler(lift * 1.8f, swing * 0.9f, swing * 1.2f);
            }
        }

        private void AnimateSprintHands(float walk)
        {
            var swing = Mathf.Sin(walkPhase * 1.35f) * Mathf.Max(0.3f, walk);
            var counterSwing = Mathf.Sin(walkPhase * 1.35f + Mathf.PI) * Mathf.Max(0.3f, walk);
            var lift = Mathf.Abs(Mathf.Sin(walkPhase * 1.35f)) * Mathf.Max(0.35f, walk);

            if (leftSprintArmRoot != null)
            {
                leftSprintArmRoot.localPosition = new Vector3(-0.35f, -0.17f + lift * 0.018f, 0.46f + swing * 0.04f);
                leftSprintArmRoot.localRotation = Quaternion.Euler(24f + counterSwing * 7f, 14f, -13f);
            }

            if (rightSprintArmRoot != null)
            {
                rightSprintArmRoot.localPosition = new Vector3(0.35f, -0.17f + lift * 0.018f, 0.46f + counterSwing * 0.04f);
                rightSprintArmRoot.localRotation = Quaternion.Euler(24f + swing * 7f, -14f, 13f);
            }
        }

        private void ApplyVisibilityState()
        {
            var showSprintHands = !hasWeapon && sprinting;
            SetTransformVisible(leftForearm, false);
            SetTransformVisible(leftGlove, false);
            SetTransformVisible(rightForearm, false);
            SetTransformVisible(rightGlove, false);
            SetTransformVisible(leftSprintArmRoot, showSprintHands);
            SetTransformVisible(rightSprintArmRoot, showSprintHands);
            SetTransformVisible(pistolGripHandRoot, hasWeapon);

            if (!showSprintHands)
            {
                ResetOriginalHandPose();
            }
        }

        private void ResetOriginalHandPose()
        {
            if (leftForearm != null)
            {
                leftForearm.localPosition = leftForearmBasePosition;
                leftForearm.localRotation = leftForearmBaseRotation;
                leftForearm.localScale = leftForearmBaseScale;
            }

            if (rightForearm != null)
            {
                rightForearm.localPosition = rightForearmBasePosition;
                rightForearm.localRotation = rightForearmBaseRotation;
                rightForearm.localScale = rightForearmBaseScale;
            }

            if (leftGlove != null)
            {
                leftGlove.localPosition = leftGloveBasePosition;
                leftGlove.localRotation = leftGloveBaseRotation;
                leftGlove.localScale = leftGloveBaseScale;
            }

            if (rightGlove != null)
            {
                rightGlove.localPosition = rightGloveBasePosition;
                rightGlove.localRotation = rightGloveBaseRotation;
                rightGlove.localScale = rightGloveBaseScale;
            }
        }

        private static void SetTransformVisible(Transform target, bool visible)
        {
            if (target != null && target.gameObject.activeSelf != visible)
            {
                target.gameObject.SetActive(visible);
            }
        }

        private void BuildPistolGripHand(ArenaTheme theme)
        {
            if (weaponRoot == null)
            {
                return;
            }

            pistolGripHandRoot = new GameObject("Pistol Grip Hand").transform;
            pistolGripHandRoot.SetParent(weaponRoot, false);
            pistolGripHandRoot.localPosition = Vector3.zero;
            pistolGripHandRoot.localRotation = Quaternion.identity;

            var shell = theme.MedicalWhite;
            var glove = theme.Wall;
            var joint = theme.Pillar;
            CreatePrimitive("Pistol White Wrist Shell", PrimitiveType.Capsule, shell, new Vector3(0.075f, -0.285f, -0.075f), new Vector3(0.07f, 0.13f, 0.07f), new Vector3(68f, -10f, 8f), pistolGripHandRoot);
            CreatePrimitive("Pistol Black Wrist Joint", PrimitiveType.Cylinder, glove, new Vector3(0.06f, -0.225f, -0.042f), new Vector3(0.068f, 0.034f, 0.068f), new Vector3(68f, -10f, 8f), pistolGripHandRoot);
            CreateRoundedPalm("Pistol Rounded Black Palm", glove, new Vector3(0.04f, -0.145f, 0.018f), new Vector3(0.155f, 0.108f, 0.088f), new Vector3(0f, 0f, -8f), pistolGripHandRoot);
            CreatePrimitive("Pistol White Backhand Plate", PrimitiveType.Cube, shell, new Vector3(0.042f, -0.14f, -0.026f), new Vector3(0.13f, 0.086f, 0.022f), new Vector3(0f, 0f, -8f), pistolGripHandRoot);
            CreatePrimitive("Pistol White Palm Side Plate", PrimitiveType.Cube, shell, new Vector3(0.115f, -0.142f, 0.02f), new Vector3(0.028f, 0.1f, 0.082f), new Vector3(0f, 0f, -8f), pistolGripHandRoot);
            BuildCurledRobotThumb(pistolGripHandRoot, theme, new Vector3(0.112f, -0.15f, 0.052f), 1f, true);

            for (var i = 0; i < 4; i++)
            {
                var y = -0.092f - i * 0.029f;
                BuildCurledRobotFinger(pistolGripHandRoot, theme, i + 1, new Vector3(-0.026f, y, 0.06f), 1f, true);
            }
        }

        private void BuildSprintHands(ArenaTheme theme)
        {
            leftSprintArmRoot = BuildSprintArm("Left Sprint Arm", -1f, theme);
            rightSprintArmRoot = BuildSprintArm("Right Sprint Arm", 1f, theme);
            leftSprintForearm = leftSprintArmRoot.Find("Sprint Forearm Sleeve");
            rightSprintForearm = rightSprintArmRoot.Find("Sprint Forearm Sleeve");
            leftSprintFist = leftSprintArmRoot.Find("Closed Sprint Fist");
            rightSprintFist = rightSprintArmRoot.Find("Closed Sprint Fist");
        }

        private Transform BuildSprintArm(string objectName, float side, ArenaTheme theme)
        {
            var armRoot = new GameObject(objectName).transform;
            armRoot.SetParent(root, false);
            armRoot.localPosition = new Vector3(0.34f * side, -0.18f, 0.46f);
            armRoot.localRotation = Quaternion.Euler(24f, -20f * side, 20f * side);

            var forearmAxis = new Vector3(58f, 0f, 7f * side);
            var forearmPosition = new Vector3(-0.018f * side, -0.105f, -0.03f);
            var forearmLength = 0.46f;
            var forearmRotation = Quaternion.Euler(forearmAxis);
            var forearm = CreateTaperedForearm("Sprint Forearm Sleeve", theme.MedicalWhite, forearmPosition, 0.105f, 0.066f, forearmLength, forearmAxis, armRoot);
            var wristPosition = forearmPosition + forearmRotation * (Vector3.up * (forearmLength * 0.5f));
            var wristForward = forearmRotation * Vector3.up;
            var wristOut = forearmRotation * Vector3.right * side;
            var wristUp = forearmRotation * Vector3.forward;
            CreatePrimitive("Sprint Black Upper Forearm Band", PrimitiveType.Cylinder, theme.Wall, new Vector3(-0.026f * side, -0.205f, -0.132f), new Vector3(0.112f, 0.022f, 0.112f), new Vector3(58f, 0f, 7f * side), armRoot);
            CreatePrimitive("Sprint Black Wrist Forearm Band", PrimitiveType.Cylinder, theme.Wall, wristPosition - wristForward * 0.014f, new Vector3(0.074f, 0.024f, 0.074f), forearmAxis, armRoot);
            CreatePrimitive("Sprint Black Rounded Side Recess", PrimitiveType.Capsule, theme.Pillar, new Vector3(0.066f * side, -0.075f, 0.018f), new Vector3(0.018f, 0.092f, 0.018f), new Vector3(58f, 0f, 7f * side), armRoot);
            var cuff = CreatePrimitive("Sprint Glove Cuff", PrimitiveType.Cylinder, theme.Wall, wristPosition + wristForward * 0.008f, new Vector3(0.088f, 0.042f, 0.088f), forearmAxis, armRoot);
            CreatePrimitive("Sprint White Wrist Ring", PrimitiveType.Cylinder, theme.MedicalWhite, wristPosition - wristForward * 0.004f, new Vector3(0.094f, 0.018f, 0.094f), forearmAxis, armRoot);
            var fistRotation = new Vector3(-6f, -16f * side, -18f * side);
            var palmCenter = wristPosition + wristForward * 0.093f - wristOut * 0.018f - wristUp * 0.012f;
            var handRoot = new GameObject("Sprint Hand Root").transform;
            handRoot.SetParent(armRoot, false);
            handRoot.localPosition = palmCenter;
            handRoot.localRotation = Quaternion.Euler(fistRotation);

            var palm = CreatePrimitive("Closed Sprint Fist", PrimitiveType.Sphere, theme.Wall, Vector3.zero, new Vector3(0.148f, 0.1f, 0.112f), Vector3.zero, handRoot);
            CreatePrimitive("Closed Sprint Soft Backhand Pad", PrimitiveType.Sphere, theme.MedicalWhite, new Vector3(-0.006f * side, 0.026f, -0.042f), new Vector3(0.118f, 0.056f, 0.024f), Vector3.zero, handRoot);
            BuildRobotFistFingers(handRoot, theme, side, Vector3.zero);
            BuildSprintThumb(handRoot, theme, side);
            forearm.name = "Sprint Forearm Sleeve";
            cuff.name = "Sprint Glove Cuff";
            palm.name = "Closed Sprint Fist";
            return armRoot;
        }

        private void BuildSprintThumb(Transform parent, ArenaTheme theme, float side)
        {
            var sign = Mathf.Sign(side == 0f ? 1f : side);

            var root = new Vector3(0.048f * sign, 0.032f, -0.13f);
            var tip = new Vector3(-0.104f * sign, -0.018f, -0.128f);
            var padCenter = (root + tip) * 0.5f;

            CreateCapsuleBetween("Closed Sprint Front Thumb", theme.MedicalWhite, root, tip, 0.033f, parent);
            CreatePrimitive("Closed Sprint Front Thumb Pad", PrimitiveType.Sphere, theme.MedicalWhite, padCenter + new Vector3(-0.012f * sign, 0.002f, -0.006f), new Vector3(0.068f, 0.028f, 0.032f), new Vector3(0f, 0f, -18f * sign), parent);
            CreatePrimitive("Closed Sprint Front Thumb Tip", PrimitiveType.Sphere, theme.MedicalWhite, tip, new Vector3(0.032f, 0.024f, 0.032f), Vector3.zero, parent);
        }

        private void BuildRobotFistFingers(Transform parent, ArenaTheme theme, float side, Vector3 palmCenter)
        {
            for (var i = 0; i < 4; i++)
            {
                var x = (-0.052f + i * 0.035f) * side;
                var y = 0.026f - Mathf.Abs(i - 1.5f) * 0.002f;
                var z = 0.066f - Mathf.Abs(i - 1.5f) * 0.002f;
                BuildCurledRobotFinger(parent, theme, i + 1, palmCenter + new Vector3(x - 0.032f * side, y, z), side, false);
            }
        }

        private void BuildCurledRobotFinger(Transform parent, ArenaTheme theme, int index, Vector3 basePosition, float side, bool gripPose)
        {
            var sign = Mathf.Sign(side == 0f ? 1f : side);
            var prefix = gripPose ? "Grip" : "Sprint";
            if (!gripPose)
            {
                var fingerRotation = new Vector3(108f, -8f * sign, 12f * sign);
                CreatePrimitive($"{prefix} Finger {index} White Base Knuckle", PrimitiveType.Sphere, theme.MedicalWhite, basePosition + new Vector3(0f, 0.002f, -0.002f), new Vector3(0.028f, 0.024f, 0.028f), Vector3.zero, parent);
                CreatePrimitive($"{prefix} Finger {index} Black Joint Groove", PrimitiveType.Sphere, theme.Wall, basePosition + new Vector3(0f, -0.003f, 0.016f), new Vector3(0.022f, 0.017f, 0.022f), Vector3.zero, parent);
                CreatePrimitive($"{prefix} Finger {index} Curled White Digit", PrimitiveType.Capsule, theme.MedicalWhite, basePosition + new Vector3(0f, -0.014f, 0.026f), new Vector3(0.023f, 0.052f, 0.023f), fingerRotation, parent);
                CreatePrimitive($"{prefix} Finger {index} Rounded Fingertip", PrimitiveType.Sphere, theme.MedicalWhite, basePosition + new Vector3(0f, -0.03f, 0.038f), new Vector3(0.024f, 0.02f, 0.024f), Vector3.zero, parent);
                return;
            }

            var baseRotation = gripPose ? new Vector3(82f, 0f, 88f) : new Vector3(62f, -18f * sign, -14f * sign);
            var curlRotation = gripPose ? baseRotation + new Vector3(24f, 0f, 14f * sign) : new Vector3(98f, -12f * sign, 18f * sign);
            var knuckleScale = gripPose ? new Vector3(0.022f, 0.022f, 0.022f) : new Vector3(0.031f, 0.026f, 0.028f);
            CreatePrimitive($"{prefix} Finger {index} Black Knuckle", PrimitiveType.Sphere, theme.Wall, basePosition + new Vector3(0f, 0.003f, -0.012f), knuckleScale, Vector3.zero, parent);
            CreatePrimitive($"{prefix} Finger {index} White Proximal Segment", PrimitiveType.Capsule, theme.MedicalWhite, basePosition + new Vector3(0f, gripPose ? 0.002f : -0.004f, 0.022f), new Vector3(0.022f, gripPose ? 0.034f : 0.058f, 0.022f), baseRotation, parent);
            CreatePrimitive($"{prefix} Finger {index} Black Middle Joint", PrimitiveType.Sphere, theme.Wall, basePosition + new Vector3(0f, gripPose ? -0.013f : -0.028f, 0.046f), new Vector3(0.022f, 0.02f, 0.022f), Vector3.zero, parent);
            CreatePrimitive($"{prefix} Finger {index} White Curled Tip", PrimitiveType.Capsule, theme.MedicalWhite, basePosition + new Vector3(0f, gripPose ? -0.027f : -0.054f, 0.054f), new Vector3(0.02f, gripPose ? 0.028f : 0.045f, 0.02f), curlRotation, parent);
        }

        private void BuildCurledRobotThumb(Transform parent, ArenaTheme theme, Vector3 basePosition, float side, bool gripPose)
        {
            var sign = Mathf.Sign(side == 0f ? 1f : side);
            if (!gripPose)
            {
                var thumbRotation = new Vector3(0f, -16f * sign, -36f * sign);
                CreatePrimitive("Closed Sprint Black Thumb Saddle", PrimitiveType.Sphere, theme.Wall, basePosition, new Vector3(0.03f, 0.026f, 0.03f), Vector3.zero, parent);
                CreatePrimitive("Closed Sprint White Thumb Main Pad", PrimitiveType.Sphere, theme.MedicalWhite, basePosition + new Vector3(-0.032f * sign, -0.026f, 0.026f), new Vector3(0.04f, 0.024f, 0.044f), thumbRotation, parent);
                CreatePrimitive("Closed Sprint White Thumb Tip Pad", PrimitiveType.Sphere, theme.MedicalWhite, basePosition + new Vector3(-0.066f * sign, -0.052f, 0.046f), new Vector3(0.03f, 0.021f, 0.032f), thumbRotation, parent);
                return;
            }

            var rotation = gripPose ? new Vector3(66f, 0f, -34f * sign) : new Vector3(28f, -28f * sign, -78f * sign);
            CreatePrimitive(gripPose ? "Grip Black Thumb Knuckle" : "Closed Sprint Black Thumb Knuckle", PrimitiveType.Sphere, theme.Wall, basePosition, new Vector3(0.034f, 0.03f, 0.034f), Vector3.zero, parent);
            CreatePrimitive(gripPose ? "Grip White Thumb Segment" : "Closed Sprint White Thumb Segment", PrimitiveType.Capsule, theme.MedicalWhite, basePosition + new Vector3(0.028f * sign, 0.02f, 0.028f), new Vector3(0.026f, 0.06f, 0.026f), rotation, parent);
            CreatePrimitive(gripPose ? "Grip White Thumb Tip" : "Closed Sprint White Thumb Tip", PrimitiveType.Capsule, theme.MedicalWhite, basePosition + new Vector3(0.052f * sign, 0.034f, 0.055f), new Vector3(0.023f, 0.042f, 0.023f), rotation + new Vector3(12f, -8f * sign, -12f * sign), parent);
        }

        private GameObject CreateTaperedForearm(string objectName, Material material, Vector3 position, float upperRadius, float wristRadius, float length, Vector3 rotation, Transform parent)
        {
            const int segments = 18;
            var mesh = new Mesh { name = objectName + " Mesh" };
            var vertices = new Vector3[segments * 2 + 2];
            var triangles = new int[segments * 12];
            var bottomCenter = segments * 2;
            var topCenter = bottomCenter + 1;
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var ring = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i] = ring * upperRadius + Vector3.down * length * 0.5f;
                vertices[i + segments] = ring * wristRadius + Vector3.up * length * 0.5f;
            }

            vertices[bottomCenter] = Vector3.down * length * 0.5f;
            vertices[topCenter] = Vector3.up * length * 0.5f;

            var t = 0;
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                triangles[t++] = i;
                triangles[t++] = i + segments;
                triangles[t++] = next + segments;
                triangles[t++] = i;
                triangles[t++] = next + segments;
                triangles[t++] = next;
                triangles[t++] = bottomCenter;
                triangles[t++] = next;
                triangles[t++] = i;
                triangles[t++] = topCenter;
                triangles[t++] = i + segments;
                triangles[t++] = next + segments;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var forearm = new GameObject(objectName);
            forearm.transform.SetParent(parent, false);
            forearm.transform.localPosition = position;
            forearm.transform.localRotation = Quaternion.Euler(rotation);
            var filter = forearm.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = forearm.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);
            return forearm;
        }

        private GameObject CreateRoundedPalm(string objectName, Material material, Vector3 position, Vector3 scale, Vector3 rotation, Transform parent)
        {
            var palm = CreatePrimitive(objectName, PrimitiveType.Sphere, material, position, scale, rotation, parent);
            CreatePrimitive(objectName + " Heel Pad", PrimitiveType.Sphere, material, position + new Vector3(0f, -scale.y * 0.36f, scale.z * 0.18f), new Vector3(scale.x * 0.84f, scale.y * 0.56f, scale.z * 0.72f), rotation, parent);
            return palm;
        }

        private GameObject CreateLimb(string name, Material material, Vector3 position, Vector3 scale, Vector3 rotation)
        {
            return CreatePrimitive(name, PrimitiveType.Cylinder, material, position, scale, rotation, root);
        }

        private GameObject CreateCapsuleBetween(string objectName, Material material, Vector3 start, Vector3 end, float radius, Transform parent)
        {
            var direction = end - start;
            var length = direction.magnitude;
            var capsule = CreatePrimitive(objectName, PrimitiveType.Capsule, material, (start + end) * 0.5f, new Vector3(radius, length * 0.5f, radius), Vector3.zero, parent);

            if (length > 0.0001f)
            {
                capsule.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            }

            return capsule;
        }

        private GameObject CreatePrimitive(string objectName, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Vector3 rotation, Transform parent)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.transform.localRotation = Quaternion.Euler(rotation);

            if (primitive.TryGetComponent<Collider>(out var collider))
            {
                ImportedModelUtility.DestroyObject(collider);
            }

            if (primitive.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);
            }

            return primitive;
        }

        private static void MarkFirstPersonWeaponOccluders(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                DroidRenderSetup.AddFirstPersonWeaponOccluder(renderer);
            }
        }
    }
}
