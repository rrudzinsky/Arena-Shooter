using UnityEngine;

namespace ArenaShooter
{
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraFraming : MonoBehaviour
    {
        [SerializeField] private float referenceVerticalFov = 62f;
        [SerializeField] private float referenceAspect = 1280f / 536f;

        private Camera gameplayCamera;

        private void Awake()
        {
            gameplayCamera = GetComponent<Camera>();
            ApplyReferenceFov(referenceVerticalFov);
        }

        private void LateUpdate()
        {
            ApplyReferenceFov(referenceVerticalFov);
        }

        public void SetReferenceVerticalFov(float verticalFov)
        {
            referenceVerticalFov = verticalFov;
            ApplyReferenceFov(referenceVerticalFov);
        }

        private void ApplyReferenceFov(float verticalFov)
        {
            if (gameplayCamera == null)
            {
                return;
            }

            gameplayCamera.enabled = true;
            gameplayCamera.fieldOfView = ConvertReferenceVerticalFov(verticalFov);
        }

        private float ConvertReferenceVerticalFov(float verticalFov)
        {
            var actualAspect = gameplayCamera != null && gameplayCamera.aspect > 0f
                ? gameplayCamera.aspect
                : Screen.width / Mathf.Max(1f, (float)Screen.height);
            var referenceVerticalRadians = verticalFov * Mathf.Deg2Rad;
            var referenceHorizontalRadians = 2f * Mathf.Atan(Mathf.Tan(referenceVerticalRadians * 0.5f) * referenceAspect);
            var adjustedVerticalRadians = 2f * Mathf.Atan(Mathf.Tan(referenceHorizontalRadians * 0.5f) / Mathf.Max(0.01f, actualAspect));
            return Mathf.Clamp(adjustedVerticalRadians * Mathf.Rad2Deg, 45f, 100f);
        }
    }
}
