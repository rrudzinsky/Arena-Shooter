using UnityEngine;

namespace ArenaShooter
{
    public static class AllOutWarArmyVisuals
    {
        public static Color GetAccent(int army)
        {
            return army switch
            {
                0 => new Color(0.05f, 0.64f, 1f, 0.74f),
                1 => new Color(1f, 0.22f, 0.34f, 0.7f),
                2 => new Color(1f, 0.72f, 0.12f, 0.68f),
                3 => new Color(0.6f, 0.28f, 1f, 0.68f),
                4 => new Color(0.08f, 0.92f, 0.52f, 0.66f),
                5 => new Color(1f, 0.42f, 0.1f, 0.68f),
                6 => new Color(0.92f, 0.2f, 0.9f, 0.66f),
                _ => new Color(0.7f, 0.88f, 1f, 0.62f)
            };
        }
    }
}
