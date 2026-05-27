using UnityEngine;

namespace ArenaShooter
{
    public sealed class HealthPickup : MonoBehaviour
    {
        [SerializeField] private float healAmount = 35f;

        private MatchController match;
        private Transform floatingModel;
        private Vector3 floatingBaseLocalPosition;
        private float bobPhase;

        public void Configure(MatchController owner, float amount)
        {
            match = owner;
            healAmount = amount;
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

            floatingModel.Rotate(0f, 80f * Time.deltaTime, 0f, Space.Self);
            floatingModel.localPosition = floatingBaseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * 3.4f + bobPhase) * 0.075f);
        }

        private void OnTriggerEnter(Collider other)
        {
            var health = other.GetComponentInParent<CombatantHealth>();
            if (health == null || !health.Heal(healAmount))
            {
                return;
            }

            match?.NotifyHealthPickupTaken(this);
            Destroy(gameObject);
        }
    }
}
