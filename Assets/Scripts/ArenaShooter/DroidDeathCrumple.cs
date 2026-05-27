using UnityEngine;

namespace ArenaShooter
{
    public sealed class DroidDeathCrumple : MonoBehaviour
    {
        private Transform visualRoot;
        private Transform blasterRoot;
        private Transform blasterArmRoot;
        private Vector3 rootStartPosition;
        private Quaternion rootStartRotation;
        private Quaternion rootEndRotation;
        private Vector3 rootEndPosition;
        private float startedAt;
        private bool initialized;

        public void Begin()
        {
            initialized = false;
            startedAt = Time.time;
            CacheParts();
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                CacheParts();
            }

            var t = Mathf.Clamp01((Time.time - startedAt) / 0.85f);
            var eased = 1f - Mathf.Pow(1f - t, 3f);

            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.Lerp(rootStartPosition, rootEndPosition, eased);
                visualRoot.localRotation = Quaternion.Slerp(rootStartRotation, rootEndRotation, eased);
            }
        }

        private void CacheParts()
        {
            visualRoot = FindPart("droid combat frame model");
            blasterRoot = FindPart("droid aimed blaster");
            blasterArmRoot = FindPart("droid attached blaster arm");
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            ReparentToVisualRoot(blasterRoot);
            ReparentToVisualRoot(blasterArmRoot);

            rootStartPosition = visualRoot.localPosition;
            rootStartRotation = visualRoot.localRotation;
            var fallSide = Random.value < 0.5f ? -1f : 1f;
            rootEndPosition = rootStartPosition + new Vector3(0.22f * fallSide, -0.46f, 0.2f);
            rootEndRotation = rootStartRotation * Quaternion.Euler(74f, 0f, 82f * fallSide);

            initialized = true;
        }

        private void ReparentToVisualRoot(Transform part)
        {
            if (part != null && part.parent != visualRoot && !visualRoot.IsChildOf(part))
            {
                part.SetParent(visualRoot, true);
            }
        }

        private Transform FindPart(string namePart)
        {
            var target = namePart.ToLowerInvariant();
            var children = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != transform && children[i].name.ToLowerInvariant().Contains(target))
                {
                    return children[i];
                }
            }

            return null;
        }
    }
}
