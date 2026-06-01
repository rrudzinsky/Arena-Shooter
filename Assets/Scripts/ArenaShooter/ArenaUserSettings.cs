using UnityEngine;

namespace ArenaShooter
{
    public static class ArenaUserSettings
    {
        public const float MinControllerLookScale = 0.35f;
        public const float MaxControllerLookScale = 2.5f;
        public const float DefaultControllerLookScale = 1f;
        public const float DefaultMusicVolume = 1f;
        public const float DefaultSfxVolume = 1f;

        private const string ControllerLookScaleKey = "ArenaControllerLookScale";
        private const string MusicVolumeKey = "ArenaMusicVolume";
        private const string SfxVolumeKey = "ArenaSfxVolume";

        public static float ControllerLookScale => GetControllerLookScale();
        public static float MusicVolume => GetUnitValue(MusicVolumeKey, DefaultMusicVolume);
        public static float SfxVolume => GetUnitValue(SfxVolumeKey, DefaultSfxVolume);

        public static float GetControllerLookScale()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(ControllerLookScaleKey, DefaultControllerLookScale), MinControllerLookScale, MaxControllerLookScale);
        }

        public static void SetControllerLookScale(float value)
        {
            PlayerPrefs.SetFloat(ControllerLookScaleKey, Mathf.Clamp(value, MinControllerLookScale, MaxControllerLookScale));
            PlayerPrefs.Save();
        }

        public static void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }

        public static void SetSfxVolume(float value)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }

        private static float GetUnitValue(string key, float fallback)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));
        }
    }
}
