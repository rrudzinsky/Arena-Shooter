using UnityEngine;

namespace ArenaShooter
{
    internal static class TacticalSurvivalModel
    {
        public static float CalculateHealRunScore(
            CombatantHealth health,
            Vector3 currentPosition,
            Vector3 destination,
            bool targetIsPickup,
            int nearbyThreats,
            int routeThreats,
            int nearbySupport,
            float damagePressure,
            bool underFire,
            bool canWaitForShield,
            bool hasRecentThreat,
            Vector3 recentThreatPosition,
            Vector3 recentThreatDirection,
            out bool shouldWaitForShield)
        {
            shouldWaitForShield = false;
            if (health == null || health.MaxHealth <= 0f)
            {
                return 0f;
            }

            var score = (health.CurrentHealth / health.MaxHealth) * 100f;
            var shieldRatio = health.MaxShield > 0f ? health.CurrentShield / health.MaxShield : 0f;
            if (shieldRatio > 0.05f)
            {
                score += Mathf.Lerp(3f, 22f, Mathf.Clamp01(shieldRatio));
            }
            else if (canWaitForShield)
            {
                shouldWaitForShield = true;
                score += 12f;
            }

            score += Mathf.Min(24f, nearbySupport * 8f);
            score -= nearbyThreats * 9f;
            score -= routeThreats * 13f;

            var distance = FlatDistance(currentPosition, destination);
            score += targetIsPickup ? 8f : 4f;
            score -= Mathf.Min(28f, distance * 1.15f);

            if (underFire)
            {
                score -= 10f;
            }

            if (damagePressure >= 2.5f)
            {
                score -= 10f;
            }

            if (hasRecentThreat)
            {
                var toHeal = Flatten(destination - currentPosition);
                if (toHeal.sqrMagnitude > 0.01f)
                {
                    var incomingAlignment = Vector3.Dot(toHeal.normalized, recentThreatDirection);
                    score -= Mathf.Max(0f, incomingAlignment) * 14f;
                }

                if (FlatDistance(recentThreatPosition, currentPosition) < 8f)
                {
                    score -= 8f;
                }
            }

            return Mathf.Clamp(score, 0f, 100f);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Flatten(a - b).magnitude;
        }
    }
}
