using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaShooter
{
    public sealed class FabricatorStation : MonoBehaviour
    {
        private MatchController match;
        private ArenaTheme theme;
        private Transform rotor;
        private bool playerInside;
        private bool hintWasController;
        private bool menuOpen;
        private float nextStickSelectAt;
        private FabricatorTab selectedTab;
        private int selectedIndex;

        public void Configure(MatchController owner, ArenaTheme arenaTheme)
        {
            match = owner;
            theme = arenaTheme;
            BuildVisuals();
        }

        private void Update()
        {
            if (rotor != null)
            {
                rotor.Rotate(0f, 42f * Time.deltaTime, 0f, Space.Self);
            }

            if (!playerInside)
            {
                return;
            }

            RefreshInteractionHint();
            var pressedKeyboardUse = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            var pressedControllerUse = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
            var pressedKeyboardClose = Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame);
            var pressedControllerClose = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;

            if (menuOpen && (pressedKeyboardClose || pressedControllerClose))
            {
                CloseMenu();
                return;
            }

            if (pressedKeyboardUse || pressedControllerUse)
            {
                if (!menuOpen)
                {
                    OpenMenu();
                }
                else
                {
                    match?.TryBuyFabricatorSelection(selectedTab, selectedIndex);
                    ClampSelection();
                    match?.ShowFabricatorMenu(selectedTab, selectedIndex, hintWasController);
                }
            }

            if (!menuOpen)
            {
                return;
            }

            HandleSelectionInput();
            match?.ShowFabricatorMenu(selectedTab, selectedIndex, hintWasController);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerFpsController>() == null)
            {
                return;
            }

            playerInside = true;
            hintWasController = HasController();
            menuOpen = false;
            match?.HideFabricatorMenu();
            match?.SetInteractionHint(BuildOpenHint(hintWasController));
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerFpsController>() == null)
            {
                return;
            }

            playerInside = false;
            CloseMenu();
            match?.ClearInteractionHint();
        }

        private void RefreshInteractionHint()
        {
            var controller = HasController();
            if (controller == hintWasController)
            {
                return;
            }

            hintWasController = controller;
            if (menuOpen)
            {
                match?.ShowFabricatorMenu(selectedTab, selectedIndex, controller);
            }
            else
            {
                match?.SetInteractionHint(BuildOpenHint(controller));
            }
        }

        private void HandleSelectionInput()
        {
            var previousTab = selectedTab;
            var previousIndex = selectedIndex;
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            var stickY = gamepad != null ? gamepad.leftStick.ReadValue().y : 0f;
            if ((keyboard != null && keyboard.qKey.wasPressedThisFrame) ||
                (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame))
            {
                selectedTab = selectedTab == FabricatorTab.Buy ? FabricatorTab.UpgradeArmy : FabricatorTab.Buy;
                selectedIndex = 0;
            }
            else if ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                     (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame))
            {
                selectedTab = selectedTab == FabricatorTab.Buy ? FabricatorTab.UpgradeArmy : FabricatorTab.Buy;
                selectedIndex = 0;
            }
            
            if ((keyboard != null && keyboard.upArrowKey.wasPressedThisFrame) ||
                (gamepad != null && (gamepad.dpad.up.wasPressedThisFrame || (stickY > 0.58f && Time.time >= nextStickSelectAt))))
            {
                selectedIndex--;
                nextStickSelectAt = Time.time + 0.24f;
            }
            else if ((keyboard != null && keyboard.downArrowKey.wasPressedThisFrame) ||
                     (gamepad != null && (gamepad.dpad.down.wasPressedThisFrame || (stickY < -0.58f && Time.time >= nextStickSelectAt))))
            {
                selectedIndex++;
                nextStickSelectAt = Time.time + 0.24f;
            }

            ClampSelection();
            if (selectedTab != previousTab || selectedIndex != previousIndex)
            {
                match?.ShowFabricatorMenu(selectedTab, selectedIndex, hintWasController);
            }
        }

        private void ClampSelection()
        {
            var count = match != null ? match.GetFabricatorRowCount(selectedTab) : 1;
            if (count <= 0)
            {
                selectedIndex = 0;
                return;
            }

            if (selectedIndex < 0)
            {
                selectedIndex = count - 1;
            }
            else if (selectedIndex >= count)
            {
                selectedIndex = 0;
            }
        }

        private static bool HasController()
        {
            return Gamepad.current != null;
        }

        private void OpenMenu()
        {
            menuOpen = true;
            match?.ClearInteractionHint();
            ClampSelection();
            match?.ShowFabricatorMenu(selectedTab, selectedIndex, hintWasController);
        }

        private void CloseMenu()
        {
            menuOpen = false;
            match?.HideFabricatorMenu();
            if (playerInside)
            {
                match?.SetInteractionHint(BuildOpenHint(hintWasController));
            }
        }

        private static string BuildOpenHint(bool controller)
        {
            return controller
                ? "FABRICATOR\nHit Y to open"
                : "FABRICATOR\nPress F to open";
        }

        private void BuildVisuals()
        {
            CreatePart("Fabricator Base", PrimitiveType.Cylinder, theme.Pillar, transform, new Vector3(0f, 0.12f, 0f), new Vector3(1.45f, 0.12f, 1.45f), Vector3.zero);
            CreatePart("Fabricator Column", PrimitiveType.Cylinder, theme.Wall, transform, new Vector3(0f, 0.76f, 0f), new Vector3(0.34f, 0.62f, 0.34f), Vector3.zero);
            CreatePart("Fabricator Console", PrimitiveType.Cube, theme.Pillar, transform, new Vector3(0f, 1.05f, 0.52f), new Vector3(1.05f, 0.32f, 0.22f), new Vector3(-14f, 0f, 0f));
            CreatePart("Fabricator Screen Glow", PrimitiveType.Cube, theme.NeonA, transform, new Vector3(0f, 1.09f, 0.65f), new Vector3(0.78f, 0.08f, 0.035f), new Vector3(-14f, 0f, 0f));

            rotor = new GameObject("Fabricator Rotor").transform;
            rotor.SetParent(transform, false);
            rotor.localPosition = new Vector3(0f, 1.48f, 0f);
            CreatePart("Fabricator Ring A", PrimitiveType.Cube, theme.Scrap, rotor, Vector3.zero, new Vector3(1.1f, 0.035f, 0.08f), Vector3.zero);
            CreatePart("Fabricator Ring B", PrimitiveType.Cube, theme.Scrap, rotor, Vector3.zero, new Vector3(0.08f, 0.035f, 1.1f), Vector3.zero);

            var light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = new Color(0.1f, 0.82f, 1f);
            light.range = 5.4f;
            light.intensity = 2.1f;

            VerticalMarkerBeam.Attach(
                transform,
                "Amber Fabricator Location Beam",
                new Color(1f, 0.64f, 0.12f),
                22f,
                0.92f,
                6.5f);
        }

        private static void CreatePart(string objectName, PrimitiveType type, Material material, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localRotation)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(localRotation);

            if (part.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (part.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
