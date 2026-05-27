using UnityEngine;

namespace ArenaShooter
{
    public sealed class AmmoPickup : MonoBehaviour
    {
        [SerializeField] private int ammoAmount = 18;

        private MatchController match;
        private Transform floatingModel;
        private Vector3 floatingBaseLocalPosition;
        private float bobPhase;
        public int AmmoAmount => ammoAmount;

        public void Configure(int amount)
        {
            Configure(null, amount);
        }

        public void Configure(MatchController owner, int amount)
        {
            match = owner;
            ammoAmount = amount;
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

            floatingModel.Rotate(0f, 70f * Time.deltaTime, 0f, Space.Self);
            floatingModel.localPosition = floatingBaseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * 3f + bobPhase) * 0.07f);
        }

        private void OnTriggerEnter(Collider other)
        {
            var inventory = other.GetComponentInParent<WeaponInventory>();
            var health = other.GetComponentInParent<CombatantHealth>();
            if (match != null && match.TryCollectAllOutWarSquadAmmoPickup(health, this))
            {
                Destroy(gameObject);
                return;
            }

            if (inventory == null || !inventory.TryAddAmmo(ammoAmount))
            {
                return;
            }

            match?.NotifyAmmoPickupTaken(this);
            Destroy(gameObject);
        }
    }
}
