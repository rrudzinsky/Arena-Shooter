using UnityEngine;

namespace ArenaShooter
{
    public static class WeaponCatalog
    {
        public static WeaponDefinition CreatePulsePistol()
        {
            return new WeaponDefinition();
        }

        public static WeaponDefinition CreateScatterShotgun()
        {
            return new WeaponDefinition
            {
                DisplayName = "Scatter Shotgun",
                Damage = 9f,
                Range = 34f,
                Cooldown = 0.82f,
                Ammo = 14,
                FireStyle = WeaponFireStyle.Scatter,
                ModelKind = WeaponModelKind.ScatterShotgun,
                PelletCount = 8,
                SpreadDegrees = 7.5f
            };
        }

        public static WeaponDefinition CreatePlasmaGrenade()
        {
            return new WeaponDefinition
            {
                DisplayName = "Plasma Grenade",
                Damage = 88f,
                Range = 30f,
                Cooldown = 1.15f,
                Ammo = 4,
                FireStyle = WeaponFireStyle.Thrown,
                ModelKind = WeaponModelKind.PlasmaGrenade,
                ExplosionRadius = 5.2f,
                ThrowSpeed = 16.5f
            };
        }

        public static WeaponDefinition CreateRandomDrop()
        {
            var roll = Random.value;
            if (roll < 0.34f)
            {
                return CreatePulsePistol();
            }

            if (roll < 0.67f)
            {
                return CreateScatterShotgun();
            }

            return CreatePlasmaGrenade();
        }
    }
}
