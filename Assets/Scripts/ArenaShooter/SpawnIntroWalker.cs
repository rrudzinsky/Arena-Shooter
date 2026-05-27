using UnityEngine;

namespace ArenaShooter
{
    public sealed class SpawnIntroWalker : MonoBehaviour
    {
        private CharacterController controller;
        private Transform cameraTransform;
        private Vector3 cameraBaseLocalPosition;
        private Vector3 destination;
        private Vector3 fallbackDestination;
        private Vector3 startPosition;
        private float speed;
        private float currentSpeed;
        private float stopDistance;
        private float combatReleaseDistance;
        private float startedAt;
        private bool active;
        private bool holding;
        private float holdStartedAt;

        public bool IsWalking => active || holding;
        public bool AllowsCombat { get; private set; }

        public void Hold()
        {
            CacheReferences();
            holding = true;
            active = false;
            AllowsCombat = false;
            holdStartedAt = Time.time;
        }

        public void Begin(Vector3 targetPosition, Vector3 fallbackTargetPosition, float walkSpeed)
        {
            Begin(targetPosition, fallbackTargetPosition, walkSpeed, 1.6f);
        }

        public void Begin(Vector3 targetPosition, Vector3 fallbackTargetPosition, float walkSpeed, float releaseCombatAfterDistance)
        {
            CacheReferences();
            startPosition = transform.position;
            destination = targetPosition;
            fallbackDestination = fallbackTargetPosition;
            speed = walkSpeed;
            currentSpeed = 0f;
            stopDistance = 0.45f;
            combatReleaseDistance = Mathf.Max(0f, releaseCombatAfterDistance);
            startedAt = Time.time;
            holding = false;
            active = true;
            AllowsCombat = combatReleaseDistance <= 0f;
        }

        private void Update()
        {
            if (holding && Time.time - holdStartedAt > 7f)
            {
                holding = false;
                AllowsCombat = true;
                ResetCameraBob();
                return;
            }

            if (!active || controller == null)
            {
                return;
            }

            var toDestination = destination - transform.position;
            toDestination.y = 0f;

            if (toDestination.magnitude <= stopDistance)
            {
                active = false;
                AllowsCombat = true;
                ResetCameraBob();
                return;
            }

            if (Time.time - startedAt > 5.5f)
            {
                Debug.LogWarning($"[Arena Shooter] Spawn intro timed out for {name}; moving actor to fallback entry point.");
                transform.position = fallbackDestination;
                active = false;
                AllowsCombat = true;
                ResetCameraBob();
                return;
            }

            var direction = toDestination.normalized;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), Time.deltaTime * 420f);
            currentSpeed = Mathf.MoveTowards(currentSpeed, speed, Time.deltaTime * 3.8f);
            var nextPosition = Vector3.MoveTowards(transform.position, destination, currentSpeed * Time.deltaTime);
            nextPosition.y = transform.position.y;
            transform.position = nextPosition;
            var traveled = transform.position - startPosition;
            traveled.y = 0f;
            if (traveled.magnitude >= combatReleaseDistance)
            {
                AllowsCombat = true;
            }

            UpdateCameraBob();
        }

        private void CacheReferences()
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            if (cameraTransform == null)
            {
                var camera = GetComponentInChildren<Camera>();
                if (camera != null)
                {
                    cameraTransform = camera.transform;
                    cameraBaseLocalPosition = cameraTransform.localPosition;
                }
            }
        }

        private void UpdateCameraBob()
        {
            if (cameraTransform == null)
            {
                return;
            }

            var step = Time.time * 7.2f;
            var bob = Mathf.Abs(Mathf.Sin(step)) * 0.035f;
            var sway = Mathf.Sin(step * 0.5f) * 0.018f;
            cameraTransform.localPosition = cameraBaseLocalPosition + new Vector3(sway, bob, 0f);
        }

        private void ResetCameraBob()
        {
            if (cameraTransform != null)
            {
                cameraTransform.localPosition = cameraBaseLocalPosition;
            }
        }
    }
}
