using UnityEngine;

namespace ArenaShooter
{
    public sealed class CombatantTeam : MonoBehaviour
    {
        public int TeamId { get; private set; } = -1;

        public void Configure(int teamId)
        {
            TeamId = Mathf.Max(0, teamId);
        }

        public static bool TryGetTeam(CombatantHealth health, out int teamId)
        {
            teamId = -1;
            return health != null &&
                   health.TryGetComponent<CombatantTeam>(out var team) &&
                   team.TeamId >= 0 &&
                   (teamId = team.TeamId) >= 0;
        }

        public static bool AreEnemies(CombatantHealth a, CombatantHealth b)
        {
            if (a == null || b == null || a == b)
            {
                return false;
            }

            if (!TryGetTeam(a, out var aTeam) || !TryGetTeam(b, out var bTeam))
            {
                return true;
            }

            return aTeam != bTeam;
        }
    }
}
