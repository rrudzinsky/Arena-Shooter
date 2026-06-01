using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArenaShooter
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string GameSceneName = "SampleScene";
        private const string GeneratedRootName = "Generated Main Menu UI";
        private const string MusicSourceName = "Main Menu Theme Music";
        private const float OpeningThemeStartTime = 53f;
        private const float DefaultThemeMusicTargetVolume = 1f;
        private const float ThemeMusicStartDelay = 0.5f;
        private const float ThemeMusicFadeDuration = 20f;
        private const float StartupInputSuppressSeconds = 0.35f;
        private const float NavigationRepeatDelay = 0.22f;
        private const float GamepadNavigationPressThreshold = 0.55f;
        private const float GamepadNavigationReleaseThreshold = 0.25f;
        private const float ProtocolFlashRate = 12.5f;
        private const float DiscreteSliderRepeatDelay = 0.18f;
        private const float OpponentArmiesSliderUnitsPerSecond = 3.5f;
        private const float SoldiersPerArmySliderUnitsPerSecond = 95f;
        private const float BattlefieldCapSliderUnitsPerSecond = 170f;
        private const float SelectionPopDuration = 0.34f;
        private const float TitleIntroDuration = 15f;
        private const float TitleIntroDelay = 0f;
        private const float TitleIntroStartScale = 0.01f;
        private const float TitleIntroMaximumDeltaTime = 1f / 30f;
        private const float ReferenceCanvasWidth = 1920f;
        private const float ReferenceCanvasHeight = 1080f;

        private readonly List<MenuButtonVisual> menuButtons = new();

        private Text titleText;
        private RectTransform titleRect;
        private Image protocolBox;
        private Text protocolText;
        private Transform generatedInterfaceRoot;
        private Transform menuContentRoot;
        private Transform allOutWarContentRoot;
        private VerticalLayoutGroup menuContentLayout;
        private Text opponentArmiesValueText;
        private Text soldiersPerArmyValueText;
        private Text battlefieldCapValueText;
        private Text mapStyleValueText;
        private Text allOutWarTotalText;
        private Slider opponentArmiesSlider;
        private Slider soldiersPerArmySlider;
        private Slider battlefieldCapSlider;
        private AudioSource themeMusicSource;
        private float themeMusicTargetVolume = DefaultThemeMusicTargetVolume;
        private float pulse;
        private float navigationCooldown;
        private float discreteSliderCooldown;
        private float titleIntroTime;
        private int lastThemeMusicIndex = -1;
        private int lastMenuActivationFrame = -1;
        private int selectedMenuIndex = -1;
        private bool gamepadNavigationHeld;
        private bool pointerPressHeld;
        private bool refreshingAllOutWarSliders;
        private bool menuBuilt;
        private bool allOutWarSetupOpen;
        private Coroutine themeMusicFadeCoroutine;
        private static Sprite synthwaveRingSprite;
        private static Sprite synthwaveGridBackdropSprite;
        private static Sprite horizonDroidSprite;
        private static Sprite horizonDroidOccluderSprite;
        private static Sprite droidBodyPartsSprite;
        private static Sprite droidExplodedAssemblySprite;
        private static readonly string[] CornerDroidPoseResourcePaths =
        {
            "UI/DroidCornerPose1Transparent",
            "UI/DroidCornerPose2Transparent",
            "UI/DroidCornerPose3Transparent",
            "UI/DroidCornerPose4Transparent",
        };

        private static readonly Sprite[] cornerDroidPoseSprites = new Sprite[CornerDroidPoseResourcePaths.Length];
        private static readonly Sprite[] cornerDroidPoseOccluderSprites = new Sprite[CornerDroidPoseResourcePaths.Length];
        private static float startupInputSuppressedUntil = -1f;

        private void Start()
        {
            RebuildMenu();
        }

        public static void SuppressStartupInputBriefly()
        {
            startupInputSuppressedUntil = Mathf.Max(
                startupInputSuppressedUntil,
                Time.realtimeSinceStartup + StartupInputSuppressSeconds);
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                RebuildMenu();
            }
        }

        private void RebuildMenu()
        {
            if (menuBuilt)
            {
                ResolveGeneratedMenuReferences();
                ResetTitleIntro();
                return;
            }

            menuBuilt = true;
            BuildCamera();
            BuildEventSystem();
            ClearGeneratedInterface();
            BuildInterface();
            BuildThemeMusic();
        }

        private void Update()
        {
            pulse += Time.unscaledDeltaTime;
            UpdateProtocolFlash();
            UpdateTitleIntro();
            UpdateMenuSelection();
            UpdateThemeMusic();
        }

        private static void BuildCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Menu Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.018f, 0.028f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.tag = "MainCamera";
        }

        private static void BuildEventSystem()
        {
            var existingEventSystem = FindAnyObjectByType<EventSystem>();
            if (existingEventSystem != null)
            {
                existingEventSystem.sendNavigationEvents = false;
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>().sendNavigationEvents = false;
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildThemeMusic()
        {
            var musicTransform = transform.Find(MusicSourceName);
            var musicObject = musicTransform != null ? musicTransform.gameObject : new GameObject(MusicSourceName);
            musicObject.transform.SetParent(transform, false);

            if (!musicObject.TryGetComponent<AudioSource>(out var audioSource))
            {
                audioSource = musicObject.AddComponent<AudioSource>();
            }

            themeMusicSource = audioSource;
            themeMusicTargetVolume = audioSource.volume > 0f ? audioSource.volume : DefaultThemeMusicTargetVolume;

            audioSource.Stop();
            audioSource.volume = 0f;
            audioSource.clip = null;
            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            if (!Application.isPlaying)
            {
                audioSource.volume = ScaleThemeMusicVolume(themeMusicTargetVolume);
            }

            if (Application.isPlaying && !audioSource.isPlaying)
            {
                if (themeMusicFadeCoroutine != null)
                {
                    StopCoroutine(themeMusicFadeCoroutine);
                    themeMusicFadeCoroutine = null;
                }

                themeMusicFadeCoroutine = StartCoroutine(PlayOpeningThemeAfterDelay());
            }
        }

        private void UpdateThemeMusic()
        {
            if (!Application.isPlaying || themeMusicSource == null || themeMusicSource.isPlaying || themeMusicFadeCoroutine != null)
            {
                return;
            }

            PlayRandomThemeTrack();
        }

        private void PlayOpeningTheme()
        {
            var clip = Resources.Load<AudioClip>(ArenaMusicLibrary.OpeningThemeResourcePath);
            if (clip == null)
            {
                PlayRandomThemeTrack();
                return;
            }

            themeMusicSource.Stop();
            themeMusicSource.volume = 0f;
            themeMusicSource.clip = clip;
            themeMusicSource.time = Mathf.Clamp(OpeningThemeStartTime, 0f, Mathf.Max(clip.length - 0.01f, 0f));
            themeMusicSource.Play();
            if (themeMusicFadeCoroutine != null)
            {
                StopCoroutine(themeMusicFadeCoroutine);
            }

            themeMusicFadeCoroutine = StartCoroutine(FadeInThemeMusic());
            lastThemeMusicIndex = 0;
        }

        private IEnumerator PlayOpeningThemeAfterDelay()
        {
            yield return new WaitForSecondsRealtime(ThemeMusicStartDelay);
            themeMusicFadeCoroutine = null;
            PlayOpeningTheme();
        }

        private IEnumerator FadeInThemeMusic()
        {
            if (themeMusicSource == null)
            {
                yield break;
            }

            themeMusicSource.volume = 0f;
            var elapsed = 0f;
            while (elapsed < ThemeMusicFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                themeMusicSource.volume = ScaleThemeMusicVolume(Mathf.Clamp01(elapsed / ThemeMusicFadeDuration) * themeMusicTargetVolume);
                yield return null;
            }

            themeMusicSource.volume = ScaleThemeMusicVolume(themeMusicTargetVolume);
            themeMusicFadeCoroutine = null;
        }

        private static float ScaleThemeMusicVolume(float volume)
        {
            return volume * ArenaUserSettings.MusicVolume;
        }

        private void PlayRandomThemeTrack()
        {
            if (themeMusicSource == null || ArenaMusicLibrary.TrackResourcePaths.Length == 0)
            {
                return;
            }

            var index = Random.Range(0, ArenaMusicLibrary.TrackResourcePaths.Length);
            if (ArenaMusicLibrary.TrackResourcePaths.Length > 1 && index == lastThemeMusicIndex)
            {
                index = (index + 1) % ArenaMusicLibrary.TrackResourcePaths.Length;
            }

            var clip = Resources.Load<AudioClip>(ArenaMusicLibrary.TrackResourcePaths[index]);
            if (clip == null)
            {
                return;
            }

            themeMusicSource.clip = clip;
            themeMusicSource.time = 0f;
            themeMusicSource.volume = 0f;
            themeMusicSource.Play();
            if (themeMusicFadeCoroutine != null)
            {
                StopCoroutine(themeMusicFadeCoroutine);
            }

            themeMusicFadeCoroutine = StartCoroutine(FadeInThemeMusic());
            lastThemeMusicIndex = index;
        }

        private void BuildInterface()
        {
            var canvasObject = new GameObject(GeneratedRootName);
            canvasObject.transform.SetParent(transform, false);
            generatedInterfaceRoot = canvasObject.transform;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            BuildSynthwaveTitleBackground(canvasObject.transform);

            titleText = CreateText("GLADIATOR GAMES", canvasObject.transform, 86, FontStyle.Bold, new Color(0.86f, 1f, 1f));
            titleText.alignment = TextAnchor.MiddleCenter;
            titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.08f, 0.70f);
            titleRect.anchorMax = new Vector2(0.92f, 0.86f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            titleRect.localScale = Vector3.one * TitleIntroStartScale;
            titleText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0.78f, 1f, 0.72f);
            ResetTitleIntro();

            protocolBox = CreatePanel("Protocol Flash Box", canvasObject.transform, new Color(1f, 0.78f, 0.04f, 0.16f));
            Stretch(protocolBox.rectTransform, new Vector2(0.405f, 0.635f), new Vector2(0.595f, 0.685f), Vector2.zero, Vector2.zero);

            protocolText = CreateText("SELECT ARENA PROTOCOL", canvasObject.transform, 22, FontStyle.Bold, new Color(1f, 0.88f, 0.16f, 0.95f));
            protocolText.alignment = TextAnchor.MiddleCenter;
            protocolText.rectTransform.anchorMin = new Vector2(0.34f, 0.638f);
            protocolText.rectTransform.anchorMax = new Vector2(0.66f, 0.682f);
            protocolText.rectTransform.offsetMin = Vector2.zero;
            protocolText.rectTransform.offsetMax = Vector2.zero;
            protocolText.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.34f, 0.02f, 0.55f);

            var buttonRoot = new GameObject("Menu Content");
            buttonRoot.transform.SetParent(canvasObject.transform, false);
            menuContentRoot = buttonRoot.transform;
            var buttonRootRect = buttonRoot.AddComponent<RectTransform>();
            buttonRootRect.anchorMin = new Vector2(0.33f, 0.30f);
            buttonRootRect.anchorMax = new Vector2(0.67f, 0.58f);
            buttonRootRect.offsetMin = Vector2.zero;
            buttonRootRect.offsetMax = Vector2.zero;
            var layout = buttonRoot.AddComponent<VerticalLayoutGroup>();
            menuContentLayout = layout;
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            CreateMenuButton("All Out War", buttonRoot.transform, MenuCommand.ShowAllOutWarSetup);
            CreateMenuButton("King of the Colosseum", buttonRoot.transform, MenuCommand.LoadKingOfTheColosseum);
            CreateMenuButton("Exit Game", buttonRoot.transform, MenuCommand.ExitGame);
            ConfigureButtonNavigation();
            SelectMenuButton(0, true);
        }

        private void ClearGeneratedInterface()
        {
            titleText = null;
            titleRect = null;
            protocolBox = null;
            protocolText = null;
            generatedInterfaceRoot = null;
            menuContentRoot = null;
            allOutWarContentRoot = null;
            menuContentLayout = null;
            opponentArmiesValueText = null;
            soldiersPerArmyValueText = null;
            battlefieldCapValueText = null;
            mapStyleValueText = null;
            allOutWarTotalText = null;
            opponentArmiesSlider = null;
            soldiersPerArmySlider = null;
            battlefieldCapSlider = null;
            menuButtons.Clear();
            selectedMenuIndex = -1;
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != GeneratedRootName)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void ShowAllOutWarSetup()
        {
            ResolveGeneratedMenuReferences();
            if (menuContentRoot == null)
            {
                Debug.LogWarning("All Out War setup could not be shown because the menu content root is missing.");
                return;
            }

            ClearMenuContent();
            allOutWarSetupOpen = true;
            menuContentRoot.gameObject.SetActive(true);
            if (menuContentLayout != null)
            {
                menuContentLayout.enabled = false;
            }

            if (menuContentRoot is RectTransform contentRect)
            {
                contentRect.anchorMin = new Vector2(0.22f, 0.13f);
                contentRect.anchorMax = new Vector2(0.78f, 0.62f);
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;
            }

            if (protocolText != null)
            {
                protocolText.text = "ALL OUT WAR CONFIGURATION";
            }

            var setupRoot = menuContentRoot;

            CreateSliderRow(setupRoot.transform, "Number of opponent armies", 0.88f, 1f, 7f, 3f, true, out opponentArmiesSlider, out opponentArmiesValueText);
            CreateSliderRow(setupRoot.transform, "Soldiers per army", 0.69f, 25f, 250f, 100f, false, out soldiersPerArmySlider, out soldiersPerArmyValueText);
            CreateSliderRow(setupRoot.transform, "Soldiers on battlefield at one time", 0.50f, 10f, 399f, 80f, false, out battlefieldCapSlider, out battlefieldCapValueText);
            CreateOptionRow(setupRoot.transform, "Map style", "Randomly Generate", 0.31f, out mapStyleValueText);

            allOutWarTotalText = CreateText("", setupRoot.transform, 20, FontStyle.Bold, new Color(0.35f, 0.82f, 1f, 0.94f));
            allOutWarTotalText.alignment = TextAnchor.MiddleCenter;
            allOutWarTotalText.rectTransform.anchorMin = new Vector2(0f, 0.17f);
            allOutWarTotalText.rectTransform.anchorMax = new Vector2(1f, 0.25f);
            allOutWarTotalText.rectTransform.offsetMin = Vector2.zero;
            allOutWarTotalText.rectTransform.offsetMax = Vector2.zero;

            var playButton = CreateMenuButton("Play", setupRoot.transform, MenuCommand.LoadAllOutWar).Rect;
            playButton.anchorMin = new Vector2(0.34f, 0.02f);
            playButton.anchorMax = new Vector2(0.66f, 0.14f);
            playButton.offsetMin = Vector2.zero;
            playButton.offsetMax = Vector2.zero;

            var backButton = CreateMenuButton("Back", setupRoot.transform, MenuCommand.BuildMainModeButtons).Rect;
            backButton.anchorMin = new Vector2(0.38f, -0.12f);
            backButton.anchorMax = new Vector2(0.62f, -0.02f);
            backButton.offsetMin = Vector2.zero;
            backButton.offsetMax = Vector2.zero;

            opponentArmiesSlider.onValueChanged.AddListener(_ => RefreshAllOutWarSliders());
            soldiersPerArmySlider.onValueChanged.AddListener(_ => RefreshAllOutWarSliders());
            battlefieldCapSlider.onValueChanged.AddListener(_ => RefreshAllOutWarSliders());
            ConfigureButtonNavigation();
            SelectMenuButton(0, true);
            RefreshAllOutWarSliders();
            Debug.Log("All Out War setup built with " + menuButtons.Count + " selectable rows.");
        }

        private void BuildMainModeButtons()
        {
            ResolveGeneratedMenuReferences();
            if (menuContentRoot == null)
            {
                return;
            }

            ClearMenuContent();
            allOutWarSetupOpen = false;
            ClearAllOutWarContent();
            menuContentRoot.gameObject.SetActive(true);
            if (menuContentRoot is RectTransform contentRect)
            {
                contentRect.anchorMin = new Vector2(0.33f, 0.30f);
                contentRect.anchorMax = new Vector2(0.67f, 0.58f);
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;
            }

            if (menuContentLayout != null)
            {
                menuContentLayout.enabled = true;
            }

            CreateMenuButton("All Out War", menuContentRoot, MenuCommand.ShowAllOutWarSetup);
            CreateMenuButton("King of the Colosseum", menuContentRoot, MenuCommand.LoadKingOfTheColosseum);
            CreateMenuButton("Exit Game", menuContentRoot, MenuCommand.ExitGame);
            ConfigureButtonNavigation();
            SelectMenuButton(0, true);
            if (protocolText != null)
            {
                protocolText.text = "SELECT ARENA PROTOCOL";
            }
        }

        private void ResolveGeneratedMenuReferences()
        {
            if (generatedInterfaceRoot == null)
            {
                generatedInterfaceRoot = transform.Find(GeneratedRootName);
            }

            if (titleText == null && generatedInterfaceRoot != null)
            {
                titleText = generatedInterfaceRoot.Find("GLADIATOR GAMES")?.GetComponent<Text>();
                titleRect = titleText != null ? titleText.rectTransform : null;
            }

            if (menuContentRoot == null && generatedInterfaceRoot != null)
            {
                menuContentRoot = generatedInterfaceRoot.Find("Menu Content");
            }

            if (menuContentLayout == null && menuContentRoot != null)
            {
                menuContentLayout = menuContentRoot.GetComponent<VerticalLayoutGroup>();
            }
        }

        private void ClearAllOutWarContent()
        {
            if (allOutWarContentRoot == null)
            {
                return;
            }

            var root = allOutWarContentRoot.gameObject;
            allOutWarContentRoot = null;
            root.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(root);
            }
            else
            {
                DestroyImmediate(root);
            }
        }

        private void ResetMenuSelectionState()
        {
            menuButtons.Clear();
            selectedMenuIndex = -1;
            opponentArmiesSlider = null;
            soldiersPerArmySlider = null;
            battlefieldCapSlider = null;
            opponentArmiesValueText = null;
            soldiersPerArmyValueText = null;
            battlefieldCapValueText = null;
            mapStyleValueText = null;
            allOutWarTotalText = null;
        }

        private void ClearMenuContent()
        {
            ResetMenuSelectionState();
            allOutWarSetupOpen = false;

            for (var i = menuContentRoot.childCount - 1; i >= 0; i--)
            {
                var child = menuContentRoot.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void RefreshAllOutWarSliders()
        {
            if (refreshingAllOutWarSliders || opponentArmiesSlider == null || soldiersPerArmySlider == null || battlefieldCapSlider == null)
            {
                return;
            }

            refreshingAllOutWarSliders = true;
            var opponentArmies = Mathf.RoundToInt(opponentArmiesSlider.value);
            var soldiersPerArmy = Mathf.RoundToInt(soldiersPerArmySlider.value);
            var totalArmies = opponentArmies + 1;
            var totalSoldiers = totalArmies * soldiersPerArmy;
            var battlefieldMax = Mathf.Max(1, totalSoldiers - 1);

            battlefieldCapSlider.maxValue = battlefieldMax;
            if (battlefieldCapSlider.value > battlefieldMax)
            {
                battlefieldCapSlider.value = battlefieldMax;
            }

            var battlefieldCap = Mathf.RoundToInt(battlefieldCapSlider.value);
            opponentArmiesValueText.text = opponentArmies.ToString();
            soldiersPerArmyValueText.text = soldiersPerArmy.ToString();
            battlefieldCapValueText.text = battlefieldCap + " / " + battlefieldMax;
            allOutWarTotalText.text = totalArmies + " armies, " + totalSoldiers + " total soldiers";
            refreshingAllOutWarSliders = false;
        }

        private void UpdateProtocolFlash()
        {
            if (protocolText == null || protocolBox == null)
            {
                return;
            }

            protocolText.color = new Color(1f, 0.88f, 0.16f, 0.95f);
            var flash = 0.5f + Mathf.Sin(pulse * ProtocolFlashRate) * 0.5f;
            protocolBox.color = Color.Lerp(new Color(1f, 0.58f, 0.02f, 0.12f), new Color(1f, 0.95f, 0.08f, 0.34f), flash);
        }

        private void ResetTitleIntro()
        {
            titleIntroTime = Application.isPlaying ? 0f : TitleIntroDuration;
            ApplyTitleIntroState(Application.isPlaying ? 0f : 1f);
        }

        private void UpdateTitleIntro()
        {
            if (titleText == null || titleRect == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                ApplyTitleIntroState(1f);
                return;
            }

            if (titleIntroTime >= TitleIntroDelay + TitleIntroDuration)
            {
                ApplyTitleIntroState(1f);
                return;
            }

            titleIntroTime += Mathf.Min(Time.unscaledDeltaTime, TitleIntroMaximumDeltaTime);
            var delayedTime = Mathf.Max(0f, titleIntroTime - TitleIntroDelay);
            ApplyTitleIntroState(Mathf.Clamp01(delayedTime / TitleIntroDuration));
        }

        private void ApplyTitleIntroState(float progress)
        {
            if (titleText == null || titleRect == null)
            {
                return;
            }

            var linearProgress = Mathf.Clamp01(progress);
            var alpha = Mathf.Lerp(0.08f, 1f, linearProgress);
            titleRect.anchorMin = new Vector2(0.08f, 0.70f);
            titleRect.anchorMax = new Vector2(0.92f, 0.86f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var scale = Mathf.Lerp(TitleIntroStartScale, 1f, linearProgress);
            titleRect.localScale = Vector3.one * scale;
            titleRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(linearProgress * Mathf.PI) * -1.6f);
            titleText.color = new Color(0.86f, 1f, 1f, alpha);
        }

        private void UpdateMenuSelection()
        {
            UpdateButtonVisuals();

            if (!Application.isPlaying || menuButtons.Count == 0)
            {
                return;
            }

            if (Time.realtimeSinceStartup < startupInputSuppressedUntil)
            {
                return;
            }

            navigationCooldown = Mathf.Max(0f, navigationCooldown - Time.unscaledDeltaTime);
            discreteSliderCooldown = Mathf.Max(0f, discreteSliderCooldown - Time.unscaledDeltaTime);
            var direction = ReadMenuNavigationDirection();
            if (direction != 0 && navigationCooldown <= 0f)
            {
                MoveMenuSelection(direction);
                navigationCooldown = NavigationRepeatDelay;
            }

            if (TryHandlePointerClick())
            {
                return;
            }

            if ((selectedMenuIndex < 0 || selectedMenuIndex >= menuButtons.Count) && menuButtons.Count > 0)
            {
                SelectMenuButton(GetEventSystemSelectedMenuIndex(0), true);
            }

            var selectedItem = selectedMenuIndex >= 0 && selectedMenuIndex < menuButtons.Count ? menuButtons[selectedMenuIndex] : null;
            var adjustment = ReadMenuSliderAdjustment();
            if (!adjustment.HasInput)
            {
                discreteSliderCooldown = 0f;
            }
            else if (selectedItem?.Slider != null)
            {
                AdjustMenuSlider(selectedItem.Slider, adjustment);
            }

            if (WasMenuBackPressed() && allOutWarSetupOpen)
            {
                BuildMainModeButtons();
                return;
            }

            if (WasMenuSubmitPressed() && selectedItem?.Command != MenuCommand.None)
            {
                ActivateMenuItem(selectedItem);
            }
        }

        private int ReadMenuNavigationDirection()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                {
                    return 1;
                }

                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                {
                    return -1;
                }
            }

            foreach (var gamepad in Gamepad.all)
            {
                var stickY = gamepad.leftStick.ReadValue().y;
                var dpadY = gamepad.dpad.ReadValue().y;
                var y = Mathf.Abs(dpadY) > Mathf.Abs(stickY) ? dpadY : stickY;
                if (Mathf.Abs(y) <= GamepadNavigationReleaseThreshold)
                {
                    gamepadNavigationHeld = false;
                    continue;
                }

                if (gamepadNavigationHeld || Mathf.Abs(y) < GamepadNavigationPressThreshold)
                {
                    continue;
                }

                gamepadNavigationHeld = true;
                return y < 0f ? 1 : -1;
            }

            return 0;
        }

        private MenuSliderAdjustment ReadMenuSliderAdjustment()
        {
            var axis = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
                {
                    axis += 1f;
                }

                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
                {
                    axis -= 1f;
                }
            }

            foreach (var gamepad in Gamepad.all)
            {
                var stickX = gamepad.leftStick.ReadValue().x;
                var dpadX = gamepad.dpad.ReadValue().x;
                if (Mathf.Abs(dpadX) >= GamepadNavigationReleaseThreshold)
                {
                    return new MenuSliderAdjustment(dpadX > 0f ? 1f : -1f, true);
                }

                if (Mathf.Abs(stickX) >= GamepadNavigationReleaseThreshold && Mathf.Abs(stickX) > Mathf.Abs(axis))
                {
                    axis = stickX;
                }
            }

            axis = Mathf.Clamp(axis, -1f, 1f);
            return Mathf.Approximately(axis, 0f)
                ? MenuSliderAdjustment.None
                : new MenuSliderAdjustment(axis, false);
        }

        private void AdjustMenuSlider(Slider slider, MenuSliderAdjustment adjustment)
        {
            if (slider == null)
            {
                return;
            }

            var useDiscreteStep = slider.wholeNumbers ||
                (adjustment.IsDpad && (slider == soldiersPerArmySlider || slider == battlefieldCapSlider));
            if (useDiscreteStep)
            {
                if (discreteSliderCooldown > 0f)
                {
                    return;
                }

                var direction = adjustment.Value > 0f ? 1f : -1f;
                slider.value = Mathf.Clamp(Mathf.Round(slider.value) + direction, slider.minValue, slider.maxValue);
                discreteSliderCooldown = DiscreteSliderRepeatDelay;
                return;
            }

            slider.value += adjustment.Value * GetSliderAdjustmentUnitsPerSecond(slider) * Time.unscaledDeltaTime;
        }

        private float GetSliderAdjustmentUnitsPerSecond(Slider slider)
        {
            if (slider == soldiersPerArmySlider)
            {
                return SoldiersPerArmySliderUnitsPerSecond;
            }

            if (slider == battlefieldCapSlider)
            {
                return BattlefieldCapSliderUnitsPerSecond;
            }

            return OpponentArmiesSliderUnitsPerSecond;
        }

        private bool WasMenuSubmitPressed()
        {
            var isPressed = false;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                isPressed |= keyboard.enterKey.wasPressedThisFrame
                    || keyboard.numpadEnterKey.wasPressedThisFrame
                    || keyboard.spaceKey.wasPressedThisFrame;
            }

            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame)
                {
                    return true;
                }

                foreach (var control in gamepad.allControls)
                {
                    if (control is not ButtonControl button || !button.wasPressedThisFrame || !IsSubmitButton(button))
                    {
                        continue;
                    }

                    isPressed = true;
                    break;
                }

                if (isPressed)
                {
                    break;
                }
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            isPressed |= Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.JoystickButton0)
                || Input.GetKeyDown(KeyCode.JoystickButton1)
                || Input.GetKeyDown(KeyCode.JoystickButton7);
#endif

            return isPressed;
        }

        private bool WasMenuBackPressed()
        {
            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.buttonEast.wasPressedThisFrame)
                {
                    return true;
                }
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                return true;
            }
#endif

            return false;
        }

        private static bool IsSubmitButton(ButtonControl button)
        {
            var name = button.name.ToLowerInvariant();
            var displayName = button.displayName.ToLowerInvariant();
            return name.Contains("buttonsouth")
                || name.Contains("buttoneast")
                || name.Contains("start")
                || name == "a"
                || name == "b"
                || displayName == "a"
                || displayName == "b"
                || displayName == "cross"
                || displayName == "circle";
        }

        private bool TryHandlePointerClick()
        {
            if (!TryGetPointerPress(out var pointerPosition))
            {
                return false;
            }

            Canvas.ForceUpdateCanvases();
            for (var i = 0; i < menuButtons.Count; i++)
            {
                var item = menuButtons[i];
                if (item.Rect == null || !RectTransformUtility.RectangleContainsScreenPoint(item.Rect, pointerPosition))
                {
                    continue;
                }

                SelectMenuButton(i, false);
                if (item.Slider != null)
                {
                    SetSliderFromPointer(item.Slider, pointerPosition);
                    return true;
                }

                ActivateMenuItem(item);
                return true;
            }

            return false;
        }

        private void ActivateMenuItem(MenuButtonVisual item)
        {
            if (item == null || item.Command == MenuCommand.None)
            {
                return;
            }

            if (lastMenuActivationFrame == Time.frameCount)
            {
                return;
            }

            lastMenuActivationFrame = Time.frameCount;
            Debug.Log("Main menu activated: " + (item.Label != null ? item.Label.text : item.Rect.name));
            ExecuteMenuCommand(item.Command);
        }

        private void ExecuteMenuCommand(MenuCommand command)
        {
            switch (command)
            {
                case MenuCommand.ShowAllOutWarSetup:
                    ShowAllOutWarSetup();
                    break;
                case MenuCommand.LoadKingOfTheColosseum:
                    LoadKingOfTheColosseum();
                    break;
                case MenuCommand.LoadAllOutWar:
                    LoadAllOutWar();
                    break;
                case MenuCommand.ExitGame:
                    ExitGame();
                    break;
                case MenuCommand.BuildMainModeButtons:
                    BuildMainModeButtons();
                    break;
            }
        }

        private int GetEventSystemSelectedMenuIndex(int fallbackIndex)
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            {
                return fallbackIndex;
            }

            var selectedObject = EventSystem.current.currentSelectedGameObject;
            for (var i = 0; i < menuButtons.Count; i++)
            {
                var visual = menuButtons[i];
                var buttonObject = visual.Button != null ? visual.Button.gameObject : null;
                var sliderObject = visual.Slider != null ? visual.Slider.gameObject : null;
                var rectObject = visual.Rect != null ? visual.Rect.gameObject : null;
                if (selectedObject != buttonObject && selectedObject != sliderObject && selectedObject != rectObject)
                {
                    continue;
                }

                return i;
            }

            return fallbackIndex;
        }

        private bool TryGetPointerPress(out Vector2 pointerPosition)
        {
            pointerPosition = default;
            var isPressed = false;
            if (Mouse.current != null)
            {
                pointerPosition = Mouse.current.position.ReadValue();
                isPressed = Mouse.current.leftButton.isPressed;
            }

            if (!isPressed && Pointer.current != null)
            {
                pointerPosition = Pointer.current.position.ReadValue();
                isPressed = Pointer.current.press.isPressed;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!isPressed)
            {
                pointerPosition = Input.mousePosition;
                isPressed = Input.GetMouseButton(0);
            }
#endif

            if (!isPressed)
            {
                pointerPressHeld = false;
                return false;
            }

            if (pointerPressHeld)
            {
                return false;
            }

            pointerPressHeld = true;
            return true;
        }

        private static void SetSliderFromPointer(Slider slider, Vector2 pointerPosition)
        {
            if (slider == null)
            {
                return;
            }

            var rect = slider.GetComponent<RectTransform>();
            if (rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, pointerPosition, null, out var localPoint))
            {
                return;
            }

            var normalized = Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, localPoint.x);
            slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, normalized);
        }

        private void MoveMenuSelection(int direction)
        {
            if (menuButtons.Count == 0)
            {
                return;
            }

            var nextIndex = selectedMenuIndex < 0 ? 0 : selectedMenuIndex + direction;
            if (nextIndex < 0)
            {
                nextIndex = menuButtons.Count - 1;
            }
            else if (nextIndex >= menuButtons.Count)
            {
                nextIndex = 0;
            }

            SelectMenuButton(nextIndex, false);
        }

        private void SelectMenuButton(int index, bool immediate)
        {
            if (index < 0 || index >= menuButtons.Count)
            {
                return;
            }

            selectedMenuIndex = index;
            for (var i = 0; i < menuButtons.Count; i++)
            {
                menuButtons[i].IsSelected = i == selectedMenuIndex;
                if (menuButtons[i].IsSelected)
                {
                    menuButtons[i].SelectionAnimationTime = immediate ? SelectionPopDuration : 0f;
                }
            }

            if (Application.isPlaying && EventSystem.current != null)
            {
                var selectable = menuButtons[selectedMenuIndex].Button != null
                    ? menuButtons[selectedMenuIndex].Button.gameObject
                    : menuButtons[selectedMenuIndex].Slider != null
                        ? menuButtons[selectedMenuIndex].Slider.gameObject
                        : null;
                EventSystem.current.SetSelectedGameObject(selectable);
            }

            UpdateButtonVisuals();
        }

        private void UpdateButtonVisuals()
        {
            if (menuButtons.Count == 0)
            {
                return;
            }

            var deltaTime = Application.isPlaying ? Time.unscaledDeltaTime : 0f;
            for (var i = 0; i < menuButtons.Count; i++)
            {
                var visual = menuButtons[i];
                if (visual.Rect == null || visual.Background == null || visual.Label == null)
                {
                    continue;
                }

                if (visual.SelectionAnimationTime < SelectionPopDuration)
                {
                    visual.SelectionAnimationTime += deltaTime;
                }

                var selected = visual.IsSelected;
                var popProgress = Mathf.Clamp01(visual.SelectionAnimationTime / SelectionPopDuration);
                var compactRow = visual.Slider != null || visual.Command == MenuCommand.None;
                var pop = selected ? Mathf.Sin(popProgress * Mathf.PI) * (compactRow ? 0.018f : 0.095f) : 0f;
                var selectedLift = selected ? compactRow ? 0.006f : 0.035f : 0f;
                var scale = 1f + selectedLift + pop;
                var wobble = selected && !compactRow ? Mathf.Sin(popProgress * Mathf.PI * 4f) * (1f - popProgress) * 4.2f : 0f;

                visual.Rect.localScale = new Vector3(scale, scale, 1f);
                visual.Rect.localRotation = Quaternion.Euler(0f, 0f, wobble);
                visual.Background.color = selected
                    ? visual.SelectedBackgroundColor
                    : visual.NormalBackgroundColor;
                visual.Label.color = selected
                    ? new Color(1f, 0.94f, 0.26f, 1f)
                    : new Color(0.82f, 1f, 1f, 1f);
                if (visual.Track != null)
                {
                    visual.Track.color = selected
                        ? new Color(0.035f, 0.11f, 0.13f, 0.70f)
                        : new Color(0.018f, 0.045f, 0.048f, 0.72f);
                }

                if (visual.LeftAccent != null)
                {
                    visual.LeftAccent.color = selected ? visual.SelectedLeftAccentColor : visual.NormalLeftAccentColor;
                }

                if (visual.RightAccent != null)
                {
                    visual.RightAccent.color = selected ? visual.SelectedRightAccentColor : visual.NormalRightAccentColor;
                }

            }
        }

        private void LoadKingOfTheColosseum()
        {
            PlayerPrefs.SetString("ArenaGameMode", "KingOfTheColosseum");
            PlayerPrefs.Save();
            SceneManager.LoadScene(GameSceneName);
        }

        private void LoadAllOutWar()
        {
            PlayerPrefs.SetString("ArenaGameMode", "AllOutWar");
            PlayerPrefs.SetInt("AllOutWarOpponentArmies", Mathf.RoundToInt(opponentArmiesSlider != null ? opponentArmiesSlider.value : 3f));
            PlayerPrefs.SetInt("AllOutWarSoldiersPerArmy", Mathf.RoundToInt(soldiersPerArmySlider != null ? soldiersPerArmySlider.value : 100f));
            PlayerPrefs.SetInt("AllOutWarBattlefieldCap", Mathf.RoundToInt(battlefieldCapSlider != null ? battlefieldCapSlider.value : 80f));
            PlayerPrefs.SetString("AllOutWarMapStyle", "Randomly Generate");
            PlayerPrefs.Save();
            SceneManager.LoadScene(GameSceneName);
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static Image CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void BuildSynthwaveTitleBackground(Transform parent)
        {
            var background = CreatePanel("Synthwave Night Backdrop", parent, new Color(0.004f, 0.004f, 0.012f, 1f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateSynthwaveGridBackdrop(parent);

            const float horizonY = 0.399f;
            CreateLeftDroidBodyPartsDiagram(parent, horizonY);
            CreateRightDroidExplodedAssembly(parent);
            CreateHorizonDroidRow(parent, horizonY);
            CreateLowerCornerDroidPoses(parent);
        }

        private static void CreateSynthwaveGridBackdrop(Transform parent)
        {
            var sprite = GetSynthwaveGridBackdropSprite();
            if (sprite == null)
            {
                return;
            }

            var image = CreateAnchoredSprite(parent, "Synthwave Grid Backdrop Image", sprite, new Vector2(0.5f, 0.5f), new Vector2(1920f, 1080f), new Vector2(0.5f, 0.5f), Color.white, false);
            image.rectTransform.anchoredPosition = new Vector2(0f, -48f);
        }

        private static void CreatePerspectiveLine(Transform parent, string name, Vector2 startAnchor, Vector2 endAnchor, Color color, float thickness)
        {
            var line = CreatePanel(name, parent, color);
            var rect = line.rectTransform;
            rect.anchorMin = startAnchor;
            rect.anchorMax = startAnchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;

            var dx = (endAnchor.x - startAnchor.x) * ReferenceCanvasWidth;
            var dy = (endAnchor.y - startAnchor.y) * ReferenceCanvasHeight;
            var length = Mathf.Sqrt(dx * dx + dy * dy);
            rect.sizeDelta = new Vector2(thickness, length);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg);
        }

        private static void CreateSynthwaveRing(Transform parent, Vector2 anchor, Vector2 size, Color color, string name)
        {
            var ring = new GameObject(name);
            ring.transform.SetParent(parent, false);
            var image = ring.AddComponent<Image>();
            image.sprite = GetSynthwaveRingSprite();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void CreateHorizonDroidRow(Transform parent, float horizonY)
        {
            var sprite = GetHorizonDroidSprite();
            if (sprite == null)
            {
                return;
            }

            var occluderSprite = GetHorizonDroidOccluderSprite();
            var droidAnchors = new[] { 0.08f, 0.81f, 0.92f };
            foreach (var anchorX in droidAnchors)
            {
                if (occluderSprite != null)
                {
                    CreateAnchoredSprite(parent, "Gold Horizon Droid Occluder", occluderSprite, new Vector2(anchorX, horizonY), new Vector2(150f, 228f), new Vector2(0.5f, 0f), new Color(1f, 1f, 1f, 1f), true);
                }

                var droid = new GameObject("Gold Horizon Droid Outline");
                droid.transform.SetParent(parent, false);
                var image = droid.AddComponent<Image>();
                image.sprite = sprite;
                image.color = new Color(1f, 1f, 1f, 0.62f);
                image.preserveAspect = true;
                image.raycastTarget = false;

                var rect = image.rectTransform;
                rect.anchorMin = new Vector2(anchorX, horizonY);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(150f, 228f);
            }
        }

        private static void CreateLeftDroidBodyPartsDiagram(Transform parent, float horizonY)
        {
            var sprite = GetDroidBodyPartsSprite();
            if (sprite == null)
            {
                return;
            }

            var diagram = new GameObject("Left Droid Body Parts Diagram");
            diagram.transform.SetParent(parent, false);
            var image = diagram.AddComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, 0.74f);
            image.preserveAspect = false;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.18f, horizonY);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 34f);
            rect.sizeDelta = new Vector2(420f, 414f);
        }

        private static Sprite GetDroidBodyPartsSprite()
        {
            if (droidBodyPartsSprite != null)
            {
                return droidBodyPartsSprite;
            }

            var texture = Resources.Load<Texture2D>("UI/DroidBodyPartsTransparent");
            if (texture == null)
            {
                Debug.LogWarning("Droid body parts sprite could not be loaded from Resources/UI/DroidBodyPartsTransparent.");
                return null;
            }

            droidBodyPartsSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 100f);
            return droidBodyPartsSprite;
        }

        private static void CreateRightDroidExplodedAssembly(Transform parent)
        {
            var sprite = GetDroidExplodedAssemblySprite();
            if (sprite == null)
            {
                return;
            }

            var diagram = new GameObject("Right Droid Exploded Assembly Diagram");
            diagram.transform.SetParent(parent, false);
            var image = diagram.AddComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, 0.72f);
            image.preserveAspect = false;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.865f, 0.80f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(620f, 332f);
        }

        private static Sprite GetDroidExplodedAssemblySprite()
        {
            if (droidExplodedAssemblySprite != null)
            {
                return droidExplodedAssemblySprite;
            }

            var texture = Resources.Load<Texture2D>("UI/DroidExplodedAssemblyTransparent");
            if (texture == null)
            {
                Debug.LogWarning("Droid exploded assembly sprite could not be loaded from Resources/UI/DroidExplodedAssemblyTransparent.");
                return null;
            }

            droidExplodedAssemblySprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            return droidExplodedAssemblySprite;
        }

        private static void CreateLowerCornerDroidPoses(Transform parent)
        {
            var anchors = new[]
            {
                new Vector2(0.095f, 0.255f),
                new Vector2(0.245f, 0.135f),
                new Vector2(0.765f, 0.245f),
                new Vector2(0.905f, 0.125f),
            };

            for (var i = 0; i < anchors.Length; i++)
            {
                var sprite = GetCornerDroidPoseSprite(i);
                if (sprite == null)
                {
                    continue;
                }

                var occluderSprite = GetCornerDroidPoseOccluderSprite(i);
                if (occluderSprite != null)
                {
                    CreateAnchoredSprite(parent, "Lower Corner Droid Occluder", occluderSprite, anchors[i], new Vector2(170f, 228f), new Vector2(0.5f, 0.5f), Color.white, true);
                }

                CreateAnchoredSprite(parent, "Lower Corner Droid Pose", sprite, anchors[i], new Vector2(170f, 228f), new Vector2(0.5f, 0.5f), new Color(1f, 1f, 1f, 0.62f), true);
            }
        }

        private static Sprite GetCornerDroidPoseSprite(int index)
        {
            if (index < 0 || index >= CornerDroidPoseResourcePaths.Length)
            {
                return null;
            }

            if (cornerDroidPoseSprites[index] != null)
            {
                return cornerDroidPoseSprites[index];
            }

            var texture = Resources.Load<Texture2D>(CornerDroidPoseResourcePaths[index]);
            if (texture == null)
            {
                Debug.LogWarning("Corner droid pose sprite could not be loaded from Resources/" + CornerDroidPoseResourcePaths[index] + ".");
                return null;
            }

            cornerDroidPoseSprites[index] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            return cornerDroidPoseSprites[index];
        }

        private static Sprite GetCornerDroidPoseOccluderSprite(int index)
        {
            if (index < 0 || index >= CornerDroidPoseResourcePaths.Length)
            {
                return null;
            }

            if (cornerDroidPoseOccluderSprites[index] != null)
            {
                return cornerDroidPoseOccluderSprites[index];
            }

            var occluderPath = CornerDroidPoseResourcePaths[index].Replace("Transparent", "Occluder");
            var texture = Resources.Load<Texture2D>(occluderPath);
            if (texture == null)
            {
                Debug.LogWarning("Corner droid pose occluder sprite could not be loaded from Resources/" + occluderPath + ".");
                return null;
            }

            cornerDroidPoseOccluderSprites[index] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            return cornerDroidPoseOccluderSprites[index];
        }

        private static Image CreateAnchoredSprite(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size, Vector2 pivot, Color color, bool preserveAspect)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(parent, false);
            var image = spriteObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return image;
        }

        private static Sprite GetHorizonDroidSprite()
        {
            if (horizonDroidSprite != null)
            {
                return horizonDroidSprite;
            }

            var texture = Resources.Load<Texture2D>("UI/GoldDroidOutlineTransparent");
            if (texture == null)
            {
                Debug.LogWarning("Gold droid horizon sprite could not be loaded from Resources/UI/GoldDroidOutlineTransparent.");
                return null;
            }

            horizonDroidSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 100f);
            return horizonDroidSprite;
        }

        private static Sprite GetHorizonDroidOccluderSprite()
        {
            if (horizonDroidOccluderSprite != null)
            {
                return horizonDroidOccluderSprite;
            }

            var texture = Resources.Load<Texture2D>("UI/GoldDroidOutlineOccluder");
            if (texture == null)
            {
                Debug.LogWarning("Gold droid occluder sprite could not be loaded from Resources/UI/GoldDroidOutlineOccluder.");
                return null;
            }

            horizonDroidOccluderSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 100f);
            return horizonDroidOccluderSprite;
        }

        private static Sprite GetSynthwaveGridBackdropSprite()
        {
            if (synthwaveGridBackdropSprite != null)
            {
                return synthwaveGridBackdropSprite;
            }

            var texture = Resources.Load<Texture2D>("UI/SynthwaveGridBackdrop");
            if (texture == null)
            {
                Debug.LogWarning("Synthwave grid backdrop sprite could not be loaded from Resources/UI/SynthwaveGridBackdrop.");
                return null;
            }

            synthwaveGridBackdropSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            return synthwaveGridBackdropSprite;
        }

        private static Sprite GetSynthwaveRingSprite()
        {
            if (synthwaveRingSprite != null)
            {
                return synthwaveRingSprite;
            }

            const int size = 256;
            const float radius = 98f;
            const float thickness = 5.5f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Generated Synthwave Cyan Ring",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    var alpha = Mathf.Clamp01(1f - Mathf.Abs(distance - radius) / thickness);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            synthwaveRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return synthwaveRingSprite;
        }

        private static Text CreateText(string text, Transform parent, int size, FontStyle style, Color color)
        {
            var textObject = new GameObject(text);
            textObject.transform.SetParent(parent, false);
            var label = textObject.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = size;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.75f);
            return label;
        }

        private MenuButtonVisual CreateMenuButton(string label, Transform parent, MenuCommand command)
        {
            var buttonObject = new GameObject(label + " Button");
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.055f, 0.075f, 0.09f, 0.96f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.15f, 0.20f, 0.22f, 1f);
            colors.pressedColor = new Color(0.22f, 0.03f, 0.05f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.minHeight = 74f;
            layout.preferredHeight = 82f;

            var text = CreateText(label.ToUpperInvariant(), buttonObject.transform, 28, FontStyle.Bold, new Color(0.82f, 1f, 1f));
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 8f), new Vector2(-24f, -8f));
            text.alignment = TextAnchor.MiddleCenter;

            var leftAccent = CreateButtonAccent(buttonObject.transform, new Vector2(0f, 0f), new Vector2(0.02f, 1f), new Color(1f, 0.08f, 0.12f, 0.94f));
            var rightAccent = CreateButtonAccent(buttonObject.transform, new Vector2(0.98f, 0f), new Vector2(1f, 1f), new Color(0f, 0.82f, 1f, 0.94f));
            var visual = new MenuButtonVisual
            {
                Rect = (RectTransform)buttonObject.transform,
                Background = image,
                Button = button,
                Command = command,
                Label = text,
                LeftAccent = leftAccent,
                RightAccent = rightAccent,
                NormalBackgroundColor = image.color,
                SelectedBackgroundColor = new Color(0.15f, 0.20f, 0.22f, 0.98f),
                NormalLeftAccentColor = new Color(1f, 0.08f, 0.12f, 0.94f),
                SelectedLeftAccentColor = new Color(1f, 0.76f, 0.04f, 1f),
                NormalRightAccentColor = new Color(0f, 0.82f, 1f, 0.94f),
                SelectedRightAccentColor = new Color(1f, 0.98f, 0.24f, 1f),
                SelectionAnimationTime = SelectionPopDuration,
            };
            button.onClick.AddListener(() => ActivateMenuItem(visual));
            menuButtons.Add(visual);
            return visual;
        }

        private void CreateSliderRow(Transform parent, string label, float centerY, float min, float max, float value, bool wholeNumbers, out Slider slider, out Text valueText)
        {
            var row = new GameObject(label + " Row");
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.02f, centerY - 0.07f);
            rowRect.anchorMax = new Vector2(0.98f, centerY + 0.07f);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;
            var rowBackground = row.AddComponent<Image>();
            rowBackground.color = new Color(0.025f, 0.043f, 0.048f, 0.94f);
            rowBackground.raycastTarget = false;
            var rowOutline = row.AddComponent<Outline>();
            rowOutline.effectColor = new Color(0f, 0.78f, 1f, 0.20f);
            rowOutline.effectDistance = new Vector2(1f, -1f);

            var labelText = CreateText(label.ToUpperInvariant(), row.transform, 16, FontStyle.Bold, new Color(0.82f, 1f, 1f, 0.96f));
            labelText.alignment = TextAnchor.MiddleLeft;
            Stretch(labelText.rectTransform, new Vector2(0.045f, 0.56f), new Vector2(0.72f, 0.94f), Vector2.zero, Vector2.zero);

            valueText = CreateText("", row.transform, 16, FontStyle.Bold, new Color(1f, 0.94f, 0.26f, 1f));
            valueText.alignment = TextAnchor.MiddleRight;
            Stretch(valueText.rectTransform, new Vector2(0.72f, 0.56f), new Vector2(0.955f, 0.94f), Vector2.zero, Vector2.zero);

            var leftAccent = CreatePanel("Slider Row Left Rail", row.transform, new Color(0f, 0.82f, 1f, 0.68f));
            Stretch(leftAccent.rectTransform, new Vector2(0f, 0f), new Vector2(0.007f, 1f), Vector2.zero, Vector2.zero);
            var rightAccent = CreatePanel("Slider Row Right Rail", row.transform, new Color(0f, 0.82f, 1f, 0.68f));
            Stretch(rightAccent.rectTransform, new Vector2(0.993f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var topSheen = CreatePanel("Slider Row Top Sheen", row.transform, new Color(0.34f, 1f, 1f, 0.16f));
            Stretch(topSheen.rectTransform, new Vector2(0.018f, 0.96f), new Vector2(0.982f, 1f), Vector2.zero, Vector2.zero);
            var lowerShadow = CreatePanel("Slider Row Lower Shadow", row.transform, new Color(0f, 0f, 0f, 0.25f));
            Stretch(lowerShadow.rectTransform, new Vector2(0.018f, 0f), new Vector2(0.982f, 0.08f), Vector2.zero, Vector2.zero);

            var sliderObject = new GameObject(label + " Slider");
            sliderObject.transform.SetParent(row.transform, false);
            var sliderRect = sliderObject.AddComponent<RectTransform>();
            Stretch(sliderRect, new Vector2(0.045f, 0.16f), new Vector2(0.955f, 0.47f), Vector2.zero, Vector2.zero);
            slider = sliderObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;

            var background = CreatePanel("Slider Track", sliderObject.transform, new Color(0.008f, 0.028f, 0.033f, 0.86f));
            Stretch(background.rectTransform, new Vector2(0f, 0.28f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero);
            var trackGlow = CreatePanel("Slider Track Glow", sliderObject.transform, new Color(0f, 0.82f, 1f, 0.18f));
            Stretch(trackGlow.rectTransform, new Vector2(0f, 0.16f), new Vector2(1f, 0.84f), Vector2.zero, Vector2.zero);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            Stretch(fillAreaRect, new Vector2(0f, 0.28f), new Vector2(1f, 0.72f), new Vector2(8f, 0f), new Vector2(-8f, 0f));

            var fill = CreatePanel("Fill", fillArea.transform, new Color(0f, 0.82f, 1f, 0.88f));
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            Stretch(handleAreaRect, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));

            var handle = CreatePanel("Handle", handleArea.transform, new Color(1f, 0.94f, 0.26f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(16f, 26f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;

            menuButtons.Add(new MenuButtonVisual
            {
                Rect = rowRect,
                Background = rowBackground,
                Track = background,
                LeftAccent = leftAccent,
                RightAccent = rightAccent,
                Slider = slider,
                Label = labelText,
                NormalBackgroundColor = new Color(0.025f, 0.043f, 0.048f, 0.94f),
                SelectedBackgroundColor = new Color(0.115f, 0.18f, 0.19f, 0.99f),
                NormalLeftAccentColor = new Color(0f, 0.82f, 1f, 0.68f),
                SelectedLeftAccentColor = new Color(1f, 0.94f, 0.12f, 1f),
                NormalRightAccentColor = new Color(0f, 0.82f, 1f, 0.68f),
                SelectedRightAccentColor = new Color(1f, 0.94f, 0.12f, 1f),
                SelectionAnimationTime = SelectionPopDuration,
            });
        }

        private void CreateOptionRow(Transform parent, string label, string value, float centerY, out Text valueText)
        {
            var row = new GameObject(label + " Row");
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.02f, centerY - 0.065f);
            rowRect.anchorMax = new Vector2(0.98f, centerY + 0.065f);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            var rowBackground = row.AddComponent<Image>();
            rowBackground.color = new Color(0.025f, 0.043f, 0.048f, 0.94f);
            rowBackground.raycastTarget = false;
            var rowOutline = row.AddComponent<Outline>();
            rowOutline.effectColor = new Color(1f, 0.94f, 0.12f, 0.20f);
            rowOutline.effectDistance = new Vector2(1f, -1f);

            var leftAccent = CreatePanel("Option Row Left Rail", row.transform, new Color(0f, 0.82f, 1f, 0.68f));
            Stretch(leftAccent.rectTransform, new Vector2(0f, 0f), new Vector2(0.007f, 1f), Vector2.zero, Vector2.zero);
            var rightAccent = CreatePanel("Option Row Right Rail", row.transform, new Color(0f, 0.82f, 1f, 0.68f));
            Stretch(rightAccent.rectTransform, new Vector2(0.993f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var labelText = CreateText(label.ToUpperInvariant(), row.transform, 16, FontStyle.Bold, new Color(0.82f, 1f, 1f, 0.96f));
            labelText.alignment = TextAnchor.MiddleLeft;
            Stretch(labelText.rectTransform, new Vector2(0.045f, 0.16f), new Vector2(0.42f, 0.84f), Vector2.zero, Vector2.zero);

            valueText = CreateText(value.ToUpperInvariant(), row.transform, 16, FontStyle.Bold, new Color(1f, 0.94f, 0.26f, 1f));
            valueText.alignment = TextAnchor.MiddleRight;
            Stretch(valueText.rectTransform, new Vector2(0.42f, 0.16f), new Vector2(0.955f, 0.84f), Vector2.zero, Vector2.zero);

            menuButtons.Add(new MenuButtonVisual
            {
                Rect = rowRect,
                Background = rowBackground,
                LeftAccent = leftAccent,
                RightAccent = rightAccent,
                Label = labelText,
                NormalBackgroundColor = new Color(0.025f, 0.043f, 0.048f, 0.94f),
                SelectedBackgroundColor = new Color(0.115f, 0.18f, 0.19f, 0.99f),
                NormalLeftAccentColor = new Color(0f, 0.82f, 1f, 0.68f),
                SelectedLeftAccentColor = new Color(1f, 0.94f, 0.12f, 1f),
                NormalRightAccentColor = new Color(0f, 0.82f, 1f, 0.68f),
                SelectedRightAccentColor = new Color(1f, 0.94f, 0.12f, 1f),
                SelectionAnimationTime = SelectionPopDuration,
            });
        }

        private void ConfigureButtonNavigation()
        {
            for (var i = 0; i < menuButtons.Count; i++)
            {
                var visual = menuButtons[i];
                if (visual.Button == null)
                {
                    continue;
                }

                visual.Button.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = menuButtons[(i - 1 + menuButtons.Count) % menuButtons.Count].Button,
                    selectOnDown = menuButtons[(i + 1) % menuButtons.Count].Button,
                };
            }
        }

        private static Image CreateButtonAccent(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var accent = CreatePanel("Neon Button Edge", parent, color);
            Stretch(accent.rectTransform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            return accent;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private sealed class MenuButtonVisual
        {
            public RectTransform Rect;
            public Image Background;
            public Image Track;
            public Image LeftAccent;
            public Image RightAccent;
            public Button Button;
            public Slider Slider;
            public MenuCommand Command;
            public Text Label;
            public Color NormalBackgroundColor;
            public Color SelectedBackgroundColor;
            public Color NormalLeftAccentColor;
            public Color SelectedLeftAccentColor;
            public Color NormalRightAccentColor;
            public Color SelectedRightAccentColor;
            public float SelectionAnimationTime;
            public bool IsSelected;
        }

        private readonly struct MenuSliderAdjustment
        {
            public static readonly MenuSliderAdjustment None = new(0f, false);

            public readonly float Value;
            public readonly bool IsDpad;
            public bool HasInput => !Mathf.Approximately(Value, 0f);

            public MenuSliderAdjustment(float value, bool isDpad)
            {
                Value = value;
                IsDpad = isDpad;
            }
        }

        private enum MenuCommand
        {
            None,
            ShowAllOutWarSetup,
            LoadKingOfTheColosseum,
            LoadAllOutWar,
            ExitGame,
            BuildMainModeButtons,
        }
    }
}
