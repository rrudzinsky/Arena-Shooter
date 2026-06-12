using System.Collections;
using UnityEngine;

namespace ArenaShooter
{
    public sealed class WeaponInventory : MonoBehaviour
    {
        public const int MaxGuns = 2;
        public const int GrenadeCapacity = 2;

        [SerializeField] private Transform firePoint;
        [SerializeField] private CombatantHealth owner;

        private sealed class GunSlot
        {
            public WeaponDefinition Weapon;
            public int Ammo;
            public int MaxAmmo;
        }

        private Material beamMaterial;
        private ArenaTheme arenaTheme;
        private FirstPersonViewModel viewModel;
        private readonly System.Collections.Generic.List<GunSlot> guns = new();
        private int activeGunIndex = -1;
        private int grenadeCount;
        private WeaponDefinition grenadeStats;
        private float nextFireTime;
        private float nextGrenadeTime;
        private float damageMultiplier = 1f;
        private const float NearCoverTargetForgiveness = 1.05f;
        private const float SwapReadySeconds = 0.3f;
        private const float MissedShotBeamLength = 30f;

        private GunSlot ActiveGun => activeGunIndex >= 0 && activeGunIndex < guns.Count ? guns[activeGunIndex] : null;

        public bool HasWeapon => ActiveGun != null;
        public string WeaponName => ActiveGun != null ? ActiveGun.Weapon.DisplayName : "Unarmed";
        public string HolsteredWeaponName => guns.Count > 1 ? guns[1 - activeGunIndex].Weapon.DisplayName : null;
        public int GunCount => guns.Count;
        public int Ammo => ActiveGun != null ? ActiveGun.Ammo : 0;
        public int MaxAmmo => ActiveGun != null ? ActiveGun.MaxAmmo : 0;
        public int GrenadeCount => grenadeCount;
        public float CurrentDamage => ActiveGun != null ? ActiveGun.Weapon.Damage * damageMultiplier : 0f;

        private void Awake()
        {
            if (owner == null)
            {
                owner = GetComponent<CombatantHealth>();
            }
        }

        public void Configure(CombatantHealth weaponOwner, Transform muzzle, Material tracerMaterial, ArenaTheme theme = null)
        {
            owner = weaponOwner;
            firePoint = muzzle;
            beamMaterial = tracerMaterial;
            arenaTheme = theme;
        }

        public void ConfigureViewModel(FirstPersonViewModel model)
        {
            viewModel = model;
            RefreshViewModel();
        }

        public void SetAiming(bool aiming)
        {
            viewModel?.SetAiming(aiming && HasWeapon);
        }

        /// <summary>
        /// Single-slot equip used by AI, turrets, and scripted weapon grants: replaces
        /// the active gun (or fills the first slot when unarmed).
        /// </summary>
        public void Equip(WeaponDefinition definition)
        {
            if (ActiveGun == null)
            {
                guns.Add(new GunSlot());
                activeGunIndex = guns.Count - 1;
            }

            var slot = ActiveGun;
            slot.Weapon = definition.Clone();
            slot.Ammo = slot.Weapon.Ammo;
            slot.MaxAmmo = slot.Weapon.Ammo;
            nextFireTime = 0f;
            RefreshViewModel();
        }

        /// <summary>
        /// Player pickup path: fills a free gun slot and switches to it. Returns false
        /// when both slots are taken (caller should offer a swap instead).
        /// </summary>
        public bool TryPickupGun(WeaponDefinition definition)
        {
            if (guns.Count >= MaxGuns)
            {
                return false;
            }

            var slot = new GunSlot
            {
                Weapon = definition.Clone()
            };
            slot.Ammo = slot.Weapon.Ammo;
            slot.MaxAmmo = slot.Weapon.Ammo;
            guns.Add(slot);
            SetActiveGun(guns.Count - 1);
            return true;
        }

        /// <summary>
        /// Replaces the held gun with the offered one and returns the old definition so
        /// the pickup pad can offer it back.
        /// </summary>
        public WeaponDefinition SwapActiveGunFor(WeaponDefinition definition)
        {
            if (ActiveGun == null)
            {
                TryPickupGun(definition);
                return null;
            }

            var previous = ActiveGun.Weapon.Clone();
            var slot = ActiveGun;
            slot.Weapon = definition.Clone();
            slot.Ammo = slot.Weapon.Ammo;
            slot.MaxAmmo = slot.Weapon.Ammo;
            nextFireTime = Time.time + SwapReadySeconds;
            RefreshViewModel();
            return previous;
        }

        public void CycleActiveGun()
        {
            if (guns.Count > 1)
            {
                SetActiveGun(1 - activeGunIndex);
            }
        }

        public void SelectGunSlot(int index)
        {
            if (index >= 0 && index < guns.Count && index != activeGunIndex)
            {
                SetActiveGun(index);
            }
        }

        private void SetActiveGun(int index)
        {
            activeGunIndex = index;
            nextFireTime = Mathf.Max(nextFireTime, Time.time + SwapReadySeconds);
            RefreshViewModel();
        }

        private void RefreshViewModel()
        {
            if (viewModel == null)
            {
                return;
            }

            if (ActiveGun != null)
            {
                viewModel.ShowWeaponModel(ActiveGun.Weapon.ModelKind);
            }

            viewModel.SetWeaponVisible(ActiveGun != null);
            viewModel.SetAmmo(Ammo, MaxAmmo);
        }

        public bool TryAddAmmo(int amount)
        {
            if (amount <= 0 || guns.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < guns.Count; i++)
            {
                var slot = guns[(activeGunIndex + i) % guns.Count];
                if (slot.Ammo >= slot.MaxAmmo)
                {
                    continue;
                }

                slot.Ammo = Mathf.Min(slot.MaxAmmo, slot.Ammo + amount);
                if (slot == ActiveGun)
                {
                    viewModel?.SetAmmo(slot.Ammo, slot.MaxAmmo);
                }

                return true;
            }

            return false;
        }

        public bool TryAddGrenades(int amount)
        {
            if (amount <= 0 || grenadeCount >= GrenadeCapacity)
            {
                return false;
            }

            grenadeCount = Mathf.Min(GrenadeCapacity, grenadeCount + amount);
            return true;
        }

        public bool TryUpgradeCurrentWeapon(float damageBonus, int ammoBonus)
        {
            var slot = ActiveGun;
            if (slot == null)
            {
                return false;
            }

            slot.Weapon.Damage += Mathf.Max(0f, damageBonus);
            if (ammoBonus > 0)
            {
                slot.MaxAmmo += ammoBonus;
                slot.Ammo = Mathf.Min(slot.MaxAmmo, slot.Ammo + ammoBonus);
                viewModel?.SetAmmo(slot.Ammo, slot.MaxAmmo);
            }

            return true;
        }

        public void SetDamageMultiplier(float multiplier)
        {
            damageMultiplier = Mathf.Max(1f, multiplier);
        }

        public bool TryFire(Vector3 origin, Vector3 direction, bool showPresentation = true)
        {
            var slot = ActiveGun;
            if (slot == null || slot.Ammo <= 0 || Time.time < nextFireTime || owner == null || !owner.IsAlive)
            {
                return false;
            }

            var weapon = slot.Weapon;
            nextFireTime = Time.time + weapon.Cooldown;
            slot.Ammo--;
            viewModel?.SetAmmo(slot.Ammo, slot.MaxAmmo);
            if (showPresentation)
            {
                viewModel?.PlayFire();
                PlayFireAudio(weapon);
            }

            ArenaNoise.EmitGunfire(origin, direction, owner, 64f);
            if (owner.GetComponent<PlayerFpsController>() != null)
            {
                ArenaNoise.EmitPlayerNoise(origin, 58f);
            }

            if (weapon.FireStyle == WeaponFireStyle.Thrown)
            {
                LaunchGrenade(origin, direction.normalized, weapon);
                return true;
            }

            var pellets = weapon.FireStyle == WeaponFireStyle.Scatter ? Mathf.Max(1, weapon.PelletCount) : 1;
            var beamWidth = pellets > 1 ? 0.028f : 0.055f;
            for (var pellet = 0; pellet < pellets; pellet++)
            {
                var pelletDirection = pellets > 1
                    ? ApplySpread(direction.normalized, weapon.SpreadDegrees)
                    : direction.normalized;
                FireSingleRay(weapon, origin, pelletDirection, showPresentation, beamWidth);
            }

            return true;
        }

        public bool TryThrowGrenade(Vector3 origin, Vector3 direction)
        {
            if (grenadeCount <= 0 || Time.time < nextGrenadeTime || owner == null || !owner.IsAlive)
            {
                return false;
            }

            grenadeStats ??= WeaponCatalog.CreatePlasmaGrenade();
            nextGrenadeTime = Time.time + grenadeStats.Cooldown;
            grenadeCount--;
            viewModel?.PlayFire();
            ArenaAudio.Instance?.PlayGrenadeThrow(transform.position);
            ArenaNoise.EmitGunfire(origin, direction, owner, 50f);
            LaunchGrenade(origin, direction.normalized, grenadeStats);
            return true;
        }

        private void FireSingleRay(WeaponDefinition weapon, Vector3 origin, Vector3 direction, bool showPresentation, float beamWidth)
        {
            var ray = new Ray(origin, direction);
            // A miss draws the tracer only partway: full-range beams painted across
            // the sky read as broken dashed lines against the dome backdrop.
            var endPoint = origin + ray.direction * Mathf.Min(weapon.Range, MissedShotBeamLength);

            if (TryGetWeaponHit(ray, weapon.Range, out var hit))
            {
                endPoint = hit.point;
                var target = hit.collider.GetComponentInParent<CombatantHealth>();
                if (target != null && target != owner)
                {
                    if (CombatantTeam.AreEnemies(owner, target))
                    {
                        target.TakeDamage(weapon.Damage * damageMultiplier, owner);
                    }
                }
                else
                {
                    var destructible = hit.collider.GetComponentInParent<DestructibleArenaPiece>();
                    if (destructible != null)
                    {
                        var structureDamage = weapon.FireStyle == WeaponFireStyle.Scatter
                            ? Mathf.Max(weapon.Damage, 14f)
                            : Mathf.Max(weapon.Damage, 32f);
                        destructible.TakeDamage(structureDamage, hit.point, hit.normal, hit.collider, ray.direction);
                    }
                }
            }

            if (showPresentation)
            {
                StartCoroutine(ShowBeam(ResolveBeamStart(origin), endPoint, beamWidth));
            }
        }

        private Vector3 ResolveBeamStart(Vector3 origin)
        {
            if (viewModel != null && viewModel.Muzzle != null)
            {
                return viewModel.Muzzle.position;
            }

            return firePoint != null ? firePoint.position : origin;
        }

        private static Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
        {
            if (spreadDegrees <= 0f)
            {
                return direction;
            }

            var rotation = Quaternion.LookRotation(direction);
            var radius = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            var offset = Random.insideUnitCircle * radius;
            var spread = rotation * new Vector3(offset.x, offset.y, 1f);
            return spread.normalized;
        }

        private void LaunchGrenade(Vector3 origin, Vector3 direction, WeaponDefinition stats)
        {
            var projectileObject = new GameObject("Plasma Grenade Projectile");
            projectileObject.transform.position = origin + direction * 0.45f + Vector3.up * 0.05f;
            var projectile = projectileObject.AddComponent<GrenadeProjectile>();
            var velocity = direction * Mathf.Max(6f, stats.ThrowSpeed) + Vector3.up * 2.6f;
            projectile.Configure(owner, arenaTheme, stats.Damage * damageMultiplier, stats.ExplosionRadius, velocity);
        }

        private void PlayFireAudio(WeaponDefinition weapon)
        {
            var audio = ArenaAudio.Instance;
            if (audio == null)
            {
                return;
            }

            switch (weapon.FireStyle)
            {
                case WeaponFireStyle.Scatter:
                    audio.PlayShotgunBlast(transform.position);
                    break;
                case WeaponFireStyle.Thrown:
                    audio.PlayGrenadeThrow(transform.position);
                    break;
                default:
                    audio.PlayGunshot(transform.position);
                    break;
            }
        }

        private bool TryGetWeaponHit(Ray ray, float range, out RaycastHit bestHit)
        {
            bestHit = default;
            var hits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Collide);
            if (hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                var target = hit.collider.GetComponentInParent<CombatantHealth>();
                if (target != null && target != owner && target.IsAlive)
                {
                    if (!CombatantTeam.AreEnemies(owner, target))
                    {
                        continue;
                    }

                    bestHit = hit;
                    return true;
                }

                if (target != null && !target.IsAlive)
                {
                    continue;
                }

                var destructible = hit.collider.GetComponentInParent<DestructibleArenaPiece>();
                if (destructible != null)
                {
                    if (destructible.AllowsProjectilePassThrough(hit.point, hit.normal))
                    {
                        // The ray entered through an existing hole. It may still strike
                        // surviving material deeper inside this piece (e.g. the wall of an
                        // angled tunnel) that the coarse colliders cannot report separately.
                        if (!destructible.TryGetMaterialHitAlongRay(hit.point, ray.direction, out var materialPoint, out var materialNormal))
                        {
                            continue;
                        }

                        var materialDistance = hit.distance + Vector3.Distance(hit.point, materialPoint);
                        if (TryFindNearTargetBehindCover(hits, i, materialDistance, out bestHit))
                        {
                            return true;
                        }

                        hit.point = materialPoint;
                        hit.normal = materialNormal;
                        hit.distance = materialDistance;
                        bestHit = hit;
                        return true;
                    }

                    if (TryFindNearTargetBehindCover(hits, i, hit.distance, out bestHit))
                    {
                        return true;
                    }

                    bestHit = hit;
                    return true;
                }

                if (ShouldLetProjectileReachFloor(hit.collider))
                {
                    continue;
                }

                if (hit.collider.isTrigger)
                {
                    continue;
                }

                if (TryFindNearTargetBehindCover(hits, i, hit.distance, out bestHit))
                {
                    return true;
                }

                bestHit = hit;
                return true;
            }

            return false;
        }

        private bool TryFindNearTargetBehindCover(RaycastHit[] hits, int blockerIndex, float blockerDistance, out RaycastHit targetHit)
        {
            targetHit = default;
            for (var i = blockerIndex + 1; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance - blockerDistance > NearCoverTargetForgiveness)
                {
                    return false;
                }

                var target = hit.collider.GetComponentInParent<CombatantHealth>();
                if (target != null && target != owner && target.IsAlive)
                {
                    if (!CombatantTeam.AreEnemies(owner, target))
                    {
                        continue;
                    }

                    targetHit = hit;
                    return true;
                }

                if (target != null && !target.IsAlive)
                {
                    continue;
                }
            }

            return false;
        }

        private static bool ShouldLetProjectileReachFloor(Collider collider)
        {
            return collider != null && collider.GetComponentInParent<HealingStation>() != null;
        }

        private IEnumerator ShowBeam(Vector3 start, Vector3 end, float width = 0.055f)
        {
            var beam = new GameObject("Pulse Beam");
            var line = beam.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            // Distant tracers must stay at least a pixel wide on screen, otherwise the
            // rasterizer slices the thin line into a stippled dash pattern.
            var viewer = Camera.main;
            if (viewer != null)
            {
                var midDistance = Vector3.Distance(viewer.transform.position, (start + end) * 0.5f);
                width = Mathf.Max(width, midDistance * 0.0014f);
            }

            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.sharedMaterial = beamMaterial;

            yield return new WaitForSeconds(0.055f);
            Destroy(beam);
        }
    }
}
