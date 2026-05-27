using UnityEngine;

namespace ArenaShooter
{
    public sealed class GateDoor : MonoBehaviour
    {
        private Transform door;
        private Vector3 openLocalPosition;
        private Vector3 closedLocalPosition;
        private bool closing;
        private bool opening;
        private float closeAt = -1f;

        public void Configure(Transform doorTransform, Vector3 openPosition, Vector3 closedPosition)
        {
            door = doorTransform;
            openLocalPosition = openPosition;
            closedLocalPosition = closedPosition;
            door.localPosition = closedLocalPosition;
            SetDoorCollidersEnabled(true);
        }

        private void Update()
        {
            if (door == null)
            {
                return;
            }

            if (opening)
            {
                door.localPosition = Vector3.MoveTowards(door.localPosition, openLocalPosition, Time.deltaTime * 7f);
            }

            if (closeAt > 0f && Time.time >= closeAt)
            {
                opening = false;
                closing = true;
                closeAt = -1f;
                SetDoorCollidersEnabled(true);
            }

            if (closing)
            {
                door.localPosition = Vector3.MoveTowards(door.localPosition, closedLocalPosition, Time.deltaTime * 7f);
            }
        }

        public void OpenThenClose(float secondsOpen)
        {
            opening = true;
            closing = false;
            closeAt = Time.time + secondsOpen;
            SetDoorCollidersEnabled(false);
        }

        public void OpenPermanently()
        {
            if (door == null)
            {
                return;
            }

            opening = false;
            closing = false;
            closeAt = -1f;
            door.localPosition = openLocalPosition;
            SetDoorCollidersEnabled(false);
        }

        private void SetDoorCollidersEnabled(bool enabled)
        {
            if (door == null)
            {
                return;
            }

            var colliders = door.GetComponentsInChildren<Collider>(true);
            foreach (var collider in colliders)
            {
                collider.enabled = enabled;
            }
        }
    }
}
