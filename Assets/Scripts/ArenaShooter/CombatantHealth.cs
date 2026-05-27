using System;
using UnityEngine;

namespace ArenaShooter
{
    public sealed class CombatantHealth : MonoBehaviour
    {
        [SerializeField] private string displayName = "Combatant";
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float shieldRechargeDelay = 5.5f;
        [SerializeField] private float shieldRechargePerSecond = 18f;

        public event Action<CombatantHealth> Died;
        public event Action<CombatantHealth> Damaged;

        public string DisplayName => displayName;
        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float MaxShield { get; private set; }
        public float CurrentShield { get; private set; }
        public CombatantHealth LastAttacker { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;
        private float lastDamageAt = float.NegativeInfinity;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        private void Update()
        {
            if (!IsAlive || MaxShield <= 0f || CurrentShield >= MaxShield || Time.time < lastDamageAt + shieldRechargeDelay)
            {
                return;
            }

            CurrentShield = Mathf.Min(MaxShield, CurrentShield + shieldRechargePerSecond * Time.deltaTime);
        }

        public void Configure(string actorName, float health)
        {
            displayName = actorName;
            maxHealth = Mathf.Max(1f, health);
            CurrentHealth = maxHealth;
            MaxShield = 0f;
            CurrentShield = 0f;
        }

        public void ConfigureShieldRecharge(float delay, float rechargePerSecond)
        {
            shieldRechargeDelay = Mathf.Max(0.25f, delay);
            shieldRechargePerSecond = Mathf.Max(1f, rechargePerSecond);
        }

        public void IncreaseMaxHealth(float amount, bool healAddedHealth)
        {
            if (amount <= 0f)
            {
                return;
            }

            maxHealth += amount;
            if (healAddedHealth)
            {
                CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            }
        }

        public void TakeDamage(float amount, CombatantHealth attacker)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            LastAttacker = attacker;
            if (CurrentShield > 0f)
            {
                var absorbed = Mathf.Min(CurrentShield, amount);
                CurrentShield -= absorbed;
                amount -= absorbed;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            lastDamageAt = Time.time;
            Damaged?.Invoke(this);

            if (CurrentHealth <= 0f)
            {
                Died?.Invoke(this);
            }
        }

        public bool Heal(float amount)
        {
            if (!IsAlive || amount <= 0f || CurrentHealth >= maxHealth)
            {
                return false;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            return true;
        }

        public bool AddShield(float amount, float maxShield)
        {
            if (!IsAlive || amount <= 0f || maxShield <= 0f)
            {
                return false;
            }

            MaxShield = Mathf.Max(MaxShield, maxShield);
            var previous = CurrentShield;
            CurrentShield = Mathf.Min(MaxShield, CurrentShield + amount);
            return CurrentShield > previous;
        }
    }
}
