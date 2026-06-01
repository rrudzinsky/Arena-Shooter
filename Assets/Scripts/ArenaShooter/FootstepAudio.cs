using UnityEngine;

namespace ArenaShooter
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FootstepAudio : MonoBehaviour
    {
        private CharacterController controller;
        private Camera lodCamera;
        private Vector3 lastPosition;
        private float distanceSinceStep;
        private float nextStepAt;
        private float volume = 0.09f;
        private float range = 5f;
        private float strideScale = 1f;
        private bool spatial;
        private bool playerFootsteps;
        private bool leftFoot;

        public void Configure(bool isPlayer)
        {
            playerFootsteps = isPlayer;
            if (isPlayer)
            {
                volume = 0.085f;
                range = 4.8f;
                strideScale = 1.65f;
                spatial = false;
            }
            else
            {
                volume = 0.105f;
                range = 8f;
                strideScale = 1.75f;
                spatial = true;
            }
        }

        public void ConfigureLod(Camera camera)
        {
            lodCamera = camera;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (ArenaAudio.Instance == null || controller == null)
            {
                lastPosition = transform.position;
                return;
            }

            if (!playerFootsteps && ShouldSuppressRemoteFootsteps())
            {
                lastPosition = transform.position;
                distanceSinceStep = 0f;
                return;
            }

            var delta = transform.position - lastPosition;
            delta.y = 0f;
            var frameDistance = delta.magnitude;
            var speed = frameDistance / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPosition = transform.position;

            if (!controller.isGrounded || speed < 0.75f || controller.velocity.sqrMagnitude < 0.35f)
            {
                distanceSinceStep = 0f;
                nextStepAt = Time.time + 0.08f;
                return;
            }

            distanceSinceStep += frameDistance;
            var strideLength = GetStrideLength(speed) * strideScale;
            if (distanceSinceStep < strideLength || Time.time < nextStepAt)
            {
                return;
            }

            distanceSinceStep = 0f;
            nextStepAt = Time.time + GetMinimumStepInterval(speed);
            leftFoot = !leftFoot;
            var footOffset = transform.right * (leftFoot ? -0.18f : 0.18f);
            var stepPosition = transform.position + footOffset;
            ArenaAudio.Instance.PlayFootstep(stepPosition, volume, range, spatial);
            if (playerFootsteps)
            {
                ArenaNoise.EmitPlayerNoise(stepPosition, speed > 6.8f ? 13f : 8f);
            }
        }

        private float GetStrideLength(float speed)
        {
            if (controller.height < 1.25f)
            {
                return 0.66f;
            }

            if (speed > 6.8f)
            {
                return 0.78f;
            }

            return Mathf.Lerp(0.92f, 0.72f, Mathf.Clamp01((speed - 1.2f) / 4.6f));
        }

        private float GetMinimumStepInterval(float speed)
        {
            if (speed > 6.8f)
            {
                return spatial ? 0.31f : 0.34f;
            }

            if (speed > 3.5f)
            {
                return spatial ? 0.38f : 0.42f;
            }

            return spatial ? 0.52f : 0.58f;
        }

        private bool ShouldSuppressRemoteFootsteps()
        {
            if (lodCamera == null)
            {
                return false;
            }

            var toCamera = transform.position - lodCamera.transform.position;
            if (toCamera.sqrMagnitude <= 45f * 45f)
            {
                return false;
            }

            var viewport = lodCamera.WorldToViewportPoint(transform.position + Vector3.up * 0.6f);
            return viewport.z <= 0f ||
                viewport.x < -0.15f ||
                viewport.x > 1.15f ||
                viewport.y < -0.15f ||
                viewport.y > 1.15f ||
                toCamera.sqrMagnitude > 78f * 78f;
        }
    }
}
