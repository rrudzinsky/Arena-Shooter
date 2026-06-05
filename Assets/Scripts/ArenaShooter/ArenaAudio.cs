using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArenaShooter
{
    public sealed class ArenaAudio : MonoBehaviour
    {
        private const string SfxPath = "Audio/SFX/";
        private const float CrowdLoopVolume = 0.04f;
        private const float GameplayMusicTargetVolume = 0.42f;
        private const float GameplayMusicFadeDuration = 7f;

        private AudioClip footstepClip;
        private AudioClip smallerGunshotClip;
        private AudioClip largerGunshotClip;
        private AudioClip[] startFightCrowdClips;
        private AudioClip[] firstBloodCrowdClips;
        private AudioClip[] playerHitClips;
        private AudioClip crowdLoopClip;
        private AudioSource crowdSource;
        private AudioSource cheerSource;
        private AudioSource musicSource;
        private bool gateOpenCrowdPlayed;
        private bool firstShotCrowdPlayed;
        private Coroutine gateCrowdSwell;
        private Coroutine musicFade;
        private int lastMusicIndex = -1;
        private float currentCheerRawVolume = 0.65f;

        public static ArenaAudio Instance { get; private set; }

        public bool IsGameplayMusicPlaying => musicSource != null && musicSource.isPlaying;

        private void Awake()
        {
            Instance = this;
            footstepClip = LoadClip("Footsteps") ?? CreateNoiseBurst("Footstep", 0.09f, 0.26f, 0.05f);
            smallerGunshotClip = LoadClip("Smaller_Pistol_Gunshot") ?? CreateGunshot("Small Pulse Gunshot", 0.15f, 185f);
            largerGunshotClip = LoadClip("Larger_Pistol_Gunshot") ?? CreateGunshot("Large Pulse Gunshot", 0.22f, 118f);
            startFightCrowdClips = LoadClipSet("Crowd_Cheering_Start_of_Fight", 4);
            firstBloodCrowdClips = LoadClipSet("Crowd_Cheering_First_Blood", 4);
            playerHitClips = new[] { LoadClip("Male_Player_Getting_Hit_1"), LoadClip("Male_Player_Getting_Hit_2") };
            crowdLoopClip = CreateCrowdCheer("Crowd Murmur", 4.5f, 0.08f);

            crowdSource = gameObject.AddComponent<AudioSource>();
            crowdSource.clip = crowdLoopClip;
            crowdSource.loop = true;
            crowdSource.spatialBlend = 0f;
            crowdSource.volume = ScaleSfxVolume(CrowdLoopVolume);
            crowdSource.Play();

            cheerSource = gameObject.AddComponent<AudioSource>();
            cheerSource.spatialBlend = 0f;
            cheerSource.volume = ScaleSfxVolume(0.65f);

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = false;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0f;
            PlayRandomMusicTrack();
        }

        private void Update()
        {
            if (musicSource == null || musicSource.isPlaying || musicFade != null)
            {
                return;
            }

            PlayRandomMusicTrack();
        }

        public void PlayFootstep(Vector3 position, float volume, float range, bool spatial)
        {
            if (spatial)
            {
                PlaySpatial(footstepClip, position, volume, Random.Range(0.92f, 1.06f), range);
                return;
            }

            PlayNonSpatial(footstepClip, volume, Random.Range(0.95f, 1.04f));
        }

        public void PlayGunshot(Vector3 position)
        {
            var clip = Random.value < 0.68f ? smallerGunshotClip : largerGunshotClip;
            PlaySpatial(clip, position, 0.9f, Random.Range(0.96f, 1.04f), 42f);

            if (!firstShotCrowdPlayed)
            {
                firstShotCrowdPlayed = true;
                PlayCrowdExcited();
            }
        }

        public void PlayGateOpenCrowd()
        {
            if (cheerSource == null || gateOpenCrowdPlayed)
            {
                return;
            }

            gateOpenCrowdPlayed = true;
            PlayCrowdClip(PickClip(startFightCrowdClips), 0.78f, 1f);
        }

        public void BeginGateCrowdSwell(float duration)
        {
            if (cheerSource == null || gateOpenCrowdPlayed)
            {
                return;
            }

            if (gateCrowdSwell != null)
            {
                StopCoroutine(gateCrowdSwell);
            }

            gateCrowdSwell = StartCoroutine(GateCrowdSwell(duration));
        }

        public void PlayPlayerHit(Vector3 position)
        {
            PlaySpatial(PickClip(playerHitClips), position, 0.72f, Random.Range(0.96f, 1.04f), 18f);
        }

        public void ApplySavedVolumes()
        {
            if (crowdSource != null)
            {
                crowdSource.volume = ScaleSfxVolume(CrowdLoopVolume);
            }

            if (musicSource != null && musicFade == null)
            {
                musicSource.volume = ScaleMusicVolume(GameplayMusicTargetVolume);
            }

            if (cheerSource != null)
            {
                cheerSource.volume = ScaleSfxVolume(currentCheerRawVolume);
            }
        }

        public bool TryGetGameplayMusicSpectrum(float[] samples)
        {
            if (samples == null || samples.Length == 0 || musicSource == null || !musicSource.isPlaying)
            {
                return false;
            }

            musicSource.GetSpectrumData(samples, 0, FFTWindow.BlackmanHarris);
            for (var i = 0; i < samples.Length; i++)
            {
                if (samples[i] > 0.00001f)
                {
                    return true;
                }
            }

            return false;
        }

        private void PlayCrowdExcited()
        {
            if (cheerSource == null)
            {
                return;
            }

            PlayCrowdClip(PickClip(firstBloodCrowdClips), 0.88f, 1f);
        }

        private void PlayCrowdClip(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || cheerSource == null)
            {
                return;
            }

            cheerSource.Stop();
            cheerSource.clip = clip;
            currentCheerRawVolume = volume;
            cheerSource.volume = ScaleSfxVolume(volume);
            cheerSource.pitch = pitch;
            cheerSource.Play();
        }

        private void PlaySpatial(AudioClip clip, Vector3 position, float volume, float pitch, float range)
        {
            if (clip == null)
            {
                return;
            }

            var sound = new GameObject($"Audio {clip.name}");
            sound.transform.position = position;
            var source = sound.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = ScaleSfxVolume(volume);
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.minDistance = 2f;
            source.maxDistance = range;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Destroy(sound, Mathf.Min(clip.length / Mathf.Max(0.1f, pitch), 0.38f));
        }

        private void PlayNonSpatial(AudioClip clip, float volume, float pitch)
        {
            if (clip == null)
            {
                return;
            }

            var sound = new GameObject($"Audio {clip.name}");
            var source = sound.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = ScaleSfxVolume(volume);
            source.pitch = pitch;
            source.spatialBlend = 0f;
            source.Play();
            Destroy(sound, Mathf.Min(clip.length / Mathf.Max(0.1f, pitch), 0.32f));
        }

        private System.Collections.IEnumerator GateCrowdSwell(float duration)
        {
            var clip = PickClip(startFightCrowdClips);
            if (clip == null)
            {
                yield break;
            }

            gateOpenCrowdPlayed = true;
            cheerSource.Stop();
            cheerSource.clip = clip;
            cheerSource.pitch = 1f;
            currentCheerRawVolume = 0.12f;
            cheerSource.volume = ScaleSfxVolume(currentCheerRawVolume);
            cheerSource.Play();

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                currentCheerRawVolume = Mathf.Lerp(0.12f, 0.82f, t * t);
                cheerSource.volume = ScaleSfxVolume(currentCheerRawVolume);
                yield return null;
            }

            currentCheerRawVolume = 0.82f;
            cheerSource.volume = ScaleSfxVolume(currentCheerRawVolume);
        }

        private void PlayRandomMusicTrack()
        {
            var clip = LoadRandomMusicTrack(out var selectedIndex);
            if (clip == null)
            {
                return;
            }

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.time = 0f;
            musicSource.volume = 0f;
            musicSource.Play();
            lastMusicIndex = selectedIndex;

            if (musicFade != null)
            {
                StopCoroutine(musicFade);
            }

            musicFade = StartCoroutine(FadeInGameplayMusic());
        }

        private AudioClip LoadRandomMusicTrack(out int selectedIndex)
        {
            selectedIndex = -1;
            var paths = ArenaMusicLibrary.TrackResourcePaths;
            if (paths == null || paths.Length == 0)
            {
                return null;
            }

            var startIndex = Random.Range(0, paths.Length);
            if (paths.Length > 1 && startIndex == lastMusicIndex)
            {
                startIndex = (startIndex + 1) % paths.Length;
            }

            for (var offset = 0; offset < paths.Length; offset++)
            {
                var index = (startIndex + offset) % paths.Length;
                if (paths.Length > 1 && index == lastMusicIndex)
                {
                    continue;
                }

                var clip = Resources.Load<AudioClip>(paths[index]);
                if (clip != null)
                {
                    selectedIndex = index;
                    return clip;
                }
            }

            if (lastMusicIndex >= 0 && lastMusicIndex < paths.Length)
            {
                var fallback = Resources.Load<AudioClip>(paths[lastMusicIndex]);
                if (fallback != null)
                {
                    selectedIndex = lastMusicIndex;
                    return fallback;
                }
            }

            return null;
        }

        private System.Collections.IEnumerator FadeInGameplayMusic()
        {
            if (musicSource == null)
            {
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < GameplayMusicFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = ScaleMusicVolume(Mathf.Clamp01(elapsed / GameplayMusicFadeDuration) * GameplayMusicTargetVolume);
                yield return null;
            }

            musicSource.volume = ScaleMusicVolume(GameplayMusicTargetVolume);
            musicFade = null;
        }

        private static float ScaleMusicVolume(float volume)
        {
            return volume * ArenaUserSettings.MusicVolume;
        }

        private static float ScaleSfxVolume(float volume)
        {
            return volume * ArenaUserSettings.SfxVolume;
        }

        private AudioClip LoadClip(string clipName)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/{SfxPath}{clipName}.wav");
#else
            return Resources.Load<AudioClip>(SfxPath + clipName);
#endif
        }

        private AudioClip[] LoadClipSet(string prefix, int count)
        {
            var clips = new AudioClip[count];
            for (var i = 0; i < count; i++)
            {
                clips[i] = LoadClip($"{prefix}_{i + 1}");
            }

            return clips;
        }

        private AudioClip PickClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            for (var attempts = 0; attempts < 8; attempts++)
            {
                var clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    return clip;
                }
            }

            foreach (var clip in clips)
            {
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private AudioClip CreateGunshot(string clipName, float duration, float frequency)
        {
            const int sampleRate = 44100;
            var length = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[length];

            for (var i = 0; i < length; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = Mathf.Exp(-t * 28f);
                var crack = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.35f;
                data[i] = (Random.Range(-1f, 1f) * 0.8f + crack) * envelope;
            }

            var clip = AudioClip.Create(clipName, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateNoiseBurst(string clipName, float duration, float volume, float lowTone)
        {
            const int sampleRate = 44100;
            var length = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[length];

            for (var i = 0; i < length; i++)
            {
                var t = i / (float)length;
                var envelope = Mathf.Sin(t * Mathf.PI);
                data[i] = (Random.Range(-1f, 1f) * volume + Mathf.Sin(t * Mathf.PI * 12f) * lowTone) * envelope;
            }

            var clip = AudioClip.Create(clipName, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateCrowdCheer(string clipName, float duration, float volume)
        {
            const int sampleRate = 22050;
            var length = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[length];

            for (var i = 0; i < length; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                var wave = Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.08f + Mathf.Sin(2f * Mathf.PI * 137f * t) * 0.06f;
                data[i] = (Random.Range(-1f, 1f) * 0.45f + wave) * envelope * volume;
            }

            var clip = AudioClip.Create(clipName, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
