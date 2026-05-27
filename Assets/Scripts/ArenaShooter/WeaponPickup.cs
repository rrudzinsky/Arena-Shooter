using UnityEngine;

namespace ArenaShooter
{
    public sealed class WeaponPickup : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition weapon = new();

        private MatchController match;
        private Transform floatingModel;
        private Vector3 floatingBaseLocalPosition;
        private float bobPhase;

        public void Configure(MatchController owner, WeaponDefinition weaponDefinition)
        {
            match = owner;
            weapon = weaponDefinition.Clone();
            floatingModel = transform.Find("Floating Pickup Model");
            if (floatingModel != null)
            {
                floatingBaseLocalPosition = floatingModel.localPosition;
            }
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (floatingModel == null)
            {
                return;
            }

            floatingModel.Rotate(0f, 95f * Time.deltaTime, 0f, Space.Self);
            floatingModel.localPosition = floatingBaseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * 3f + bobPhase) * 0.08f);
        }

        private void OnTriggerEnter(Collider other)
        {
            var inventory = other.GetComponentInParent<WeaponInventory>();
            if (inventory == null)
            {
                return;
            }

            inventory.Equip(weapon);
            match?.NotifyPickupTaken(this);
            Destroy(gameObject);
        }
    }
}
