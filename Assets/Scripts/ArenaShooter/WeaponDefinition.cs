using UnityEngine;

namespace ArenaShooter
{
    public enum WeaponFireStyle
    {
        Hitscan,
        Scatter,
        Thrown
    }

    public enum WeaponModelKind
    {
        PulsePistol,
        ScatterShotgun,
        PlasmaGrenade
    }

    [System.Serializable]
    public sealed class WeaponDefinition
    {
        public string DisplayName = "Pulse Pistol";
        public float Damage = 28f;
        public float Range = 80f;
        public float Cooldown = 0.28f;
        public int Ammo = 36;
        public WeaponFireStyle FireStyle = WeaponFireStyle.Hitscan;
        public WeaponModelKind ModelKind = WeaponModelKind.PulsePistol;
        public int PelletCount = 1;
        public float SpreadDegrees = 0f;
        public float ExplosionRadius = 0f;
        public float ThrowSpeed = 0f;

        public WeaponDefinition Clone()
        {
            return new WeaponDefinition
            {
                DisplayName = DisplayName,
                Damage = Damage,
                Range = Range,
                Cooldown = Cooldown,
                Ammo = Ammo,
                FireStyle = FireStyle,
                ModelKind = ModelKind,
                PelletCount = PelletCount,
                SpreadDegrees = SpreadDegrees,
                ExplosionRadius = ExplosionRadius,
                ThrowSpeed = ThrowSpeed
            };
        }
    }
}
