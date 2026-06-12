using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaShooter
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        private Text statusText;
        private Text opponentText;
        private Text centerText;
        private Text waveCountdownText;
        private Text healthValueText;
        private Text shieldValueText;
        private Text ammoValueText;
        private Text deployableText;
        private Text weaponNameText;
        private Text holsteredWeaponText;
        private Text grenadeCountText;
        private Text scrapValueText;
        private Text scrapLabelText;
        private Text fabricatorTitleText;
        private Text fabricatorTabText;
        private Text fabricatorScrapText;
        private Text fabricatorHelpText;
        private Canvas hudCanvas;
        private Camera hudCamera;
        private RectTransform safeAreaRoot;
        private RectTransform compositionRoot;
        private RectTransform healthPanelRect;
        private RectTransform ammoPanelRect;
        private RectTransform scrapPanelRect;
        private RectTransform allOutWarArmyPanelRect;
        private Rect lastSafeArea;
        private Rect lastPixelRect;
        private Image playerHealthFill;
        private Image playerShieldFill;
        private Image scrapCoreIcon;
        private Image weaponIcon;
        private Image healthAccent;
        private Image ammoAccent;
        private Image healthTrim;
        private Image ammoTrim;
        private Image crosshairTop;
        private Image crosshairBottom;
        private Image crosshairLeft;
        private Image crosshairRight;
        private Image damageOverlay;
        private GameObject fabricatorPanel;
        private readonly Image[] fabricatorRowBacks = new Image[6];
        private readonly Text[] fabricatorRowTexts = new Text[6];
        private readonly Text[] fabricatorCostTexts = new Text[6];
        private readonly List<Image> allOutWarArmyFills = new();
        private readonly List<Text> allOutWarArmyTexts = new();
        private readonly List<int> allOutWarArmyIds = new();
        private CombatantHealth playerHealth;
        private CombatantHealth opponentHealth;
        private WeaponInventory playerWeapons;
        private MatchController match;
        private Transform playerTransform;
        private Transform opponentTransform;
        private float opponentIntroHideAt;
        private float damageFlashAlpha;
        private string centerMessage = "";
        private string waveCountdownMessage = "";
        private bool allOutWarHudMode;
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const float CompositionAspect = 1280f / 536f;
        private const float HorizontalSafePadding = 0.018f;
        private const float VerticalSafePadding = 0.018f;
        private const float HealthPanelWidth = 378f;
        private const float HealthPanelHeight = 96f;
        private const float AmmoPanelWidth = 318f;
        private const float AmmoPanelHeight = 130f;
        private const float ScrapPanelWidth = 238f;
        private const float ScrapPanelHeight = 60f;
        private const float AllOutWarPanelWidth = 760f;
        private const float AllOutWarPanelHeight = 62f;
        private const float HudPanelScale = 0.78f;
        private const float HudCornerInset = 28f;

        public void Build(Camera hudCamera = null)
        {
            this.hudCamera = hudCamera;
            hudCanvas = gameObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            hudCanvas.overrideSorting = true;
            hudCanvas.sortingOrder = 1000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildSafeAreaRoot();
            BuildPlayerHealth();
            BuildAmmoPanel();
            BuildScrapPanel();
            BuildFabricatorPanel();
            BuildCrosshair();
            BuildDamageOverlay();

            statusText = CreateText("Status", new Vector2(18f, -106f), TextAnchor.UpperLeft, 15);
            statusText.enabled = false;
            opponentText = CreateText("Opponent Intro", new Vector2(18f, -154f), TextAnchor.UpperLeft, 15);
            opponentText.enabled = false;
            centerText = CreateText("Center Message", Vector2.zero, TextAnchor.MiddleCenter, 34);
            centerText.rectTransform.anchorMin = new Vector2(0.15f, 0.25f);
            centerText.rectTransform.anchorMax = new Vector2(0.85f, 0.75f);
            centerText.rectTransform.offsetMin = Vector2.zero;
            centerText.rectTransform.offsetMax = Vector2.zero;

            waveCountdownText = CreateText("Wave Countdown", new Vector2(0f, -26f), TextAnchor.UpperCenter, 20);
            waveCountdownText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            waveCountdownText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            waveCountdownText.rectTransform.pivot = new Vector2(0.5f, 1f);
            waveCountdownText.rectTransform.sizeDelta = new Vector2(520f, 40f);
            waveCountdownText.color = new Color(0.76f, 0.96f, 1f, 0.92f);
            waveCountdownText.text = "";
        }

        public void Bind(CombatantHealth player, WeaponInventory weapons, CombatantHealth opponent, Transform playerBody, Transform opponentBody)
        {
            playerHealth = player;
            playerHealth.Damaged += OnPlayerDamaged;
            playerWeapons = weapons;
            opponentHealth = opponent;
            playerTransform = playerBody;
            opponentTransform = opponentBody;
            opponentIntroHideAt = Time.time + 5f;
            opponentText.text = "";
        }

        public void BindWaveState(MatchController owner)
        {
            match = owner;
            allOutWarHudMode = match != null && match.IsAllOutWarMode;
            if (allOutWarHudMode)
            {
                SetAllOutWarMode(owner);
                return;
            }

            if (statusText != null)
            {
                statusText.enabled = true;
            }

            if (scrapPanelRect != null)
            {
                scrapPanelRect.gameObject.SetActive(true);
            }

            if (allOutWarArmyPanelRect != null)
            {
                allOutWarArmyPanelRect.gameObject.SetActive(false);
            }
        }

        public void SetAllOutWarMode(MatchController owner)
        {
            match = owner;
            allOutWarHudMode = true;
            if (statusText != null)
            {
                statusText.enabled = false;
                statusText.text = "";
            }

            if (scrapPanelRect != null)
            {
                scrapPanelRect.gameObject.SetActive(false);
            }

            if (fabricatorPanel != null)
            {
                fabricatorPanel.SetActive(false);
            }

            if (allOutWarArmyPanelRect != null)
            {
                allOutWarArmyPanelRect.gameObject.SetActive(false);
            }
        }

        public void SetCenterMessage(string message)
        {
            centerMessage = message;
            if (centerText != null)
            {
                centerText.text = message;
            }
        }

        public void SetWaveCountdown(string message)
        {
            waveCountdownMessage = message;
            if (waveCountdownText != null)
            {
                waveCountdownText.text = message;
            }
        }

        public void ShowFabricatorMenu(FabricatorTab tab, int selectedIndex, bool controller)
        {
            if (allOutWarHudMode)
            {
                return;
            }

            if (fabricatorPanel == null || match == null)
            {
                return;
            }

            fabricatorPanel.SetActive(true);
            fabricatorTitleText.text = "FABRICATOR";
            fabricatorTabText.text = tab == FabricatorTab.Buy ? "<  BUY  >" : "<  UPGRADE ARMY  >";
            fabricatorScrapText.text = $"SCRAP {match.Scrap:000}";
            fabricatorHelpText.text = controller
                ? "LB / RB TABS    LEFT STICK / D-PAD SELECT    Y CONFIRM    B CLOSE"
                : "Q / E TABS    UP / DOWN SELECT    F CONFIRM    ESC CLOSE";

            var rowCount = match.GetFabricatorRowCount(tab);
            for (var i = 0; i < fabricatorRowTexts.Length; i++)
            {
                var rowActive = i < rowCount;
                fabricatorRowBacks[i].gameObject.SetActive(rowActive);
                if (!rowActive)
                {
                    continue;
                }

                var selected = i == selectedIndex;
                var affordable = match.CanAffordFabricatorRow(tab, i);
                var available = match.IsFabricatorRowAvailable(tab, i);
                var rowColor = selected
                    ? new Color(0.08f, 0.42f, 0.58f, 0.9f)
                    : new Color(0.015f, 0.02f, 0.03f, 0.76f);
                if (!available)
                {
                    rowColor = new Color(0.06f, 0.06f, 0.07f, 0.66f);
                }

                fabricatorRowBacks[i].color = rowColor;
                fabricatorRowTexts[i].text = match.GetFabricatorRowName(tab, i);
                fabricatorRowTexts[i].color = available ? new Color(0.78f, 0.96f, 1f, 1f) : new Color(0.55f, 0.58f, 0.62f, 0.9f);
                fabricatorCostTexts[i].text = match.GetFabricatorRowCostLabel(tab, i);
                fabricatorCostTexts[i].color = available && affordable ? new Color(1f, 0.82f, 0.22f, 1f) : new Color(0.95f, 0.22f, 0.18f, 0.95f);
            }
        }

        public void HideFabricatorMenu()
        {
            if (fabricatorPanel != null)
            {
                fabricatorPanel.SetActive(false);
            }
        }

        public string GetDiagnosticsSummary()
        {
            var canvasMode = hudCanvas != null ? hudCanvas.renderMode.ToString() : "missing";
            var canvasScale = transform.localScale;
            var safeRect = safeAreaRoot != null ? safeAreaRoot.rect : default;
            var compositionRect = compositionRoot != null ? compositionRoot.rect : default;
            var healthPos = healthPanelRect != null ? healthPanelRect.anchoredPosition : default;
            var ammoPos = ammoPanelRect != null ? ammoPanelRect.anchoredPosition : default;
            var scrapPos = scrapPanelRect != null ? scrapPanelRect.anchoredPosition : default;
            return $"HUD: mode={canvasMode} scale={canvasScale} safe={FormatRect(safeRect)} composition={FormatRect(compositionRect)} health={healthPos} ammo={ammoPos} scrap={scrapPos}";
        }

        private static string FormatRect(Rect rect)
        {
            return $"({rect.x:0},{rect.y:0},{rect.width:0},{rect.height:0})";
        }

        private void Update()
        {
            ApplySafeArea();
            UpdateDamageOverlay();

            if (statusText != null && match != null && !allOutWarHudMode)
            {
                statusText.text = $"WAVE {match.CurrentWave}/{match.FinalWave}    DROIDS {match.ActiveDroidCount}    DMG +{match.WeaponUpgradeLevel}";
            }

            if (scrapValueText != null && match != null && !allOutWarHudMode)
            {
                scrapValueText.text = match.Scrap.ToString("000");
                if (scrapCoreIcon != null)
                {
                    var pulse = 0.76f + Mathf.Sin(Time.time * 4.5f) * 0.1f;
                    scrapCoreIcon.color = new Color(0.82f, 0.95f, 1f, pulse);
                }
            }

            if (deployableText != null && match != null)
            {
                if (match.TurretKits > 0)
                {
                    deployableText.text = match.IsTurretPlacementMode
                        ? $"TURRET KIT x{match.TurretKits}  SELECTED"
                        : $"TURRET KIT x{match.TurretKits}  4 / D-PAD DOWN";
                    deployableText.color = match.IsTurretPlacementMode
                        ? new Color(0.82f, 0.96f, 1f, 1f)
                        : new Color(0.55f, 0.9f, 1f, 0.84f);
                }
                else
                {
                    deployableText.text = "NO DEPLOYABLE";
                    deployableText.color = new Color(0.28f, 0.36f, 0.4f, 0.72f);
                }
            }

            if (playerHealth != null && playerWeapons != null)
            {
                var healthRatio = Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.MaxHealth);
                var shieldRatio = playerHealth.MaxShield <= 0f ? 0f : Mathf.Clamp01(playerHealth.CurrentShield / playerHealth.MaxShield);

                playerHealthFill.rectTransform.anchorMax = new Vector2(healthRatio, 1f);
                if (playerShieldFill != null)
                {
                    playerShieldFill.rectTransform.anchorMax = new Vector2(shieldRatio, 1f);
                }

                healthValueText.text = $"HP {Mathf.CeilToInt(playerHealth.CurrentHealth)}";
                if (shieldValueText != null)
                {
                    shieldValueText.text = $"SHD {Mathf.CeilToInt(playerHealth.CurrentShield)}";
                }

                ammoValueText.text = playerWeapons.HasWeapon ? playerWeapons.Ammo.ToString("000") : "---";
                if (weaponNameText != null)
                {
                    weaponNameText.text = playerWeapons.HasWeapon ? playerWeapons.WeaponName.ToUpperInvariant() : "UNARMED";
                }

                if (holsteredWeaponText != null)
                {
                    var holstered = playerWeapons.HolsteredWeaponName;
                    holsteredWeaponText.text = holstered != null ? $"[X] SWAP: {holstered.ToUpperInvariant()}" : "";
                }

                if (grenadeCountText != null)
                {
                    var grenades = playerWeapons.GrenadeCount;
                    grenadeCountText.text = grenades > 0 ? $"[G] GRENADES x{grenades}" : "";
                    grenadeCountText.color = grenades > 0
                        ? new Color(1f, 0.62f, 0.12f, 0.95f)
                        : new Color(0.32f, 0.4f, 0.44f, 0.7f);
                }

                var healthColor = healthRatio <= 0.28f ? new Color(1f, 0.04f, 0.04f, 1f) : new Color(0.92f, 0.05f, 0.08f, 0.98f);
                var shieldColor = shieldRatio > 0f ? new Color(0.1f, 0.62f, 1f, 0.96f) : new Color(0.02f, 0.08f, 0.13f, 0.38f);
                var ammoLow = playerWeapons.HasWeapon && playerWeapons.Ammo <= Mathf.Max(3, Mathf.CeilToInt(playerWeapons.MaxAmmo * 0.2f));
                var ammoColor = ammoLow ? new Color(1f, 0.32f, 0.08f, 1f) : new Color(1f, 0.78f, 0.18f, 0.98f);
                playerHealthFill.color = healthColor;
                if (playerShieldFill != null)
                {
                    playerShieldFill.color = shieldColor;
                }

                SetAlpha(healthAccent, healthRatio <= 0.28f ? 1f : 0.95f);
                SetAlpha(ammoAccent, playerWeapons.HasWeapon ? 1f : 0.72f);
                SetAlpha(healthTrim, healthRatio <= 0.28f ? 1f : 0.78f);
                SetAlpha(ammoTrim, playerWeapons.HasWeapon ? 0.95f : 0.4f);
                SetImageColor(weaponIcon, playerWeapons.HasWeapon ? new Color(0.72f, 0.95f, 1f, 0.92f) : new Color(0.12f, 0.16f, 0.19f, 0.5f));
                ammoValueText.color = playerWeapons.HasWeapon ? ammoColor : new Color(0.32f, 0.4f, 0.44f, 0.7f);
            }

            if (opponentText != null && Time.time > opponentIntroHideAt)
            {
                opponentText.text = "";
            }

            if (centerText != null)
            {
                centerText.text = centerMessage;
            }

            if (waveCountdownText != null)
            {
                waveCountdownText.text = waveCountdownMessage;
            }
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.Damaged -= OnPlayerDamaged;
            }
        }

        private void OnPlayerDamaged(CombatantHealth damagedHealth)
        {
            damageFlashAlpha = 0.5f;
            ArenaAudio.Instance?.PlayPlayerHit(playerTransform != null ? playerTransform.position : Vector3.zero);
        }

        public void SetAiming(bool aiming)
        {
            SetCrosshairAlpha(0.9f);
        }

        private void BuildPlayerHealth()
        {
            var panel = CreatePanel("Health Panel", new Vector2(24f, 24f), new Vector2(HealthPanelWidth, HealthPanelHeight), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            var panelBack = panel.GetComponent<Image>();
            panelBack.color = new Color(0.004f, 0.008f, 0.014f, 0.72f);

            AddCyberFrame(panel.transform, HealthPanelWidth, HealthPanelHeight, new Color(0.96f, 0.08f, 0.12f, 0.92f), new Color(0.07f, 0.75f, 1f, 0.7f), false);
            healthAccent = CreateImage("Health Red Side Pulse", panel.transform, new Color(0.96f, 0.05f, 0.09f, 0.9f), new Vector2(17f, 18f), new Vector2(5f, 60f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            healthTrim = CreateImage("Health Top Glow", panel.transform, new Color(0.07f, 0.75f, 1f, 0.55f), new Vector2(46f, -13f), new Vector2(254f, 2f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            var label = CreateTextInPanel("Vitals Label", panel.transform, "VITALS", new Vector2(32f, -12f), TextAnchor.UpperLeft, 13, new Vector2(92f, 20f));
            label.color = new Color(0.54f, 0.92f, 1f, 0.86f);
            healthValueText = CreateTextInPanel("Health Value", panel.transform, "HP 100", new Vector2(-24f, -52f), TextAnchor.UpperRight, 16, new Vector2(86f, 24f));
            shieldValueText = CreateTextInPanel("Shield Value", panel.transform, "SHD 0", new Vector2(-24f, -27f), TextAnchor.UpperRight, 15, new Vector2(86f, 22f));
            shieldValueText.color = new Color(0.55f, 0.9f, 1f, 0.92f);

            var shieldBack = CreateImage("Shield Meter Back", panel.transform, new Color(0.006f, 0.035f, 0.065f, 0.78f), new Vector2(32f, -33f), new Vector2(244f, 14f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            playerShieldFill = CreateImage("Shield Meter Fill", shieldBack.transform, new Color(0.1f, 0.62f, 1f, 0.96f), Vector2.zero, Vector2.zero, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            playerShieldFill.rectTransform.offsetMin = Vector2.zero;
            playerShieldFill.rectTransform.offsetMax = Vector2.zero;

            var barBack = CreateImage("Health Live Meter Back", panel.transform, new Color(0.055f, 0f, 0.006f, 0.82f), new Vector2(32f, -60f), new Vector2(244f, 16f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            playerHealthFill = CreateImage("Health Live Meter Fill", barBack.transform, new Color(0.92f, 0.05f, 0.08f, 0.98f), Vector2.zero, Vector2.zero, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f));
            playerHealthFill.rectTransform.offsetMin = Vector2.zero;
            playerHealthFill.rectTransform.offsetMax = Vector2.zero;
        }

        private void BuildAmmoPanel()
        {
            var panel = CreatePanel("Ammo Panel", new Vector2(-24f, 24f), new Vector2(AmmoPanelWidth, AmmoPanelHeight), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            var panelBack = panel.GetComponent<Image>();
            panelBack.color = new Color(0.004f, 0.008f, 0.014f, 0.72f);

            AddCyberFrame(panel.transform, AmmoPanelWidth, AmmoPanelHeight, new Color(1f, 0.72f, 0.16f, 0.88f), new Color(0.08f, 0.82f, 1f, 0.62f), true);
            ammoAccent = CreateImage("Ammo Amber Side Pulse", panel.transform, new Color(1f, 0.72f, 0.16f, 0.9f), new Vector2(-18f, 18f), new Vector2(5f, 56f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            ammoTrim = CreateImage("Ammo Top Glow", panel.transform, new Color(0.08f, 0.82f, 1f, 0.52f), new Vector2(-42f, -13f), new Vector2(214f, 2f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            weaponNameText = CreateTextInPanel("Ammo Label", panel.transform, "UNARMED", new Vector2(-24f, -14f), TextAnchor.UpperRight, 13, new Vector2(190f, 20f));
            weaponNameText.color = new Color(0.54f, 0.92f, 1f, 0.82f);
            weaponIcon = CreateArtworkImage("Equipped Weapon Silhouette", panel.transform, "UI/HudPistolIcon", new Vector2(-184f, -51f), new Vector2(122f, 58f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            ammoValueText = CreateTextInPanel("Ammo Value", panel.transform, "---", new Vector2(-24f, -42f), TextAnchor.UpperRight, 36, new Vector2(118f, 42f));
            holsteredWeaponText = CreateTextInPanel("Holstered Weapon", panel.transform, "", new Vector2(-24f, -80f), TextAnchor.UpperRight, 12, new Vector2(260f, 18f));
            holsteredWeaponText.color = new Color(0.42f, 0.62f, 0.72f, 0.8f);
            grenadeCountText = CreateTextInPanel("Grenade Count", panel.transform, "", new Vector2(-24f, -96f), TextAnchor.UpperRight, 12, new Vector2(260f, 18f));
            grenadeCountText.color = new Color(0.32f, 0.4f, 0.44f, 0.7f);
            deployableText = CreateTextInPanel("Deployable Slot", panel.transform, "NO DEPLOYABLE", new Vector2(-24f, -112f), TextAnchor.UpperRight, 12, new Vector2(210f, 18f));
            deployableText.color = new Color(0.28f, 0.36f, 0.4f, 0.72f);
        }

        private void BuildScrapPanel()
        {
            var panel = CreatePanel("Scrap Panel", new Vector2(-28f, -26f), new Vector2(ScrapPanelWidth, ScrapPanelHeight), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            scrapPanelRect = panel.GetComponent<RectTransform>();
            var back = panel.GetComponent<Image>();
            back.color = new Color(0.004f, 0.008f, 0.014f, 0.72f);

            AddCyberFrame(panel.transform, ScrapPanelWidth, ScrapPanelHeight, new Color(0.08f, 0.82f, 1f, 0.82f), new Color(1f, 0.72f, 0.16f, 0.72f), true);
            CreateImage("Scrap Inner Rail", panel.transform, new Color(0.08f, 0.82f, 1f, 0.64f), new Vector2(-204f, -11f), new Vector2(3f, 38f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            scrapCoreIcon = CreateImage("Scrap Core Icon", panel.transform, new Color(0.82f, 0.95f, 1f, 0.82f), new Vector2(-178f, -30f), new Vector2(30f, 30f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            scrapCoreIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            scrapLabelText = CreateTextInPanel("Scrap Label", panel.transform, "SCRAP", new Vector2(-138f, -11f), TextAnchor.UpperLeft, 13, new Vector2(80f, 22f));
            scrapLabelText.color = new Color(0.52f, 0.86f, 1f, 0.86f);
            scrapValueText = CreateTextInPanel("Scrap Value", panel.transform, "000", new Vector2(-18f, -17f), TextAnchor.UpperRight, 28, new Vector2(98f, 34f));
            scrapValueText.color = new Color(1f, 0.84f, 0.25f, 1f);
        }

        private void EnsureAllOutWarArmyPanel()
        {
            if (allOutWarArmyPanelRect != null)
            {
                allOutWarArmyPanelRect.gameObject.SetActive(true);
                return;
            }

            var panel = CreatePanel("All Out War Army Status", new Vector2(0f, -20f), new Vector2(AllOutWarPanelWidth, AllOutWarPanelHeight), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            allOutWarArmyPanelRect = panel.GetComponent<RectTransform>();
            var back = panel.GetComponent<Image>();
            back.color = new Color(0.004f, 0.008f, 0.016f, 0.68f);
            CreateImage("Army Status Top Rail", panel.transform, new Color(0.08f, 0.82f, 1f, 0.42f), new Vector2(0f, -7f), new Vector2(AllOutWarPanelWidth - 28f, 2f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            CreateImage("Army Status Bottom Rail", panel.transform, new Color(1f, 0.35f, 0.8f, 0.36f), new Vector2(0f, 7f), new Vector2(AllOutWarPanelWidth - 28f, 2f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        }

        private void RebuildAllOutWarArmyBars()
        {
            EnsureAllOutWarArmyPanel();
            var armyCount = match != null ? Mathf.Clamp(match.AllOutWarArmyCount, 0, 8) : 0;
            if (armyCount == allOutWarArmyIds.Count)
            {
                return;
            }

            for (var i = allOutWarArmyPanelRect.childCount - 1; i >= 0; i--)
            {
                var child = allOutWarArmyPanelRect.GetChild(i);
                if (child.name.StartsWith("Army Status Top Rail") || child.name.StartsWith("Army Status Bottom Rail"))
                {
                    continue;
                }

                Destroy(child.gameObject);
            }

            allOutWarArmyFills.Clear();
            allOutWarArmyTexts.Clear();
            allOutWarArmyIds.Clear();

            if (armyCount <= 0)
            {
                return;
            }

            const float gap = 6f;
            for (var army = 0; army < armyCount; army++)
            {
                var minX = army / (float)armyCount;
                var maxX = (army + 1) / (float)armyCount;
                var accent = GetAllOutWarArmyAccent(army);
                var barBack = CreateImage($"Army {army} Status Back", allOutWarArmyPanelRect, new Color(0.012f, 0.016f, 0.026f, 0.86f), Vector2.zero, Vector2.zero, new Vector2(minX, 0.18f), new Vector2(maxX, 0.82f), new Vector2(0.5f, 0.5f));
                barBack.rectTransform.offsetMin = new Vector2(gap, 0f);
                barBack.rectTransform.offsetMax = new Vector2(-gap, 0f);

                var fill = CreateImage($"Army {army} Status Fill", barBack.transform, accent, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(1f, 1f), new Vector2(0f, 0.5f));
                fill.rectTransform.offsetMin = Vector2.zero;
                fill.rectTransform.offsetMax = Vector2.zero;

                var line = CreateImage($"Army {army} Status Accent", barBack.transform, new Color(accent.r, accent.g, accent.b, 0.95f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
                line.rectTransform.sizeDelta = new Vector2(3f, 0f);
                line.rectTransform.offsetMin = Vector2.zero;
                line.rectTransform.offsetMax = new Vector2(3f, 0f);

                var label = CreateTextInPanel($"Army {army} Status Text", barBack.transform, "", Vector2.zero, TextAnchor.MiddleCenter, 13, Vector2.zero);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
                label.color = army == 0 ? new Color(0.78f, 0.96f, 1f, 1f) : new Color(0.95f, 0.9f, 0.82f, 0.96f);

                allOutWarArmyFills.Add(fill);
                allOutWarArmyTexts.Add(label);
                allOutWarArmyIds.Add(army);
            }
        }

        private void UpdateAllOutWarArmyBars()
        {
            if (match == null)
            {
                return;
            }

            if (allOutWarArmyPanelRect == null || allOutWarArmyIds.Count != Mathf.Clamp(match.AllOutWarArmyCount, 0, 8))
            {
                RebuildAllOutWarArmyBars();
            }

            for (var i = 0; i < allOutWarArmyIds.Count; i++)
            {
                var army = allOutWarArmyIds[i];
                var remaining = match.GetAllOutWarArmyRemaining(army);
                var starting = Mathf.Max(1, match.GetAllOutWarArmyStartingCount(army));
                var ratio = Mathf.Clamp01(remaining / (float)starting);
                if (i < allOutWarArmyFills.Count && allOutWarArmyFills[i] != null)
                {
                    allOutWarArmyFills[i].rectTransform.anchorMax = new Vector2(ratio, 1f);
                    var accent = GetAllOutWarArmyAccent(army);
                    allOutWarArmyFills[i].color = new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0.22f, accent.a, ratio));
                }

                if (i < allOutWarArmyTexts.Count && allOutWarArmyTexts[i] != null)
                {
                    allOutWarArmyTexts[i].text = $"ARMY {army}  {remaining}";
                }
            }
        }

        private static Color GetAllOutWarArmyAccent(int army)
        {
            return AllOutWarArmyVisuals.GetAccent(army);
        }

        private void BuildFabricatorPanel()
        {
            fabricatorPanel = CreatePanel("Fabricator Menu Panel", Vector2.zero, new Vector2(590f, 434f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var panelImage = fabricatorPanel.GetComponent<Image>();
            panelImage.color = new Color(0.005f, 0.01f, 0.016f, 0.88f);

            CreateImage("Fabricator Top Rail", fabricatorPanel.transform, new Color(0.06f, 0.78f, 1f, 0.82f), new Vector2(0f, -18f), new Vector2(528f, 4f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            CreateImage("Fabricator Amber Rail", fabricatorPanel.transform, new Color(1f, 0.72f, 0.16f, 0.82f), new Vector2(0f, -412f), new Vector2(528f, 4f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            fabricatorTitleText = CreateTextInPanel("Fabricator Title", fabricatorPanel.transform, "FABRICATOR", new Vector2(28f, -32f), TextAnchor.UpperLeft, 28, new Vector2(250f, 36f));
            fabricatorTitleText.color = new Color(0.76f, 0.96f, 1f, 1f);
            fabricatorTabText = CreateTextInPanel("Fabricator Tab", fabricatorPanel.transform, "<  BUY  >", new Vector2(0f, -56f), TextAnchor.UpperCenter, 16, new Vector2(260f, 24f));
            fabricatorTabText.color = new Color(1f, 0.82f, 0.22f, 0.96f);
            fabricatorScrapText = CreateTextInPanel("Fabricator Scrap", fabricatorPanel.transform, "SCRAP 000", new Vector2(-28f, -36f), TextAnchor.UpperRight, 22, new Vector2(180f, 34f));
            fabricatorScrapText.color = new Color(1f, 0.84f, 0.25f, 1f);

            for (var i = 0; i < fabricatorRowTexts.Length; i++)
            {
                var y = -96f - i * 46f;
                fabricatorRowBacks[i] = CreateImage($"Fabricator Row {i + 1}", fabricatorPanel.transform, new Color(0.015f, 0.02f, 0.03f, 0.76f), new Vector2(0f, y), new Vector2(528f, 40f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                CreateImage($"Fabricator Row {i + 1} Notch", fabricatorRowBacks[i].transform, new Color(0.05f, 0.78f, 1f, 0.88f), new Vector2(10f, -19f), new Vector2(6f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
                fabricatorRowTexts[i] = CreateTextInPanel($"Fabricator Option {i + 1}", fabricatorRowBacks[i].transform, "", new Vector2(26f, -6f), TextAnchor.UpperLeft, 18, new Vector2(300f, 28f));
                fabricatorCostTexts[i] = CreateTextInPanel($"Fabricator Cost {i + 1}", fabricatorRowBacks[i].transform, "", new Vector2(-18f, -7f), TextAnchor.UpperRight, 16, new Vector2(150f, 28f));
            }

            fabricatorHelpText = CreateTextInPanel("Fabricator Help", fabricatorPanel.transform, "", new Vector2(0f, -384f), TextAnchor.UpperCenter, 15, new Vector2(540f, 28f));
            fabricatorHelpText.color = new Color(0.58f, 0.9f, 1f, 0.92f);
            fabricatorPanel.SetActive(false);
        }

        private void BuildCrosshair()
        {
            crosshairTop = CreateCrosshairSegment("Crosshair Top", new Vector2(0f, 12f), new Vector2(2f, 14f));
            crosshairBottom = CreateCrosshairSegment("Crosshair Bottom", new Vector2(0f, -12f), new Vector2(2f, 14f));
            crosshairLeft = CreateCrosshairSegment("Crosshair Left", new Vector2(-12f, 0f), new Vector2(14f, 2f));
            crosshairRight = CreateCrosshairSegment("Crosshair Right", new Vector2(12f, 0f), new Vector2(14f, 2f));
        }

        private void BuildDamageOverlay()
        {
            damageOverlay = CreateImage("Damage Overlay", transform, new Color(1f, 0f, 0.04f, 0f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            damageOverlay.rectTransform.offsetMin = Vector2.zero;
            damageOverlay.rectTransform.offsetMax = Vector2.zero;
            damageOverlay.transform.SetAsLastSibling();
        }

        private void BuildSafeAreaRoot()
        {
            var safeObject = new GameObject("HUD Safe Area");
            safeObject.transform.SetParent(transform, false);
            safeAreaRoot = safeObject.AddComponent<RectTransform>();
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;

            var compositionObject = new GameObject("HUD 16x9 Composition");
            compositionObject.transform.SetParent(safeAreaRoot, false);
            compositionRoot = compositionObject.AddComponent<RectTransform>();
            compositionRoot.anchorMin = new Vector2(0.5f, 0.5f);
            compositionRoot.anchorMax = new Vector2(0.5f, 0.5f);
            compositionRoot.pivot = new Vector2(0.5f, 0.5f);
            ApplySafeArea(true);
        }

        private void ApplySafeArea(bool force = false)
        {
            if (safeAreaRoot == null)
            {
                return;
            }

            var viewportRect = GetHudViewportRect();
            if (!force && viewportRect == lastPixelRect)
            {
                UpdatePanelLayout();
                return;
            }

            lastPixelRect = viewportRect;
            lastSafeArea = Screen.safeArea;
            ApplyFixedAnchors();
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            UpdatePanelLayout();
        }

        private void ApplyFixedAnchors()
        {
            safeAreaRoot.anchorMin = new Vector2(HorizontalSafePadding, VerticalSafePadding);
            safeAreaRoot.anchorMax = new Vector2(1f - HorizontalSafePadding, 1f - VerticalSafePadding);
        }

        private Rect GetHudViewportRect()
        {
            var safeArea = Screen.safeArea;
            return safeArea.width > 1f && safeArea.height > 1f ? safeArea : new Rect(0f, 0f, Screen.width, Screen.height);
        }

        private void UpdatePanelLayout()
        {
            if (safeAreaRoot == null)
            {
                return;
            }

            UpdateCompositionRoot();
            var panelScale = HudPanelScale;

            if (healthPanelRect != null)
            {
                healthPanelRect.anchorMin = new Vector2(0f, 0f);
                healthPanelRect.anchorMax = new Vector2(0f, 0f);
                healthPanelRect.pivot = new Vector2(0f, 0f);
                healthPanelRect.localScale = Vector3.one * panelScale;
                healthPanelRect.sizeDelta = new Vector2(HealthPanelWidth, HealthPanelHeight);
                healthPanelRect.anchoredPosition = new Vector2(HudCornerInset, HudCornerInset);
            }

            if (ammoPanelRect != null)
            {
                ammoPanelRect.anchorMin = new Vector2(1f, 0f);
                ammoPanelRect.anchorMax = new Vector2(1f, 0f);
                ammoPanelRect.pivot = new Vector2(1f, 0f);
                ammoPanelRect.localScale = Vector3.one * panelScale;
                ammoPanelRect.sizeDelta = new Vector2(AmmoPanelWidth, AmmoPanelHeight);
                ammoPanelRect.anchoredPosition = new Vector2(-HudCornerInset, HudCornerInset);
            }

            if (scrapPanelRect != null)
            {
                scrapPanelRect.anchorMin = new Vector2(1f, 1f);
                scrapPanelRect.anchorMax = new Vector2(1f, 1f);
                scrapPanelRect.pivot = new Vector2(1f, 1f);
                scrapPanelRect.localScale = Vector3.one * panelScale;
                scrapPanelRect.sizeDelta = new Vector2(ScrapPanelWidth, ScrapPanelHeight);
                scrapPanelRect.anchoredPosition = new Vector2(-HudCornerInset, -HudCornerInset);
            }
        }

        private void UpdateCompositionRoot()
        {
            if (compositionRoot == null || safeAreaRoot == null)
            {
                return;
            }

            var parentRect = safeAreaRoot.rect;
            var width = Mathf.Max(1f, parentRect.width);
            var height = Mathf.Max(1f, parentRect.height);
            var targetAspect = CompositionAspect;
            var compositionWidth = width;
            var compositionHeight = height;
            if (width / height > targetAspect)
            {
                compositionWidth = height * targetAspect;
            }
            else
            {
                compositionHeight = width / targetAspect;
            }

            compositionRoot.sizeDelta = new Vector2(compositionWidth, compositionHeight);
            compositionRoot.anchoredPosition = Vector2.zero;
        }

        private void UpdateDamageOverlay()
        {
            if (damageOverlay == null)
            {
                return;
            }

            damageFlashAlpha = Mathf.MoveTowards(damageFlashAlpha, 0f, Time.deltaTime * 1.85f);
            var pulse = damageFlashAlpha + Mathf.Sin(Time.time * 24f) * damageFlashAlpha * 0.12f;
            damageOverlay.color = new Color(1f, 0f, 0.04f, Mathf.Clamp01(pulse));
        }

        private void AddCyberFrame(Transform parent, float width, float height, Color primary, Color secondary, bool mirrored)
        {
            CreateImage("Frame Top Rail", parent, primary, new Vector2(18f, -7f), new Vector2(width - 54f, 2f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            CreateImage("Frame Bottom Rail", parent, primary, new Vector2(18f, 7f), new Vector2(width - 54f, 2f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            CreateImage("Frame Left Rail", parent, secondary, new Vector2(7f, 15f), new Vector2(2f, height - 30f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            CreateImage("Frame Right Rail", parent, secondary, new Vector2(-7f, 15f), new Vector2(2f, height - 30f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));

            var topSlash = CreateImage("Frame Top Slash", parent, secondary, mirrored ? new Vector2(-22f, -16f) : new Vector2(22f, -16f), new Vector2(36f, 3f), mirrored ? Vector2.one : new Vector2(0f, 1f), mirrored ? Vector2.one : new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
            topSlash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, mirrored ? -34f : 34f);

            var bottomSlash = CreateImage("Frame Bottom Slash", parent, primary, mirrored ? new Vector2(-22f, 16f) : new Vector2(22f, 16f), new Vector2(36f, 3f), mirrored ? new Vector2(1f, 0f) : Vector2.zero, mirrored ? new Vector2(1f, 0f) : Vector2.zero, new Vector2(0.5f, 0.5f));
            bottomSlash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, mirrored ? 34f : -34f);
        }

        private Image CreateCrosshairSegment(string objectName, Vector2 anchoredPosition, Vector2 size)
        {
            return CreateImage(objectName, transform, new Color(0.72f, 0.96f, 1f, 0.9f), anchoredPosition, size, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        private void SetCrosshairAlpha(float alpha)
        {
            SetAlpha(crosshairTop, alpha);
            SetAlpha(crosshairBottom, alpha);
            SetAlpha(crosshairLeft, alpha);
            SetAlpha(crosshairRight, alpha);
        }

        private void SetAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private void SetImageColor(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.color = color;
        }

        private Text CreateText(string textName, Vector2 anchoredPosition, TextAnchor alignment, int size)
        {
            var textObject = new GameObject(textName);
            textObject.transform.SetParent(compositionRoot != null ? compositionRoot : safeAreaRoot != null ? safeAreaRoot : transform, false);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            }

            text.fontSize = size;
            text.color = new Color(0.74f, 0.96f, 1f);
            text.alignment = alignment;
            text.raycastTarget = false;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(520f, 180f);

            return text;
        }

        private GameObject CreatePanel(string objectName, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var panel = new GameObject(objectName);
            panel.transform.SetParent(compositionRoot != null ? compositionRoot : safeAreaRoot != null ? safeAreaRoot : transform, false);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.01f, 0.012f, 0.018f, 0.72f);
            image.raycastTarget = false;
            image.rectTransform.anchorMin = anchorMin;
            image.rectTransform.anchorMax = anchorMax;
            image.rectTransform.pivot = pivot;
            image.rectTransform.anchoredPosition = anchoredPosition;
            image.rectTransform.sizeDelta = size;
            if (objectName == "Health Panel")
            {
                healthPanelRect = image.rectTransform;
            }
            else if (objectName == "Ammo Panel")
            {
                ammoPanelRect = image.rectTransform;
            }

            UpdatePanelLayout();
            return panel;
        }

        private Image CreateImage(string objectName, Transform parent, Color color, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var imageObject = new GameObject(objectName);
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = anchorMin;
            image.rectTransform.anchorMax = anchorMax;
            image.rectTransform.pivot = pivot;
            image.rectTransform.anchoredPosition = anchoredPosition;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private Image CreateArtworkImage(string objectName, Transform parent, string resourcePath, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var image = CreateImage(objectName, parent, Color.white, anchoredPosition, size, anchorMin, anchorMax, pivot);
            image.sprite = LoadArtworkSprite(resourcePath);
            if (image.sprite == null)
            {
                image.color = Color.clear;
            }
            image.preserveAspect = true;
            return image;
        }

        private Sprite LoadArtworkSprite(string resourcePath)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private Text CreateTextInPanel(string textName, Transform parent, string value, Vector2 anchoredPosition, TextAnchor alignment, int size, Vector2 textSize)
        {
            var textObject = new GameObject(textName);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            }

            text.fontSize = size;
            text.color = new Color(0.74f, 0.96f, 1f);
            text.alignment = alignment;
            text.raycastTarget = false;
            var anchorX = alignment == TextAnchor.UpperRight ? 1f : alignment == TextAnchor.UpperCenter ? 0.5f : 0f;
            text.rectTransform.anchorMin = new Vector2(anchorX, 1f);
            text.rectTransform.anchorMax = text.rectTransform.anchorMin;
            text.rectTransform.pivot = new Vector2(anchorX, 1f);
            text.rectTransform.anchoredPosition = anchoredPosition;
            text.rectTransform.sizeDelta = textSize;
            return text;
        }

        private string GetOpponentBearing()
        {
            var toOpponent = opponentTransform.position - playerTransform.position;
            toOpponent.y = 0f;
            if (toOpponent.sqrMagnitude < 0.01f)
            {
                return "near";
            }

            toOpponent.Normalize();
            var forward = Vector3.Dot(playerTransform.forward, toOpponent);
            var right = Vector3.Dot(playerTransform.right, toOpponent);

            if (forward > 0.65f)
            {
                return "ahead";
            }

            if (forward < -0.65f)
            {
                return "behind";
            }

            return right > 0f ? "right" : "left";
        }
    }
}
