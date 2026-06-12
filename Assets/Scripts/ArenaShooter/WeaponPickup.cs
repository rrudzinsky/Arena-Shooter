using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaShooter
{
    public sealed class WeaponPickup : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition weapon = new();

        private MatchController match;
        private Transform floatingModel;
        private Vector3 floatingBaseLocalPosition;
        private float bobPhase;
        private bool showingSwapHint;
        private float swapReadyAt;

        public void Configure(MatchController owner, WeaponDefinition weaponDefinition)
        {
            match = owner;
            weapon = weaponDefinition.Clone();
            floatingModel = transform.Find("Floating Pickup Model");
            if (floatingModel != null)
            {
                floatingBaseLocalPosition = floatingModel.localPosition;
            }
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (floatingModel == null)
            {
                return;
            }

            floatingModel.Rotate(0f, 95f * Time.deltaTime, 0f, Space.Self);
            floatingModel.localPosition = floatingBaseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * 3f + bobPhase) * 0.08f);
        }

        private void OnTriggerEnter(Collider other)
        {
            var inventory = other.GetComponentInParent<WeaponInventory>();
            if (inventory == null)
            {
                return;
            }

            var isPlayer = inventory.GetComponent<PlayerFpsController>() != null;

            // grenade crates are a player-only inventory item and never touch gun slots
            if (weapon.FireStyle == WeaponFireStyle.Thrown)
            {
                if (isPlayer && inventory.TryAddGrenades(WeaponInventory.GrenadeCapacity))
                {
                    Consume();
                }

                return;
            }

            if (!isPlayer)
            {
                inventory.Equip(weapon);
                Consume();
                return;
            }

            if (inventory.TryPickupGun(weapon))
            {
                Consume();
                return;
            }

            ShowSwapHint(inventory);
        }

        private void OnTriggerStay(Collider other)
        {
            if (weapon.FireStyle == WeaponFireStyle.Thrown)
            {
                return;
            }

            var inventory = other.GetComponentInParent<WeaponInventory>();
            if (inventory == null || inventory.GetComponent<PlayerFpsController>() == null)
            {
                return;
            }

            // a slot may have freed up (or this pickup just changed) while standing here
            if (!showingSwapHint)
            {
                if (inventory.TryPickupGun(weapon))
                {
                    Consume();
                }
                else
                {
                    ShowSwapHint(inventory);
                }

                return;
            }

            if (Time.time < swapReadyAt || !SwapPressed())
            {
                return;
            }

            var previous = inventory.SwapActiveGunFor(weapon);
            if (previous == null)
            {
                Consume();
                return;
            }

            // the replaced gun stays here on the pad, ready to be taken back
            ReplaceWeapon(previous);
            ShowSwapHint(inventory);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!showingSwapHint || other.GetComponentInParent<WeaponInventory>() == null)
            {
                return;
            }

            showingSwapHint = false;
        }

        private static bool SwapPressed()
        {
            var keyboard = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            var gamepad = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
            return keyboard || gamepad;
        }

        private void ShowSwapHint(WeaponInventory inventory)
        {
            // Arms the silent swap-by-F state — no on-screen hint text; the HUD
            // weapon panel already shows what you carry.
            showingSwapHint = true;
            swapReadyAt = Time.time + 0.12f;
        }

        private void ReplaceWeapon(WeaponDefinition replacement)
        {
            weapon = replacement.Clone();
            gameObject.name = $"{weapon.DisplayName} Pickup";

            // detach before Destroy so the rebuilt "Floating Pickup Model" is the only
            // child with that name this frame
            var stale = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in transform)
            {
                stale.Add(child.gameObject);
            }

            foreach (var staleChild in stale)
            {
                staleChild.transform.SetParent(null, false);
                Destroy(staleChild);
            }

            var theme = match != null ? match.Theme : null;
            if (theme != null)
            {
                PickupVisuals.BuildGunPickup(transform, theme, weapon.ModelKind);
            }

            floatingModel = transform.Find("Floating Pickup Model");
            if (floatingModel != null)
            {
                floatingBaseLocalPosition = floatingModel.localPosition;
            }
        }

        private void Consume()
        {
            showingSwapHint = false;
            match?.NotifyPickupTaken(this);
            Destroy(gameObject);
        }
    }
}
