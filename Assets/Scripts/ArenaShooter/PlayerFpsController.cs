using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaShooter
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CombatantHealth))]
    [RequireComponent(typeof(WeaponInventory))]
    public sealed class PlayerFpsController : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float moveSpeed = 6.2f;
        [SerializeField] private float sprintMultiplier = 1.45f;
        [SerializeField] private float jumpSpeed = 5.2f;
        [SerializeField] private float lookSensitivity = 0.085f;
        [SerializeField] private float gamepadLookDegreesPerSecond = 305f;
        [SerializeField] private float crouchSpeedMultiplier = 0.34f;
        [SerializeField] private float proneSpeedMultiplier = 0.11f;
        [SerializeField] private float dodgeCooldown = 0.7f;
        [SerializeField] private float normalFov = 62f;
        [SerializeField] private float aimedFov = 44f;
        [SerializeField] private float vaultDuration = 0.48f;

        private CharacterController controller;
        private CombatantHealth health;
        private WeaponInventory weapons;
        private SpawnIntroWalker introWalker;
        private MatchController match;
        private FirstPersonViewModel viewModel;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction attackAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction stanceAction;
        private InputAction aimAction;
        private InputAction lungeLeftAction;
        private InputAction lungeRightAction;
        private InputAction turretModeAction;
        private InputAction swapWeaponAction;
        private InputAction gunSlotOneAction;
        private InputAction gunSlotTwoAction;
        private InputAction grenadeAction;
        private float pitch;
        private float verticalVelocity;
        private float standingHeight = 1.9f;
        private float crouchingHeight = 0.92f;
        private float proneHeight = 0.42f;
        private float standingCameraY;
        private float crouchingCameraY;
        private float proneCameraY;
        private float stancePressedAt;
        private const float ProneHoldSeconds = 0.36f;
        private float evadeTimer;
        private float evadeDuration;
        private float evadeSpeed;
        private float evadeCooldownUntil;
        private Vector3 evadeDirection;
        private bool evadeIsRoll;
        private bool crouchToggled;
        private bool proneToggled;
        private bool stancePressActive;
        private bool stanceHoldConsumed;
        private bool stanceSuppressedUntilRelease;
        private float visualRoll;
        private float visualPitch;
        private float stanceBobPhase;
        private float stanceCameraPitch;
        private float stanceCameraRoll;
        private ArenaCameraFraming cameraFraming;
        private GameObject turretPreview;
        private Renderer[] turretPreviewRenderers;
        private bool turretPreviewValid;
        private float vaultTimer;
        private Vector3 vaultStart;
        private Vector3 vaultApex;
        private Vector3 vaultEnd;
        private Collider vaultIgnoredCollider;
        private const float VaultProbeDistance = 1.85f;
        private const float VaultProbeRadius = 0.24f;
        private const float VaultLandingGroundProbeHeight = 1.25f;

        public Camera PlayerCamera { get; private set; }
        public MatchController Match => match;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<CombatantHealth>();
            weapons = GetComponent<WeaponInventory>();
            introWalker = GetComponent<SpawnIntroWalker>();
            BuildInputActions();
        }

        public void Configure(MatchController owner, Camera playerCamera)
        {
            match = owner;
            PlayerCamera = playerCamera;
            cameraPivot = playerCamera.transform;
            cameraFraming = playerCamera.GetComponent<ArenaCameraFraming>();
            viewModel = playerCamera.GetComponent<FirstPersonViewModel>();
            standingCameraY = cameraPivot.localPosition.y;
            crouchingCameraY = standingCameraY - 0.64f;
            proneCameraY = standingCameraY - 1.22f;
        }

        private void OnEnable()
        {
            moveAction.Enable();
            lookAction.Enable();
            attackAction.Enable();
            jumpAction.Enable();
            sprintAction.Enable();
            crouchAction.Enable();
            stanceAction.Enable();
            aimAction.Enable();
            lungeLeftAction.Enable();
            lungeRightAction.Enable();
            turretModeAction.Enable();
            swapWeaponAction.Enable();
            gunSlotOneAction.Enable();
            gunSlotTwoAction.Enable();
            grenadeAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            lookAction.Disable();
            attackAction.Disable();
            jumpAction.Disable();
            sprintAction.Disable();
            crouchAction.Disable();
            stanceAction.Disable();
            aimAction.Disable();
            lungeLeftAction.Disable();
            lungeRightAction.Disable();
            turretModeAction.Disable();
            swapWeaponAction.Disable();
            gunSlotOneAction.Disable();
            gunSlotTwoAction.Disable();
            grenadeAction.Disable();
            DestroyTurretPreview();
            CancelVault();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (introWalker == null)
            {
                introWalker = GetComponent<SpawnIntroWalker>();
            }

            if (health == null || !health.IsAlive || match == null || !match.IsMatchActive || (introWalker != null && introWalker.IsWalking))
            {
                CancelVault();
                return;
            }

            if (match.IsPauseMenuOpen || match.IsFabricatorMenuOpen || Time.time < match.InputSuppressedUntil)
            {
                CancelVault();
                weapons.SetAiming(false);
                match.SetPlayerAiming(false);
                viewModel?.SetMovementState(false, false);
                DestroyTurretPreview();
                return;
            }

            if (turretModeAction.WasPressedThisFrame())
            {
                match.ToggleTurretPlacementMode();
                if (!match.IsTurretPlacementMode)
                {
                    DestroyTurretPreview();
                }
            }

            UpdateLook();

            if (vaultTimer > 0f)
            {
                weapons.SetAiming(false);
                match.SetPlayerAiming(false);
                viewModel?.SetMovementState(false, false);
                DestroyTurretPreview();
                UpdateVaultMovement();
                return;
            }

            if (match.IsTurretPlacementMode)
            {
                weapons.SetAiming(false);
                match.SetPlayerAiming(false);
                UpdateStanceInput();
                UpdateViewModelState();
                UpdateMovement();
                if (vaultTimer > 0f)
                {
                    CancelVault();
                    return;
                }

                UpdateTurretPlacementMode();
                return;
            }

            UpdateStanceInput();
            UpdateAbilityInput();
            UpdateWeaponSelectionInput();
            UpdateAiming();
            UpdateViewModelState();
            UpdateMovement();

            if (vaultTimer > 0f)
            {
                return;
            }

            if (attackAction.IsPressed() && cameraPivot != null)
            {
                weapons.TryFire(cameraPivot.position, cameraPivot.forward);
            }

            if (grenadeAction.WasPressedThisFrame() && cameraPivot != null)
            {
                weapons.TryThrowGrenade(cameraPivot.position, cameraPivot.forward);
            }
        }

        private void UpdateWeaponSelectionInput()
        {
            if (swapWeaponAction.WasPressedThisFrame())
            {
                weapons.CycleActiveGun();
            }

            if (gunSlotOneAction.WasPressedThisFrame())
            {
                weapons.SelectGunSlot(0);
            }

            if (gunSlotTwoAction.WasPressedThisFrame())
            {
                weapons.SelectGunSlot(1);
            }
        }

        private void UpdateTurretPlacementMode()
        {
            if (cameraPivot == null || match == null || match.TurretKits <= 0)
            {
                match?.SetTurretPlacementMode(false);
                DestroyTurretPreview();
                return;
            }

            UpdateTurretPreview();

            if (aimAction.WasPressedThisFrame())
            {
                match.SetTurretPlacementMode(false);
                DestroyTurretPreview();
                match.ClearInteractionHint();
                return;
            }

            if (attackAction.WasPressedThisFrame())
            {
                var ray = new Ray(cameraPivot.position, cameraPivot.forward);
                if (match.TryPlaceTurret(ray) && !match.IsTurretPlacementMode)
                {
                    DestroyTurretPreview();
                }
            }
        }

        private void UpdateTurretPreview()
        {
            if (turretPreview == null)
            {
                turretPreview = match.CreateTurretPlacementPreview();
                turretPreviewRenderers = turretPreview.GetComponentsInChildren<Renderer>(true);
            }

            var ray = new Ray(cameraPivot.position, cameraPivot.forward);
            turretPreviewValid = match.TryResolveTurretPlacement(ray, out var position, out var rotation);
            turretPreview.SetActive(turretPreviewValid);
            if (!turretPreviewValid)
            {
                return;
            }

            turretPreview.transform.position = position;
            turretPreview.transform.rotation = rotation;
            SetTurretPreviewColor(new Color(0.94f, 0.97f, 1f, 0.46f));
        }

        private void SetTurretPreviewColor(Color color)
        {
            if (turretPreviewRenderers == null)
            {
                return;
            }

            for (var i = 0; i < turretPreviewRenderers.Length; i++)
            {
                var renderer = turretPreviewRenderers[i];
                if (renderer == null || renderer.material == null)
                {
                    continue;
                }

                var material = renderer.material;
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * 0.55f);
                }
            }
        }

        private void DestroyTurretPreview()
        {
            if (turretPreview != null)
            {
                Destroy(turretPreview);
            }

            turretPreview = null;
            turretPreviewRenderers = null;
            turretPreviewValid = false;
        }

        private void UpdateAiming()
        {
            var aiming = aimAction.IsPressed() && weapons.HasWeapon && evadeTimer <= 0f;
            weapons.SetAiming(aiming);
            match?.SetPlayerAiming(aiming);

            if (PlayerCamera != null)
            {
                var targetFov = aiming ? aimedFov : normalFov;
                if (cameraFraming != null)
                {
                    cameraFraming.SetReferenceVerticalFov(targetFov);
                }
                else
                {
                    PlayerCamera.fieldOfView = Mathf.Lerp(PlayerCamera.fieldOfView, targetFov, Time.deltaTime * 14f);
                }
            }
        }

        private void UpdateViewModelState()
        {
            if (viewModel == null)
            {
                return;
            }

            var input = moveAction.ReadValue<Vector2>();
            var isMoving = input.sqrMagnitude > 0.04f;
            var isAiming = aimAction.IsPressed() && weapons.HasWeapon;
            var stance = ResolveStance();
            var isSprinting = sprintAction.IsPressed() && isMoving && stance == PlayerStance.Standing && !isAiming && evadeTimer <= 0f;
            viewModel.SetMovementState(isMoving, isSprinting);
        }

        private void UpdateLook()
        {
            var look = ReadLookDelta();
            transform.Rotate(Vector3.up, look.x, Space.World);

            pitch = Mathf.Clamp(pitch - look.y, -82f, 82f);
            cameraPivot.localRotation = Quaternion.Euler(pitch + visualPitch + stanceCameraPitch, 0f, visualRoll + stanceCameraRoll);
        }

        private Vector2 ReadLookDelta()
        {
            var look = lookAction.ReadValue<Vector2>();
            var activeControl = lookAction.activeControl;
            var isGamepadLook = activeControl != null && activeControl.device is Gamepad;
            if (!isGamepadLook)
            {
                return look * lookSensitivity;
            }

            var magnitude = Mathf.Clamp01(look.magnitude);
            if (magnitude <= 0.001f)
            {
                return Vector2.zero;
            }

            var curvedMagnitude = Mathf.Pow(magnitude, 1.18f);
            var direction = look / magnitude;
            return direction * (curvedMagnitude * gamepadLookDegreesPerSecond * ArenaUserSettings.ControllerLookScale * Time.deltaTime);
        }

        private void UpdateMovement()
        {
            if (evadeTimer > 0f)
            {
                UpdateEvadeMovement();
                return;
            }

            var input = moveAction.ReadValue<Vector2>();
            var desired = transform.right * input.x + transform.forward * input.y;
            if (desired.sqrMagnitude > 1f)
            {
                desired.Normalize();
            }

            var stance = ResolveStance();
            var aiming = aimAction.IsPressed() && weapons.HasWeapon;
            var speed = moveSpeed * (sprintAction.IsPressed() && stance == PlayerStance.Standing && !aiming ? sprintMultiplier : 1f);
            if (stance == PlayerStance.Prone)
            {
                speed *= proneSpeedMultiplier;
            }
            else if (stance == PlayerStance.Crouching)
            {
                speed *= crouchSpeedMultiplier;
            }
            else if (aiming)
            {
                speed *= 0.72f;
            }

            UpdateStanceShape(stance);

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1.5f;
            }

            if (controller.isGrounded && jumpAction.WasPressedThisFrame())
            {
                if (TryBeginVault())
                {
                    return;
                }

                verticalVelocity = jumpSpeed;
            }

            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            var movement = desired * speed;
            movement.y = verticalVelocity;
            controller.Move(movement * Time.deltaTime);
        }

        private bool TryBeginVault()
        {
            if (cameraPivot == null ||
                controller == null ||
                !controller.isGrounded ||
                ResolveStance() != PlayerStance.Standing ||
                (match != null && match.IsTurretPlacementMode))
            {
                return false;
            }

            var forward = cameraPivot.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = transform.forward;
            }

            forward.Normalize();
            var origin = cameraPivot.position - Vector3.up * 0.18f;
            var hits = Physics.SphereCastAll(origin, VaultProbeRadius, forward, VaultProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            DestructibleArenaPiece.PlayerVaultSolution bestSolution = default;
            var bestDistance = float.PositiveInfinity;
            var found = false;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform == transform)
                {
                    continue;
                }

                var destructible = hit.collider.GetComponentInParent<DestructibleArenaPiece>();
                if (destructible == null ||
                    !destructible.TryResolvePlayerVault(transform.position, forward, controller.radius, standingHeight, out var solution) ||
                    !TryResolveVaultLanding(solution, out var adjustedSolution))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestSolution = adjustedSolution;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            BeginVault(bestSolution);
            return true;
        }

        private bool TryResolveVaultLanding(DestructibleArenaPiece.PlayerVaultSolution candidate, out DestructibleArenaPiece.PlayerVaultSolution adjusted)
        {
            adjusted = candidate;
            var rayOrigin = candidate.ExitPosition + Vector3.up * VaultLandingGroundProbeHeight;
            var groundDestructible = default(DestructibleArenaPiece);
            if (!Physics.Raycast(rayOrigin, Vector3.down, out var groundHit, VaultLandingGroundProbeHeight + 1.6f, ~0, QueryTriggerInteraction.Ignore) ||
                groundHit.normal.y < 0.55f ||
                ((groundDestructible = groundHit.collider.GetComponentInParent<DestructibleArenaPiece>()) != null && !groundDestructible.IsFloorSurface()) ||
                groundHit.collider.GetComponentInParent<CombatantHealth>() != null ||
                groundHit.collider.GetComponentInParent<FabricatorStation>() != null ||
                groundHit.collider.GetComponentInParent<HealingStation>() != null ||
                groundHit.collider.GetComponentInParent<PlayerTurret>() != null)
            {
                return false;
            }

            var landing = candidate.ExitPosition;
            landing.y = groundHit.point.y;
            if (!IsVaultCapsuleClear(landing, candidate.SourceCollider))
            {
                return false;
            }

            var apex = candidate.ApexPosition;
            apex.y = Mathf.Max(Mathf.Max(apex.y, landing.y + 0.62f), transform.position.y + 0.52f);
            adjusted = new DestructibleArenaPiece.PlayerVaultSolution(candidate.EntryPosition, landing, apex, candidate.SourceCollider);
            return true;
        }

        private bool IsVaultCapsuleClear(Vector3 position, Collider sourceCollider)
        {
            var radius = Mathf.Max(0.12f, controller.radius * 0.92f);
            var height = Mathf.Max(standingHeight, radius * 2f + 0.05f);
            var center = position + Vector3.up * (height * 0.5f);
            var half = Mathf.Max(0f, height * 0.5f - radius);
            var bottom = center - Vector3.up * half;
            var top = center + Vector3.up * half;
            var overlaps = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < overlaps.Length; i++)
            {
                var overlap = overlaps[i];
                if (overlap == null ||
                    overlap == sourceCollider ||
                    overlap.transform == transform ||
                    overlap.GetComponentInParent<PlayerFpsController>() == this)
                {
                    continue;
                }

                var destructible = overlap.GetComponentInParent<DestructibleArenaPiece>();
                if (destructible != null && !destructible.IsFloorSurface())
                {
                    return false;
                }

                if (overlap.GetComponentInParent<CombatantHealth>() != null ||
                    overlap.GetComponentInParent<FabricatorStation>() != null ||
                    overlap.GetComponentInParent<HealingStation>() != null ||
                    overlap.GetComponentInParent<PlayerTurret>() != null)
                {
                    return false;
                }

                if (!overlap.name.ToLowerInvariant().Contains("floor"))
                {
                    return false;
                }
            }

            return true;
        }

        private void BeginVault(DestructibleArenaPiece.PlayerVaultSolution solution)
        {
            vaultStart = transform.position;
            vaultApex = solution.ApexPosition;
            vaultEnd = solution.ExitPosition;
            vaultTimer = vaultDuration;
            vaultIgnoredCollider = solution.SourceCollider;
            verticalVelocity = 0f;
            evadeTimer = 0f;
            crouchToggled = false;
            proneToggled = false;
            stancePressActive = false;
            stanceHoldConsumed = false;
            weapons.SetAiming(false);
            match?.SetPlayerAiming(false);
            if (vaultIgnoredCollider != null)
            {
                Physics.IgnoreCollision(controller, vaultIgnoredCollider, true);
            }
        }

        private void CancelVault()
        {
            if (vaultTimer <= 0f && vaultIgnoredCollider == null)
            {
                return;
            }

            if (vaultIgnoredCollider != null && controller != null)
            {
                Physics.IgnoreCollision(controller, vaultIgnoredCollider, false);
            }

            vaultIgnoredCollider = null;
            vaultTimer = 0f;
            verticalVelocity = -1.5f;
            visualPitch = 0f;
            visualRoll = 0f;
        }

        private void UpdateVaultMovement()
        {
            var duration = Mathf.Max(0.05f, vaultDuration);
            vaultTimer = Mathf.Max(0f, vaultTimer - Time.deltaTime);
            var t = 1f - vaultTimer / duration;
            var eased = Mathf.SmoothStep(0f, 1f, t);
            var firstLeg = Vector3.Lerp(vaultStart, vaultApex, eased);
            var secondLeg = Vector3.Lerp(vaultApex, vaultEnd, eased);
            var target = Vector3.Lerp(firstLeg, secondLeg, eased);
            controller.Move(target - transform.position);

            visualPitch = Mathf.Sin(t * Mathf.PI) * -5.5f;
            visualRoll = Mathf.Sin(t * Mathf.PI * 2f) * 2.2f;
            UpdateStanceShape(PlayerStance.Standing);

            if (vaultTimer > 0f)
            {
                return;
            }

            controller.Move(vaultEnd - transform.position);
            if (vaultIgnoredCollider != null)
            {
                Physics.IgnoreCollision(controller, vaultIgnoredCollider, false);
                vaultIgnoredCollider = null;
            }

            verticalVelocity = -1.5f;
            visualPitch = 0f;
            visualRoll = 0f;
        }

        private void UpdateAbilityInput()
        {
            if (Time.time < evadeCooldownUntil || evadeTimer > 0f || vaultTimer > 0f || !controller.isGrounded)
            {
                return;
            }

            var input = moveAction.ReadValue<Vector2>();
            if (crouchAction.WasPressedThisFrame() && sprintAction.IsPressed() && input.y > 0.2f)
            {
                BeginEvade(transform.forward, 0.46f, 12.8f, true);
                return;
            }

            if (lungeLeftAction.WasPressedThisFrame())
            {
                BeginEvade(-transform.right, 0.24f, 13.5f, false);
                return;
            }

            if (lungeRightAction.WasPressedThisFrame())
            {
                BeginEvade(transform.right, 0.24f, 13.5f, false);
            }
        }

        private void BeginEvade(Vector3 direction, float duration, float speed, bool roll)
        {
            evadeDirection = direction.normalized;
            evadeDuration = duration;
            evadeTimer = duration;
            evadeSpeed = speed;
            evadeIsRoll = roll;
            evadeCooldownUntil = Time.time + dodgeCooldown;
        }

        private void UpdateEvadeMovement()
        {
            evadeTimer = Mathf.Max(0f, evadeTimer - Time.deltaTime);

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1.5f;
            }

            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            var movement = evadeDirection * evadeSpeed;
            movement.y = verticalVelocity;
            controller.Move(movement * Time.deltaTime);

            var progress = 1f - (evadeTimer / Mathf.Max(0.01f, evadeDuration));
            if (evadeIsRoll)
            {
                visualPitch = Mathf.Sin(progress * Mathf.PI) * -6f;
                visualRoll = Mathf.Sin(progress * Mathf.PI) * 3f;
            }
            else
            {
                var side = Vector3.Dot(evadeDirection, transform.right) >= 0f ? -1f : 1f;
                visualRoll = Mathf.Sin(progress * Mathf.PI) * 18f * side;
                visualPitch = Mathf.Sin(progress * Mathf.PI) * -4f;
            }

            UpdateStanceShape(evadeIsRoll ? PlayerStance.Crouching : ResolveStance());

            if (evadeTimer <= 0f)
            {
                visualPitch = 0f;
                visualRoll = 0f;
            }
        }

        private enum PlayerStance
        {
            Standing,
            Crouching,
            Prone
        }

        private void UpdateStanceInput()
        {
            if (stanceSuppressedUntilRelease)
            {
                if (stanceAction.WasReleasedThisFrame())
                {
                    stanceSuppressedUntilRelease = false;
                }

                return;
            }

            if (stanceAction.WasPressedThisFrame())
            {
                if (TryBeginSlideFromStanceInput() || !IsStanceOnlyInput())
                {
                    stancePressActive = false;
                    stanceHoldConsumed = false;
                    stanceSuppressedUntilRelease = true;
                    return;
                }

                stancePressedAt = Time.time;
                stancePressActive = true;
                stanceHoldConsumed = false;
            }

            if (stancePressActive && stanceAction.IsPressed() && !stanceHoldConsumed && Time.time - stancePressedAt >= ProneHoldSeconds)
            {
                proneToggled = true;
                crouchToggled = false;
                stanceHoldConsumed = true;
            }

            if (stanceAction.WasReleasedThisFrame())
            {
                if (!stanceHoldConsumed)
                {
                    if (proneToggled)
                    {
                        proneToggled = false;
                        crouchToggled = true;
                    }
                    else
                    {
                        crouchToggled = !crouchToggled;
                    }
                }

                stancePressActive = false;
            }
        }

        private bool TryBeginSlideFromStanceInput()
        {
            if (Time.time < evadeCooldownUntil || evadeTimer > 0f || !controller.isGrounded)
            {
                return false;
            }

            var input = moveAction.ReadValue<Vector2>();
            if (!sprintAction.IsPressed() || input.y <= 0.2f)
            {
                return false;
            }

            BeginEvade(transform.forward, 0.46f, 12.8f, true);
            return true;
        }

        private bool IsStanceOnlyInput()
        {
            var input = moveAction.ReadValue<Vector2>();
            return input.sqrMagnitude <= 0.04f
                && !sprintAction.IsPressed()
                && !jumpAction.IsPressed()
                && !attackAction.IsPressed()
                && !aimAction.IsPressed()
                && evadeTimer <= 0f;
        }

        private PlayerStance ResolveStance()
        {
            if (proneToggled)
            {
                return PlayerStance.Prone;
            }

            return crouchAction.IsPressed() || crouchToggled ? PlayerStance.Crouching : PlayerStance.Standing;
        }

        private void UpdateStanceShape(PlayerStance stance)
        {
            var targetHeight = stance == PlayerStance.Prone ? proneHeight : stance == PlayerStance.Crouching ? crouchingHeight : standingHeight;
            controller.height = Mathf.MoveTowards(controller.height, targetHeight, Time.deltaTime * 6f);
            controller.center = new Vector3(0f, -(standingHeight - controller.height) * 0.5f, 0f);

            if (cameraPivot != null)
            {
                var local = cameraPivot.localPosition;
                var targetCameraY = stance == PlayerStance.Prone ? proneCameraY : stance == PlayerStance.Crouching ? crouchingCameraY : standingCameraY;
                targetCameraY += UpdateStanceCameraMotion(stance);
                local.y = Mathf.MoveTowards(local.y, targetCameraY, Time.deltaTime * 3.8f);
                cameraPivot.localPosition = local;
            }
        }

        private float UpdateStanceCameraMotion(PlayerStance stance)
        {
            var input = moveAction.ReadValue<Vector2>();
            var isMoving = input.sqrMagnitude > 0.04f && controller.isGrounded && evadeTimer <= 0f;
            if (!isMoving || stance == PlayerStance.Standing)
            {
                stanceCameraPitch = Mathf.Lerp(stanceCameraPitch, 0f, Time.deltaTime * 9f);
                stanceCameraRoll = Mathf.Lerp(stanceCameraRoll, 0f, Time.deltaTime * 9f);
                return 0f;
            }

            var speed = stance == PlayerStance.Prone ? 2.8f : 3.9f;
            stanceBobPhase += Time.deltaTime * speed;
            var wave = Mathf.Sin(stanceBobPhase);
            var doubleWave = Mathf.Sin(stanceBobPhase * 2.1f);

            if (stance == PlayerStance.Prone)
            {
                var crawlJerk = Mathf.Sign(doubleWave) * 0.006f;
                stanceCameraPitch = Mathf.Lerp(stanceCameraPitch, wave * 0.95f + Mathf.Sign(wave) * 0.18f, Time.deltaTime * 8f);
                stanceCameraRoll = Mathf.Lerp(stanceCameraRoll, doubleWave * 0.75f, Time.deltaTime * 8f);
                return Mathf.Abs(wave) * 0.034f + crawlJerk;
            }

            stanceCameraPitch = Mathf.Lerp(stanceCameraPitch, wave * 0.42f, Time.deltaTime * 8f);
            stanceCameraRoll = Mathf.Lerp(stanceCameraRoll, doubleWave * 0.55f, Time.deltaTime * 8f);
            return Mathf.Abs(wave) * 0.018f;
        }

        private void BuildInputActions()
        {
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
            lookAction.AddBinding("<Gamepad>/rightStick");

            attackAction = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            attackAction.AddBinding("<Gamepad>/rightTrigger");

            jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");

            sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            sprintAction.AddBinding("<Gamepad>/leftStickPress");

            crouchAction = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            crouchAction.AddBinding("<Keyboard>/c");

            stanceAction = new InputAction("Stance", InputActionType.Button, "<Keyboard>/b");
            stanceAction.AddBinding("<Gamepad>/buttonEast");

            aimAction = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            aimAction.AddBinding("<Gamepad>/leftTrigger");

            lungeLeftAction = new InputAction("Lunge Left", InputActionType.Button, "<Keyboard>/q");
            lungeLeftAction.AddBinding("<Gamepad>/leftShoulder");

            lungeRightAction = new InputAction("Lunge Right", InputActionType.Button, "<Keyboard>/e");
            lungeRightAction.AddBinding("<Gamepad>/rightShoulder");

            turretModeAction = new InputAction("Turret Kit", InputActionType.Button, "<Keyboard>/4");
            turretModeAction.AddBinding("<Gamepad>/dpad/down");

            swapWeaponAction = new InputAction("Swap Weapon", InputActionType.Button, "<Keyboard>/x");
            swapWeaponAction.AddBinding("<Gamepad>/dpad/up");

            gunSlotOneAction = new InputAction("Gun Slot 1", InputActionType.Button, "<Keyboard>/1");
            gunSlotTwoAction = new InputAction("Gun Slot 2", InputActionType.Button, "<Keyboard>/2");

            grenadeAction = new InputAction("Throw Grenade", InputActionType.Button, "<Keyboard>/g");
            grenadeAction.AddBinding("<Gamepad>/dpad/right");
        }
    }
}
