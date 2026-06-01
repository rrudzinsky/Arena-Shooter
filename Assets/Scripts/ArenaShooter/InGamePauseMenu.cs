using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ArenaShooter
{
    public sealed class InGamePauseMenu : MonoBehaviour
    {
        private enum MenuView
        {
            Main,
            Settings
        }

        private enum MenuCommand
        {
            None,
            ReturnToGame,
            RestartGame,
            QuitGame,
            Settings,
            Back
        }

        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const float NavigationRepeatDelay = 0.22f;
        private const float SliderRepeatDelay = 0.16f;
        private const float GamepadNavigationPressThreshold = 0.55f;
        private const float GamepadNavigationReleaseThreshold = 0.25f;
        private const float SelectionPopDuration = 0.22f;

        private readonly List<MenuItem> items = new();
        private MatchController match;
        private Canvas canvas;
        private RectTransform panel;
        private Text titleText;
        private MenuView currentView;
        private int selectedIndex;
        private float navigationCooldown;
        private float sliderCooldown;
        private bool gamepadNavigationHeld;
        private int openedFrame = -1;
        private int lastActivationFrame = -1;
        private Slider controllerLookSlider;
        private Slider musicSlider;
        private Slider sfxSlider;
        private Text controllerLookValueText;
        private Text musicValueText;
        private Text sfxValueText;

        public void Build(MatchController owner)
        {
            match = owner;
            EnsureEventSystem();
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2200;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildFrame();
            gameObject.SetActive(false);
        }

        public void ShowMain()
        {
            gameObject.SetActive(true);
            openedFrame = Time.frameCount;
            BuildMainView();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            UpdateItemVisuals();
            if (Time.frameCount == openedFrame)
            {
                return;
            }

            navigationCooldown = Mathf.Max(0f, navigationCooldown - Time.unscaledDeltaTime);
            sliderCooldown = Mathf.Max(0f, sliderCooldown - Time.unscaledDeltaTime);

            var direction = ReadNavigationDirection();
            if (direction != 0 && navigationCooldown <= 0f)
            {
                MoveSelection(direction);
                navigationCooldown = NavigationRepeatDelay;
            }

            if (TryHandlePointerClick())
            {
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= items.Count)
            {
                SelectItem(0, true);
            }

            var selected = selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
            var sliderAdjustment = ReadSliderAdjustment();
            if (!sliderAdjustment.HasInput)
            {
                sliderCooldown = 0f;
            }
            else if (selected?.Slider != null)
            {
                AdjustSlider(selected.Slider, sliderAdjustment);
            }

            if (WasBackPressed())
            {
                HandleBack();
                return;
            }

            if (WasSubmitPressed() && selected != null && selected.Command != MenuCommand.None)
            {
                ActivateItem(selected);
            }
        }

        private void BuildFrame()
        {
            var backdrop = CreatePanel("Pause Backdrop", transform, new Color(0f, 0f, 0.005f, 0.72f));
            Stretch(backdrop.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var panelImage = CreatePanel("Pause Panel", transform, new Color(0.012f, 0.018f, 0.025f, 0.96f));
            panel = panelImage.rectTransform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(760f, 620f);
            panel.anchoredPosition = Vector2.zero;

            var outline = panelImage.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.82f, 1f, 0.36f);
            outline.effectDistance = new Vector2(2f, -2f);

            var leftRail = CreatePanel("Pause Left Rail", panel, new Color(1f, 0.08f, 0.18f, 0.92f));
            Stretch(leftRail.rectTransform, Vector2.zero, new Vector2(0.012f, 1f), Vector2.zero, Vector2.zero);
            var rightRail = CreatePanel("Pause Right Rail", panel, new Color(0f, 0.82f, 1f, 0.92f));
            Stretch(rightRail.rectTransform, new Vector2(0.988f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            var topRail = CreatePanel("Pause Top Rail", panel, new Color(1f, 0.08f, 0.9f, 0.54f));
            Stretch(topRail.rectTransform, new Vector2(0f, 0.988f), Vector2.one, Vector2.zero, Vector2.zero);

            titleText = CreateText("PAUSED", panel, 44, FontStyle.Bold, new Color(0.86f, 1f, 1f, 1f));
            titleText.alignment = TextAnchor.MiddleCenter;
            Stretch(titleText.rectTransform, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
        }

        private void BuildMainView()
        {
            currentView = MenuView.Main;
            titleText.text = "PAUSED";
            ClearItems();
            CreateButton("RETURN TO GAME", 0.69f, MenuCommand.ReturnToGame);
            CreateButton("RESTART GAME", 0.55f, MenuCommand.RestartGame);
            CreateButton("SETTINGS", 0.41f, MenuCommand.Settings);
            CreateButton("QUIT GAME", 0.27f, MenuCommand.QuitGame);
            SelectItem(0, true);
        }

        private void BuildSettingsView()
        {
            currentView = MenuView.Settings;
            titleText.text = "SETTINGS";
            ClearItems();
            CreateSlider("CONTROLLER LOOK", 0.67f, ArenaUserSettings.MinControllerLookScale, ArenaUserSettings.MaxControllerLookScale, ArenaUserSettings.ControllerLookScale, out controllerLookSlider, out controllerLookValueText);
            CreateSlider("MUSIC VOLUME", 0.51f, 0f, 1f, ArenaUserSettings.MusicVolume, out musicSlider, out musicValueText);
            CreateSlider("SFX VOLUME", 0.35f, 0f, 1f, ArenaUserSettings.SfxVolume, out sfxSlider, out sfxValueText);
            CreateButton("BACK", 0.18f, MenuCommand.Back);
            UpdateSettingsLabels();
            SelectItem(0, true);
        }

        private void ClearItems()
        {
            for (var i = panel.childCount - 1; i >= 0; i--)
            {
                var child = panel.GetChild(i);
                if (child == titleText.transform ||
                    child.name.Contains("Rail"))
                {
                    continue;
                }

                Destroy(child.gameObject);
            }

            items.Clear();
            selectedIndex = -1;
        }

        private void CreateButton(string label, float centerY, MenuCommand command)
        {
            var buttonObject = new GameObject(label + " Button");
            buttonObject.transform.SetParent(panel, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.13f, centerY - 0.055f);
            rect.anchorMax = new Vector2(0.87f, centerY + 0.055f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.045f, 0.065f, 0.078f, 0.96f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(label, buttonObject.transform, 26, FontStyle.Bold, new Color(0.82f, 1f, 1f, 1f));
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 4f), new Vector2(-18f, -4f));

            var leftAccent = CreatePanel("Button Left Accent", buttonObject.transform, new Color(1f, 0.08f, 0.18f, 0.9f));
            Stretch(leftAccent.rectTransform, Vector2.zero, new Vector2(0.018f, 1f), Vector2.zero, Vector2.zero);
            var rightAccent = CreatePanel("Button Right Accent", buttonObject.transform, new Color(0f, 0.82f, 1f, 0.9f));
            Stretch(rightAccent.rectTransform, new Vector2(0.982f, 0f), Vector2.one, Vector2.zero, Vector2.zero);

            var item = new MenuItem
            {
                Rect = rect,
                Background = image,
                Button = button,
                Command = command,
                Label = text,
                LeftAccent = leftAccent,
                RightAccent = rightAccent,
            };
            button.onClick.AddListener(() => ActivateItem(item));
            items.Add(item);
        }

        private void CreateSlider(string label, float centerY, float min, float max, float value, out Slider slider, out Text valueText)
        {
            var row = new GameObject(label + " Row");
            row.transform.SetParent(panel, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.08f, centerY - 0.07f);
            rowRect.anchorMax = new Vector2(0.92f, centerY + 0.07f);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            var rowBack = row.AddComponent<Image>();
            rowBack.color = new Color(0.025f, 0.043f, 0.048f, 0.94f);

            var labelText = CreateText(label, row.transform, 18, FontStyle.Bold, new Color(0.82f, 1f, 1f, 0.96f));
            labelText.alignment = TextAnchor.MiddleLeft;
            Stretch(labelText.rectTransform, new Vector2(0.04f, 0.55f), new Vector2(0.70f, 0.94f), Vector2.zero, Vector2.zero);

            valueText = CreateText("", row.transform, 18, FontStyle.Bold, new Color(1f, 0.94f, 0.26f, 1f));
            valueText.alignment = TextAnchor.MiddleRight;
            Stretch(valueText.rectTransform, new Vector2(0.70f, 0.55f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);

            var sliderObject = new GameObject(label + " Slider");
            sliderObject.transform.SetParent(row.transform, false);
            var sliderRect = sliderObject.AddComponent<RectTransform>();
            Stretch(sliderRect, new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.48f), Vector2.zero, Vector2.zero);

            slider = sliderObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.wholeNumbers = false;

            var track = CreatePanel("Slider Track", sliderObject.transform, new Color(0.008f, 0.028f, 0.033f, 0.86f));
            Stretch(track.rectTransform, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f), Vector2.zero, Vector2.zero);
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            Stretch(fillAreaRect, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
            var fill = CreatePanel("Fill", fillArea.transform, new Color(0f, 0.82f, 1f, 0.9f));
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            Stretch(handleAreaRect, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            var handle = CreatePanel("Handle", handleArea.transform, new Color(1f, 0.94f, 0.26f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(18f, 28f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.onValueChanged.AddListener(_ => OnSettingsSliderChanged());

            items.Add(new MenuItem
            {
                Rect = rowRect,
                Background = rowBack,
                Slider = slider,
                Label = labelText,
                Track = track,
            });
        }

        private void OnSettingsSliderChanged()
        {
            if (controllerLookSlider != null)
            {
                ArenaUserSettings.SetControllerLookScale(controllerLookSlider.value);
            }

            if (musicSlider != null)
            {
                ArenaUserSettings.SetMusicVolume(musicSlider.value);
            }

            if (sfxSlider != null)
            {
                ArenaUserSettings.SetSfxVolume(sfxSlider.value);
            }

            ArenaAudio.Instance?.ApplySavedVolumes();
            UpdateSettingsLabels();
        }

        private void UpdateSettingsLabels()
        {
            if (controllerLookValueText != null && controllerLookSlider != null)
            {
                controllerLookValueText.text = controllerLookSlider.value.ToString("0.00") + "x";
            }

            if (musicValueText != null && musicSlider != null)
            {
                musicValueText.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
            }

            if (sfxValueText != null && sfxSlider != null)
            {
                sfxValueText.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
            }
        }

        private int ReadNavigationDirection()
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

        private SliderAdjustment ReadSliderAdjustment()
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
                var dpadX = gamepad.dpad.ReadValue().x;
                if (Mathf.Abs(dpadX) >= GamepadNavigationReleaseThreshold)
                {
                    return new SliderAdjustment(dpadX > 0f ? 1f : -1f, true);
                }

                var stickX = gamepad.leftStick.ReadValue().x;
                if (Mathf.Abs(stickX) >= GamepadNavigationReleaseThreshold && Mathf.Abs(stickX) > Mathf.Abs(axis))
                {
                    axis = stickX;
                }
            }

            axis = Mathf.Clamp(axis, -1f, 1f);
            return Mathf.Approximately(axis, 0f)
                ? SliderAdjustment.None
                : new SliderAdjustment(axis, false);
        }

        private void AdjustSlider(Slider slider, SliderAdjustment adjustment)
        {
            if (slider == null)
            {
                return;
            }

            if (adjustment.IsDpad)
            {
                if (sliderCooldown > 0f)
                {
                    return;
                }

                var step = slider == controllerLookSlider ? 0.05f : 0.05f;
                slider.value = Mathf.Clamp(slider.value + Mathf.Sign(adjustment.Value) * step, slider.minValue, slider.maxValue);
                sliderCooldown = SliderRepeatDelay;
                return;
            }

            var speed = slider == controllerLookSlider ? 1.3f : 0.75f;
            slider.value += adjustment.Value * speed * Time.unscaledDeltaTime;
        }

        private bool WasSubmitPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                return true;
            }

            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.buttonSouth.wasPressedThisFrame)
                {
                    return true;
                }

                foreach (var control in gamepad.allControls)
                {
                    if (control is ButtonControl button && button.wasPressedThisFrame && IsSubmitButton(button))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool WasBackPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }

            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.buttonEast.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleBack()
        {
            if (currentView == MenuView.Settings)
            {
                BuildMainView();
                return;
            }

            match?.ResumeGame();
        }

        private void ActivateItem(MenuItem item)
        {
            if (item == null || lastActivationFrame == Time.frameCount)
            {
                return;
            }

            lastActivationFrame = Time.frameCount;
            switch (item.Command)
            {
                case MenuCommand.ReturnToGame:
                    match?.ResumeGame();
                    break;
                case MenuCommand.RestartGame:
                    match?.RestartFromPauseMenu();
                    break;
                case MenuCommand.QuitGame:
                    match?.QuitToMainMenu();
                    break;
                case MenuCommand.Settings:
                    BuildSettingsView();
                    break;
                case MenuCommand.Back:
                    BuildMainView();
                    break;
            }
        }

        private bool TryHandlePointerClick()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            var pointerPosition = mouse.position.ReadValue();
            Canvas.ForceUpdateCanvases();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Rect == null || !RectTransformUtility.RectangleContainsScreenPoint(item.Rect, pointerPosition))
                {
                    continue;
                }

                SelectItem(i, false);
                if (item.Slider != null)
                {
                    SetSliderFromPointer(item.Slider, pointerPosition);
                    return true;
                }

                ActivateItem(item);
                return true;
            }

            return false;
        }

        private static void SetSliderFromPointer(Slider slider, Vector2 pointerPosition)
        {
            if (slider == null || slider.fillRect == null)
            {
                return;
            }

            var sliderRect = (RectTransform)slider.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(sliderRect, pointerPosition, null, out var localPoint))
            {
                var rect = sliderRect.rect;
                var normalized = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
                slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, normalized);
            }
        }

        private void MoveSelection(int direction)
        {
            if (items.Count == 0)
            {
                return;
            }

            var next = selectedIndex < 0 ? 0 : selectedIndex + direction;
            if (next < 0)
            {
                next = items.Count - 1;
            }
            else if (next >= items.Count)
            {
                next = 0;
            }

            SelectItem(next, false);
        }

        private void SelectItem(int index, bool immediate)
        {
            if (items.Count == 0)
            {
                selectedIndex = -1;
                return;
            }

            selectedIndex = Mathf.Clamp(index, 0, items.Count - 1);
            for (var i = 0; i < items.Count; i++)
            {
                items[i].IsSelected = i == selectedIndex;
                if (immediate)
                {
                    items[i].SelectionAnimationTime = SelectionPopDuration;
                }
                else if (items[i].IsSelected)
                {
                    items[i].SelectionAnimationTime = 0f;
                }
            }
        }

        private void UpdateItemVisuals()
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Rect == null || item.Background == null || item.Label == null)
                {
                    continue;
                }

                item.SelectionAnimationTime = Mathf.Min(SelectionPopDuration, item.SelectionAnimationTime + Time.unscaledDeltaTime);
                var selected = item.IsSelected;
                var progress = Mathf.Clamp01(item.SelectionAnimationTime / SelectionPopDuration);
                var pop = selected ? Mathf.Sin(progress * Mathf.PI) * 0.035f : 0f;
                item.Rect.localScale = Vector3.one * (selected ? 1.025f + pop : 1f);
                item.Background.color = selected
                    ? new Color(0.115f, 0.18f, 0.19f, 0.99f)
                    : new Color(0.045f, 0.065f, 0.078f, 0.96f);
                item.Label.color = selected
                    ? new Color(1f, 0.94f, 0.26f, 1f)
                    : new Color(0.82f, 1f, 1f, 1f);

                if (item.Track != null)
                {
                    item.Track.color = selected
                        ? new Color(0.035f, 0.11f, 0.13f, 0.70f)
                        : new Color(0.008f, 0.028f, 0.033f, 0.86f);
                }

                if (item.LeftAccent != null)
                {
                    item.LeftAccent.color = selected ? new Color(1f, 0.94f, 0.12f, 1f) : new Color(1f, 0.08f, 0.18f, 0.9f);
                }

                if (item.RightAccent != null)
                {
                    item.RightAccent.color = selected ? new Color(1f, 0.94f, 0.12f, 1f) : new Color(0f, 0.82f, 1f, 0.9f);
                }
            }
        }

        private static bool IsSubmitButton(ButtonControl button)
        {
            var name = button.name.ToLowerInvariant();
            var displayName = button.displayName.ToLowerInvariant();
            return name.Contains("buttonsouth")
                || name == "a"
                || displayName == "a"
                || displayName == "cross";
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>().sendNavigationEvents = false;
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private static Image CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
            return image;
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
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = size;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.74f);
            return label;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private sealed class MenuItem
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
            public float SelectionAnimationTime;
            public bool IsSelected;
        }

        private readonly struct SliderAdjustment
        {
            public static readonly SliderAdjustment None = new(0f, false);

            public readonly float Value;
            public readonly bool IsDpad;
            public bool HasInput => !Mathf.Approximately(Value, 0f);

            public SliderAdjustment(float value, bool isDpad)
            {
                Value = value;
                IsDpad = isDpad;
            }
        }
    }
}
