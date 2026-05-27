using UnityEngine;

namespace ArenaShooter
{
    public sealed class DroidCrouchPose : MonoBehaviour
    {
        private const float StandingHeight = 1.9f;
        private const float CrouchingHeight = 0.84f;

        private CharacterController controller;
        private CombatantVisualWalkAnimator walkAnimator;
        private DroidBlasterRig blasterRig;
        private Transform visualRoot;
        private Transform hitboxRoot;
        private Vector3 visualRootBaseScale = Vector3.one;
        private Vector3 hitboxRootBasePosition;
        private Vector3 hitboxRootBaseScale = Vector3.one;

        public void Apply(bool crouching, float transitionSpeed)
        {
            CacheComponents();

            walkAnimator?.SetCrouching(crouching);
            blasterRig?.SetCrouching(crouching);

            if (controller != null)
            {
                var targetHeight = crouching ? CrouchingHeight : StandingHeight;
                controller.height = Mathf.MoveTowards(controller.height, targetHeight, Time.deltaTime * transitionSpeed);
                controller.center = new Vector3(0f, -(StandingHeight - controller.height) * 0.5f, 0f);
            }

            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, visualRootBaseScale, Time.deltaTime * 8f);
            }

            if (hitboxRoot == null)
            {
                return;
            }

            var hitboxScale = crouching
                ? new Vector3(hitboxRootBaseScale.x * 1.1f, hitboxRootBaseScale.y * 0.5f, hitboxRootBaseScale.z * 1.24f)
                : hitboxRootBaseScale;
            var hitboxPosition = crouching ? hitboxRootBasePosition + new Vector3(0f, -0.3f, -0.09f) : hitboxRootBasePosition;
            hitboxRoot.localScale = Vector3.Lerp(hitboxRoot.localScale, hitboxScale, Time.deltaTime * 8f);
            hitboxRoot.localPosition = Vector3.Lerp(hitboxRoot.localPosition, hitboxPosition, Time.deltaTime * 8f);
        }

        private void CacheComponents()
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            if (walkAnimator == null)
            {
                walkAnimator = GetComponent<CombatantVisualWalkAnimator>();
            }

            if (blasterRig == null)
            {
                blasterRig = GetComponent<DroidBlasterRig>();
            }

            if (visualRoot == null)
            {
                visualRoot = transform.Find("Droid Combat Frame Model");
                if (visualRoot != null)
                {
                    visualRootBaseScale = visualRoot.localScale;
                }
            }

            if (hitboxRoot == null)
            {
                hitboxRoot = transform.Find("Droid Hitbox Rig");
                if (hitboxRoot != null)
                {
                    hitboxRootBasePosition = hitboxRoot.localPosition;
                    hitboxRootBaseScale = hitboxRoot.localScale;
                }
            }
        }
    }
}
