using UnityEngine;

namespace ArenaShooter
{
    public sealed class CombatantVisualWalkAnimator : MonoBehaviour
    {
        private const float ModelForwardZ = 1f;
        private const float LowerLegFootAnchorBack = 0.04f;
        private const float ArmElbowForwardStride = 0.055f;
        private const float ArmElbowBackwardStride = 0.07f;
        private const float ArmHandStride = 0.18f;

        private Part visualRoot;
        private Part leftUpperArm;
        private Part rightUpperArm;
        private Part leftForearm;
        private Part rightForearm;
        private Part leftShoulder;
        private Part rightShoulder;
        private Part leftElbow;
        private Part rightElbow;
        private Part leftWrist;
        private Part rightWrist;
        private Part leftHand;
        private Part rightHand;
        private Part leftThigh;
        private Part rightThigh;
        private Part leftShin;
        private Part rightShin;
        private Part leftHip;
        private Part rightHip;
        private Part leftKnee;
        private Part rightKnee;
        private Part leftAnkle;
        private Part rightAnkle;
        private Part leftFoot;
        private Part rightFoot;
        private Part leftFallbackArm;
        private Part rightFallbackArm;
        private Camera lodCamera;
        private Vector3 lastPosition;
        private float walkAmount;
        private float phase;
        private float crouchBlend;
        private float nextLodUpdateAt;
        private float lastSampleTime;
        private bool crouching;
        private bool cached;
        private bool hasLastPosition;
        private bool usesImportedBody;
        private const float FullAnimationDistance = 40f;
        private const float ReducedAnimationDistance = 86f;
        private const float MaxAnimationSampleDelta = 0.25f;

        public void ConfigureLod(Camera camera)
        {
            lodCamera = camera;
        }

        public void SetCrouching(bool isCrouching)
        {
            crouching = isCrouching;
        }

        private void LateUpdate()
        {
            if (!cached)
            {
                CacheParts();
            }

            if (ShouldSkipAnimationFrame())
            {
                return;
            }

            var sampleTime = Time.time;
            var rawSampleDelta = hasLastPosition
                ? Mathf.Max(sampleTime - lastSampleTime, 0.0001f)
                : Mathf.Max(Time.deltaTime, 0.0001f);
            var animationDelta = Mathf.Min(rawSampleDelta, MaxAnimationSampleDelta);
            var movement = Vector3.zero;
            if (hasLastPosition)
            {
                movement = transform.position - lastPosition;
                movement.y = 0f;
            }

            lastPosition = transform.position;
            lastSampleTime = sampleTime;
            hasLastPosition = true;

            var speed = movement.magnitude / rawSampleDelta;
            var targetWalk = Mathf.Clamp01(speed / 2.3f);
            walkAmount = Mathf.MoveTowards(walkAmount, targetWalk, animationDelta * 4.8f);
            crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1f : 0f, animationDelta * 7.5f);
            phase += animationDelta * Mathf.Lerp(crouching ? 2.8f : 5.2f, crouching ? 4.6f : 8.8f, walkAmount);

            ApplyWalkPose();
        }

        private bool ShouldSkipAnimationFrame()
        {
            if (lodCamera == null)
            {
                return false;
            }

            var toCamera = transform.position - lodCamera.transform.position;
            var distanceSqr = toCamera.sqrMagnitude;
            if (distanceSqr <= FullAnimationDistance * FullAnimationDistance)
            {
                return false;
            }

            if (Time.time < nextLodUpdateAt)
            {
                return true;
            }

            var viewport = lodCamera.WorldToViewportPoint(transform.position + Vector3.up * 0.8f);
            var inView = viewport.z > 0f &&
                viewport.x >= -0.15f &&
                viewport.x <= 1.15f &&
                viewport.y >= -0.15f &&
                viewport.y <= 1.15f;
            nextLodUpdateAt = Time.time + (inView && distanceSqr <= ReducedAnimationDistance * ReducedAnimationDistance ? 0.16f : 0.45f);
            return !inView || distanceSqr > ReducedAnimationDistance * ReducedAnimationDistance;
        }

        private void CacheParts()
        {
            var model = FindPart("cyber opponent combatant model");
            if (model == null)
            {
                model = FindPart("droid combat frame model");
            }
            visualRoot = model != null ? new Part(model) : default;
            usesImportedBody = model != null && HasDescendantNamed(model, "imported cyber battle droid mesh");
            if (usesImportedBody)
            {
                cached = true;
                return;
            }

            leftUpperArm = FindTrackedPart("left upper arm");
            rightUpperArm = FindTrackedPart("right upper arm");
            leftForearm = FindTrackedPart("left forearm");
            rightForearm = FindTrackedPart("right forearm");
            leftShoulder = FindTrackedPart("left shoulder");
            rightShoulder = FindTrackedPart("right shoulder");
            leftElbow = FindTrackedPart("left elbow");
            rightElbow = FindTrackedPart("right elbow");
            leftWrist = FindTrackedPart("left wrist");
            rightWrist = FindTrackedPart("right wrist");
            leftHand = FindTrackedPart("left claw hand");
            rightHand = FindTrackedPart("right claw hand");
            leftThigh = FindTrackedPart("left thigh");
            rightThigh = FindTrackedPart("right thigh");
            leftShin = FindTrackedPart("left shin");
            rightShin = FindTrackedPart("right shin");
            leftHip = FindTrackedPart("left hip");
            rightHip = FindTrackedPart("right hip");
            leftKnee = FindTrackedPart("left knee");
            rightKnee = FindTrackedPart("right knee");
            leftAnkle = FindTrackedPart("left ankle");
            rightAnkle = FindTrackedPart("right ankle");
            leftFoot = FindTrackedPart("left foot");
            rightFoot = FindTrackedPart("right foot");
            leftFallbackArm = FindTrackedPart("opponent left arm");
            rightFallbackArm = FindTrackedPart("opponent right arm");
            cached = true;
        }

        private void ApplyWalkPose()
        {
            var swing = Mathf.Sin(phase) * walkAmount;
            var counterSwing = Mathf.Sin(phase + Mathf.PI) * walkAmount;
            var stepLift = Mathf.Abs(Mathf.Sin(phase)) * walkAmount;
            var scoot = Mathf.Sin(phase * 1.65f) * walkAmount;
            var leftStride = counterSwing;
            var rightStride = swing;
            var leftLift = Mathf.Max(0f, leftStride) * walkAmount;
            var rightLift = Mathf.Max(0f, rightStride) * walkAmount;

            if (!usesImportedBody)
            {
                ApplyJointedWalkPose(leftStride, rightStride, leftLift, rightLift, scoot);
                ApplyBlendedRotation(leftFallbackArm, swing * 18f, 0f, counterSwing * 8f, -18f, -5f, -18f);
                ApplyBlendedRotation(rightFallbackArm, counterSwing * 18f, 0f, swing * 8f, -20f, 5f, 18f);
            }

            if (visualRoot.Transform != null)
            {
                var standingPosition = visualRoot.LocalPosition + new Vector3(0f, stepLift * 0.045f, swing * 0.018f);
                var crouchPosition = visualRoot.LocalPosition + new Vector3(0f, -0.42f + Mathf.Abs(scoot) * 0.018f, -0.13f + swing * 0.018f);
                visualRoot.Transform.localPosition = Vector3.Lerp(standingPosition, crouchPosition, crouchBlend);

                var standingRotation = visualRoot.LocalRotation * Quaternion.Euler(Mathf.Abs(swing) * 1.4f, 0f, swing * 3.2f);
                var crouchRotation = visualRoot.LocalRotation * Quaternion.Euler(19f + Mathf.Abs(scoot) * 1.6f, 0f, -6f + scoot * 2.2f);
                visualRoot.Transform.localRotation = Quaternion.Slerp(standingRotation, crouchRotation, crouchBlend);
            }
        }

        private void ApplyJointedWalkPose(float leftStride, float rightStride, float leftLift, float rightLift, float scoot)
        {
            PoseLeg(leftHip, leftKnee, leftAnkle, leftThigh, leftShin, leftFoot, leftStride, leftLift, -1f, scoot);
            PoseLeg(rightHip, rightKnee, rightAnkle, rightThigh, rightShin, rightFoot, rightStride, rightLift, 1f, -scoot);
            PoseArm(leftShoulder, leftUpperArm, leftForearm, leftElbow, leftWrist, leftHand, leftStride, -1f);
            PoseArm(rightShoulder, rightUpperArm, rightForearm, rightElbow, rightWrist, rightHand, rightStride, 1f);
        }

        private void PoseLeg(Part hip, Part knee, Part ankle, Part thigh, Part shin, Part foot, float stride, float lift, float side, float scoot)
        {
            if (knee.Transform == null)
            {
                return;
            }

            var forwardStride = Mathf.Max(0f, stride);
            var backwardStride = Mathf.Min(0f, stride);
            var kneeForward = forwardStride * 0.46f + backwardStride * 0.08f;
            var crouchedKneeForward = 0.34f + forwardStride * 0.22f + backwardStride * 0.04f + scoot * 0.06f;

            var hipPosition = hip.Transform != null ? hip.LocalPosition : ResolveUpperLegAnchor(thigh, knee);
            var kneeStanding = knee.LocalPosition + new Vector3(0f, lift * 0.07f + forwardStride * 0.035f, kneeForward * ModelForwardZ);
            var footBase = foot.Transform != null ? foot.LocalPosition : ResolveLowerLegEnd(shin, knee);
            var ankleBase = ankle.Transform != null ? ankle.LocalPosition : footBase;
            var ankleStanding = ankleBase + new Vector3(0f, lift * 0.12f, stride * 0.3f * ModelForwardZ);
            var kneeCrouching = knee.LocalPosition + new Vector3(side * 0.035f, 0.17f + Mathf.Abs(scoot) * 0.035f + forwardStride * 0.04f, crouchedKneeForward * ModelForwardZ);
            var ankleCrouching = ankleBase + new Vector3(side * 0.02f, 0.2f, (0.4f + stride * 0.12f + scoot * 0.07f) * ModelForwardZ);

            var kneePosition = Vector3.Lerp(kneeStanding, kneeCrouching, crouchBlend);
            var anklePosition = Vector3.Lerp(ankleStanding, ankleCrouching, crouchBlend);
            if (hip.Transform != null)
            {
                hip.Transform.localPosition = hipPosition;
            }

            knee.Transform.localPosition = kneePosition;
            if (ankle.Transform != null)
            {
                ankle.Transform.localPosition = anklePosition;
            }

            if (foot.Transform != null)
            {
                var footStanding = footBase + new Vector3(0f, lift * 0.1f, stride * 0.34f * ModelForwardZ);
                var footCrouching = footBase + new Vector3(side * 0.02f, 0.24f, (0.46f + stride * 0.16f + scoot * 0.08f) * ModelForwardZ);
                var footPosition = Vector3.Lerp(footStanding, footCrouching, crouchBlend);
                foot.Transform.localPosition = footPosition;
                foot.Transform.localRotation = foot.LocalRotation * Quaternion.Euler(Mathf.Lerp(stride * -9f, -10f, crouchBlend), 0f, side * stride * 3f);
                if (ankle.Transform == null)
                {
                    anklePosition = footPosition + Vector3.forward * LowerLegFootAnchorBack;
                }
            }

            PoseLimbSegmentLocal(thigh, hipPosition, knee.Transform.localPosition, Vector3.forward);
            PoseLimbSegmentLocal(shin, knee.Transform.localPosition, anklePosition, Vector3.forward);
        }

        private static Vector3 ResolveUpperLegAnchor(Part thigh, Part knee)
        {
            if (thigh.Transform == null)
            {
                return knee.LocalPosition + Vector3.up * 0.58f;
            }

            return thigh.LocalPosition + (thigh.LocalPosition - knee.LocalPosition);
        }

        private static Vector3 ResolveLowerLegEnd(Part shin, Part knee)
        {
            if (shin.Transform == null)
            {
                return knee.LocalPosition + Vector3.down * 0.58f;
            }

            return shin.LocalPosition + (shin.LocalPosition - knee.LocalPosition);
        }

        private void PoseArm(Part shoulder, Part upperArm, Part forearm, Part elbow, Part wrist, Part hand, float stride, float side)
        {
            if (elbow.Transform == null)
            {
                return;
            }

            var handTarget = wrist.Transform != null ? wrist : hand;
            if (handTarget.Transform == null)
            {
                return;
            }

            var forwardStride = Mathf.Max(0f, stride);
            var backwardStride = Mathf.Min(0f, stride);
            var shoulderPosition = shoulder.Transform != null ? shoulder.LocalPosition : ResolveUpperArmAnchor(upperArm, elbow, side);
            var handBase = handTarget.LocalPosition;
            var elbowForward = (forwardStride * ArmElbowForwardStride + backwardStride * ArmElbowBackwardStride) * ModelForwardZ;
            var elbowStanding = elbow.LocalPosition + new Vector3(side * 0.012f, 0.014f + forwardStride * 0.01f, elbowForward);
            var wristStanding = handBase + new Vector3(side * 0.012f, 0.012f, stride * ArmHandStride * ModelForwardZ);
            var elbowCrouching = elbow.LocalPosition + new Vector3(side * 0.035f, -0.015f, 0.18f * ModelForwardZ);
            var wristCrouching = handBase + new Vector3(side * 0.035f, 0.02f, 0.28f * ModelForwardZ);

            var elbowPosition = Vector3.Lerp(elbowStanding, elbowCrouching, crouchBlend);
            var wristPosition = Vector3.Lerp(wristStanding, wristCrouching, crouchBlend);
            elbow.Transform.localPosition = elbowPosition;
            handTarget.Transform.localPosition = wristPosition;
            if (hand.Transform != null && hand.Transform != handTarget.Transform)
            {
                hand.Transform.localPosition = wristPosition;
            }

            var armRollReference = Vector3.right * side;
            PoseLimbSegmentLocal(upperArm, shoulderPosition, elbow.Transform.localPosition, armRollReference);
            PoseLimbSegmentLocal(forearm, elbow.Transform.localPosition, wristPosition, armRollReference);
        }

        private static Vector3 ResolveUpperArmAnchor(Part upperArm, Part elbow, float side)
        {
            if (upperArm.Transform == null)
            {
                return elbow.LocalPosition + new Vector3(-side * 0.16f, 0.45f, 0f);
            }

            return upperArm.LocalPosition + (upperArm.LocalPosition - elbow.LocalPosition);
        }

        private void PoseLimbSegmentLocal(Part segment, Vector3 start, Vector3 end, Vector3 rollReference)
        {
            if (segment.Transform == null)
            {
                return;
            }

            var direction = end - start;
            segment.Transform.localPosition = (start + end) * 0.5f;
            if (direction.sqrMagnitude > 0.001f)
            {
                segment.Transform.localRotation = StableAxisRotation(segment.AxisIndex, direction.normalized, rollReference);
                segment.Transform.localScale = segment.ScaleForLength(direction.magnitude);
            }
        }

        private static Quaternion StableAxisRotation(int axisIndex, Vector3 direction, Vector3 rollReference)
        {
            var reference = Vector3.ProjectOnPlane(rollReference, direction);
            if (reference.sqrMagnitude < 0.001f)
            {
                reference = Vector3.ProjectOnPlane(Vector3.up, direction);
            }

            if (reference.sqrMagnitude < 0.001f)
            {
                reference = Vector3.ProjectOnPlane(Vector3.forward, direction);
            }

            if (reference.sqrMagnitude < 0.001f)
            {
                reference = Vector3.ProjectOnPlane(Vector3.right, direction);
            }

            reference.Normalize();

            if (axisIndex == 0)
            {
                var xAxis = direction;
                var yAxis = reference;
                var zAxis = Vector3.Cross(xAxis, yAxis).normalized;
                yAxis = Vector3.Cross(zAxis, xAxis).normalized;
                return Quaternion.LookRotation(zAxis, yAxis);
            }

            if (axisIndex == 1)
            {
                var yAxis = direction;
                var zAxis = reference;
                var xAxis = Vector3.Cross(yAxis, zAxis).normalized;
                zAxis = Vector3.Cross(xAxis, yAxis).normalized;
                return Quaternion.LookRotation(zAxis, yAxis);
            }

            {
                var zAxis = direction;
                var yAxis = reference;
                var xAxis = Vector3.Cross(yAxis, zAxis).normalized;
                yAxis = Vector3.Cross(zAxis, xAxis).normalized;
                return Quaternion.LookRotation(zAxis, yAxis);
            }
        }

        private void ApplyRotation(Part part, float x, float y, float z)
        {
            if (part.Transform == null)
            {
                return;
            }

            part.Transform.localRotation = part.LocalRotation * Quaternion.Euler(x, y, z);
        }

        private void ApplyBlendedRotation(Part part, float standX, float standY, float standZ, float crouchX, float crouchY, float crouchZ)
        {
            if (part.Transform == null)
            {
                return;
            }

            var standing = part.LocalRotation * Quaternion.Euler(standX, standY, standZ);
            var kneeling = part.LocalRotation * Quaternion.Euler(crouchX, crouchY, crouchZ);
            part.Transform.localRotation = Quaternion.Slerp(standing, kneeling, crouchBlend);
        }

        private void ApplyBlendedPosition(Part part, Vector3 standingOffset, Vector3 crouchingOffset)
        {
            if (part.Transform == null)
            {
                return;
            }

            var standing = part.LocalPosition + standingOffset;
            var kneeling = part.LocalPosition + crouchingOffset;
            part.Transform.localPosition = Vector3.Lerp(standing, kneeling, crouchBlend);
        }

        private Part FindTrackedPart(string namePart)
        {
            var transformPart = FindPart(namePart);
            return transformPart != null ? new Part(transformPart) : default;
        }

        private Transform FindPart(string namePart)
        {
            var target = namePart.ToLowerInvariant();
            var children = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                var candidateName = children[i].name.ToLowerInvariant();
                if (children[i] != transform && !candidateName.Contains("hitbox") && candidateName.Contains(target))
                {
                    return children[i];
                }
            }

            return null;
        }

        private static bool HasDescendantNamed(Transform root, string namePart)
        {
            if (root == null)
            {
                return false;
            }

            var target = namePart.ToLowerInvariant();
            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != root && children[i].name.ToLowerInvariant().Contains(target))
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct Part
        {
            private const float MaximumLimbAxisScaleMultiplier = 2.25f;

            public readonly Transform Transform;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly Vector3 LocalScale;
            public readonly Vector3 Axis;
            public readonly float MeshLength;
            public readonly int AxisIndex;

            public Part(Transform transform)
            {
                Transform = transform;
                LocalPosition = transform.localPosition;
                LocalRotation = transform.localRotation;
                LocalScale = transform.localScale;
                Axis = Vector3.up;
                MeshLength = 2f;
                AxisIndex = 1;

                if (transform.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
                {
                    var size = meshFilter.sharedMesh.bounds.size;
                    var scaledSize = new Vector3(
                        Mathf.Abs(size.x * LocalScale.x),
                        Mathf.Abs(size.y * LocalScale.y),
                        Mathf.Abs(size.z * LocalScale.z));

                    if (scaledSize.x >= scaledSize.y && scaledSize.x >= scaledSize.z)
                    {
                        Axis = Vector3.right;
                        MeshLength = Mathf.Max(0.0001f, size.x);
                        AxisIndex = 0;
                    }
                    else if (scaledSize.z >= scaledSize.x && scaledSize.z >= scaledSize.y)
                    {
                        Axis = Vector3.forward;
                        MeshLength = Mathf.Max(0.0001f, size.z);
                        AxisIndex = 2;
                    }
                    else
                    {
                        MeshLength = Mathf.Max(0.0001f, size.y);
                    }
                }
            }

            public Vector3 ScaleForLength(float length)
            {
                var scale = LocalScale;
                var axisScale = length / MeshLength;
                if (AxisIndex == 0)
                {
                    axisScale = ClampAxisScale(axisScale, LocalScale.x);
                    scale.x = axisScale;
                }
                else if (AxisIndex == 2)
                {
                    axisScale = ClampAxisScale(axisScale, LocalScale.z);
                    scale.z = axisScale;
                }
                else
                {
                    axisScale = ClampAxisScale(axisScale, LocalScale.y);
                    scale.y = axisScale;
                }

                return scale;
            }

            private static float ClampAxisScale(float axisScale, float originalAxisScale)
            {
                var maxScale = Mathf.Max(Mathf.Abs(originalAxisScale) * MaximumLimbAxisScaleMultiplier, 0.001f);
                return Mathf.Clamp(axisScale, -maxScale, maxScale);
            }
        }
    }
}
