using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArenaShooter.Editor
{
    /// <summary>
    /// Headless-friendly helpers for visual iteration: jump straight into a match,
    /// park a spectator camera on the dome gallery, and dump game-view screenshots
    /// to Temp/ClaudeCaptures so changes can be reviewed outside the editor.
    /// </summary>
    [InitializeOnLoad]
    public static class ClaudeArenaDebugTools
    {
        private const string CaptureFolder = "Temp/ClaudeCaptures";
        private const string CaptureIndexKey = "ClaudeArenaDebugTools.CaptureIndex";
        private const string PendingMatchLoadKey = "ClaudeArenaDebugTools.PendingMatchLoad";
        private const string SpectatorName = "Claude Spectator Camera";

        private static GameObject spectator;
        private static float spectatorYaw;

        static ClaudeArenaDebugTools()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Claude/Start All Out War Match")]
        public static void StartAllOutWarMatch()
        {
            PlayerPrefs.SetString("ArenaGameMode", "AllOutWar");
            PlayerPrefs.Save();
            if (EditorApplication.isPlaying)
            {
                SceneManager.LoadScene("SampleScene");
                return;
            }

            // Two-step boot: enter play (lands on the forced MainMenu start scene),
            // then the play-mode-change handler hops to the match scene in-play.
            SessionState.SetBool(PendingMatchLoadKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingMatchLoadKey, false))
            {
                return;
            }

            SessionState.SetBool(PendingMatchLoadKey, false);
            SceneManager.LoadScene("SampleScene");
        }

        [MenuItem("Tools/Claude/Spectate Dome (Cycle Angle)")]
        public static void SpectateDome()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ClaudeArenaDebugTools] Spectating requires play mode.");
                return;
            }

            if (spectator == null)
            {
                spectator = GameObject.Find(SpectatorName);
            }

            if (spectator == null)
            {
                spectator = new GameObject(SpectatorName);
                var camera = spectator.AddComponent<Camera>();
                camera.depth = 100f;
                camera.fieldOfView = 74f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                spectatorYaw = 0f;
                spectator.transform.position = ResolveStadiumCenter() + Vector3.up * 2.2f;
            }
            else
            {
                spectatorYaw += 60f;
            }

            spectator.transform.rotation = Quaternion.Euler(-38f, spectatorYaw, 0f);
            Debug.Log($"[ClaudeArenaDebugTools] Spectator yaw {spectatorYaw:F0}.");
        }

        private static Vector3 ResolveStadiumCenter()
        {
            var shell = GameObject.Find("Hex Shield Dome Backdrop/Shield Dome Tinted Shell");
            if (shell != null && shell.TryGetComponent<Renderer>(out var renderer))
            {
                var center = renderer.bounds.center;
                return new Vector3(center.x, 0f, center.z);
            }

            return Camera.main != null ? Camera.main.transform.position : Vector3.up * 2f;
        }

        [MenuItem("Tools/Claude/Spectate Zoom Toggle")]
        public static void SpectateZoomToggle()
        {
            if (spectator == null)
            {
                SpectateDome();
            }

            // Telephoto inspection of the gallery band: artifacts that are a few
            // pixels wide from across the arena are invisible in wide captures.
            var camera = spectator.GetComponent<Camera>();
            camera.fieldOfView = camera.fieldOfView > 30f ? 14f : 74f;
            Debug.Log($"[ClaudeArenaDebugTools] Spectator FOV {camera.fieldOfView:F0}.");
        }

        [MenuItem("Tools/Claude/Spectate Pitch Up")]
        public static void SpectatePitchUp()
        {
            if (spectator == null)
            {
                SpectateDome();
                return;
            }

            var euler = spectator.transform.rotation.eulerAngles;
            spectator.transform.rotation = Quaternion.Euler(euler.x - 12f, euler.y, 0f);
        }

        [MenuItem("Tools/Claude/Stop Spectating")]
        public static void StopSpectating()
        {
            if (spectator != null)
            {
                Object.DestroyImmediate(spectator);
                spectator = null;
            }
        }

        [MenuItem("Tools/Claude/Report Play State")]
        public static void ReportPlayState()
        {
            var scene = SceneManager.GetActiveScene();
            Debug.Log($"[ClaudeArenaDebugTools] isPlaying={EditorApplication.isPlaying} isPaused={EditorApplication.isPaused} " +
                      $"frame={Time.frameCount} scene={scene.name} roots={scene.rootCount} pendingLoad={SessionState.GetBool(PendingMatchLoadKey, false)}");
        }

        [MenuItem("Tools/Claude/Force Resume")]
        public static void ForceResume()
        {
            EditorApplication.isPaused = false;
            Debug.Log("[ClaudeArenaDebugTools] Unpaused the editor.");
        }

        [MenuItem("Tools/Claude/Capture Game View")]
        public static void CaptureGameView()
        {
            Directory.CreateDirectory(CaptureFolder);
            var index = EditorPrefs.GetInt(CaptureIndexKey, 0) + 1;
            EditorPrefs.SetInt(CaptureIndexKey, index);
            var path = Path.Combine(CaptureFolder, $"shot_{index:D3}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[ClaudeArenaDebugTools] Capturing game view to {Path.GetFullPath(path)}");
        }

        [MenuItem("Tools/Claude/Capture Spectator Direct")]
        public static void CaptureSpectatorDirect()
        {
            // Renders the spectator (or main) camera to a texture and writes the PNG
            // immediately — no dependency on the game view repainting.
            var camera = spectator != null ? spectator.GetComponent<Camera>() : null;
            if (camera == null)
            {
                var found = GameObject.Find(SpectatorName);
                camera = found != null ? found.GetComponent<Camera>() : Camera.main;
            }

            if (camera == null)
            {
                Debug.LogWarning("[ClaudeArenaDebugTools] No camera available to capture.");
                return;
            }

            const int width = 1680;
            const int height = 760;
            var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();

                Directory.CreateDirectory(CaptureFolder);
                var index = EditorPrefs.GetInt(CaptureIndexKey, 0) + 1;
                EditorPrefs.SetInt(CaptureIndexKey, index);
                var path = Path.Combine(CaptureFolder, $"shot_{index:D3}.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"[ClaudeArenaDebugTools] Wrote spectator capture to {Path.GetFullPath(path)}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }
}
