using UnityEngine;

namespace ArenaShooter
{
    [System.Serializable]
    public sealed class WeaponDefinition
    {
        public string DisplayName = "Pulse Pistol";
        public float Damage = 28f;
        public float Range = 80f;
        public float Cooldown = 0.28f;
        public int Ammo = 36;

        public WeaponDefinition Clone()
        {
            return new WeaponDefinition
            {
                DisplayName = DisplayName,
                Damage = Damage,
                Range = Range,
                Cooldown = Cooldown,
                Ammo = Ammo
            };
        }
    }
}
