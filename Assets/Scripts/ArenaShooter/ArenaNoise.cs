using System;
using UnityEngine;

namespace ArenaShooter
{
    public static class ArenaNoise
    {
        public static event Action<Vector3, float> PlayerNoise;
        public static event Action<Vector3, Vector3, CombatantHealth, float> Gunfire;

        public static void EmitPlayerNoise(Vector3 position, float radius)
        {
            PlayerNoise?.Invoke(position, Mathf.Max(0f, radius));
        }

        public static void EmitGunfire(Vector3 origin, Vector3 direction, CombatantHealth shooter, float radius)
        {
            Gunfire?.Invoke(origin, direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward, shooter, Mathf.Max(0f, radius));
        }
    }
}
