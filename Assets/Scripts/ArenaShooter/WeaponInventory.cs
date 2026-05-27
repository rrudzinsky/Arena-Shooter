using System.Collections;
using UnityEngine;

namespace ArenaShooter
{
    public sealed class WeaponInventory : MonoBehaviour
    {
        [SerializeField] private Transform firePoint;
        [SerializeField] private CombatantHealth owner;

        private Material beamMaterial;
        private FirstPersonViewModel viewModel;
        private WeaponDefinition currentWeapon;
        private float nextFireTime;
        private float damageMultiplier = 1f;
        private int ammo;
        private int maxAmmo;
        private const float NearCoverTargetForgiveness = 1.05f;

        public bool HasWeapon => currentWeapon != null;
        public string WeaponName => currentWeapon == null ? "Unarmed" : currentWeapon.DisplayName;
        public int Ammo => ammo;
        public int MaxAmmo => maxAmmo;
        public float CurrentDamage => currentWeapon != null ? currentWeapon.Damage * damageMultiplier : 0f;

        private void Awake()
        {
            if (owner == null)
            {
                owner = GetComponent<CombatantHealth>();
            }
        }

        public void Configure(CombatantHealth weaponOwner, Transform muzzle, Material tracerMaterial)
        {
            owner = weaponOwner;
            firePoint = muzzle;
            beamMaterial = tracerMaterial;
        }

        public void ConfigureViewModel(FirstPersonViewModel model)
        {
            viewModel = model;
            if (viewModel != null)
            {
                viewModel.SetWeaponVisible(currentWeapon != null);
                viewModel.SetAmmo(ammo, maxAmmo);
            }
        }

        public void SetAiming(bool aiming)
        {
            viewModel?.SetAiming(aiming && HasWeapon);
        }

        public void Equip(WeaponDefinition definition)
        {
            currentWeapon = definition.Clone();
            ammo = currentWeapon.Ammo;
            maxAmmo = currentWeapon.Ammo;
            nextFireTime = 0f;
            viewModel?.SetWeaponVisible(true);
            viewModel?.SetAmmo(ammo, maxAmmo);
        }

        public bool TryAddAmmo(int amount)
        {
            if (currentWeapon == null || amount <= 0 || ammo >= maxAmmo)
            {
                return false;
            }

            ammo = Mathf.Min(maxAmmo, ammo + amount);
            viewModel?.SetAmmo(ammo, maxAmmo);
            return true;
        }

        public bool TryUpgradeCurrentWeapon(float damageBonus, int ammoBonus)
        {
            if (currentWeapon == null)
            {
                return false;
            }

            currentWeapon.Damage += Mathf.Max(0f, damageBonus);
            if (ammoBonus > 0)
            {
                maxAmmo += ammoBonus;
                ammo = Mathf.Min(maxAmmo, ammo + ammoBonus);
                viewModel?.SetAmmo(ammo, maxAmmo);
            }

            return true;
        }

        public void SetDamageMultiplier(float multiplier)
        {
            damageMultiplier = Mathf.Max(1f, multiplier);
        }

        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (currentWeapon == null || ammo <= 0 || Time.time < nextFireTime || owner == null || !owner.IsAlive)
            {
                return false;
            }

            nextFireTime = Time.time + currentWeapon.Cooldown;
            ammo--;
            viewModel?.SetAmmo(ammo, maxAmmo);
            viewModel?.PlayFire();
            ArenaAudio.Instance?.PlayGunshot(transform.position);
            ArenaNoise.EmitGunfire(origin, direction, owner, 64f);
            if (owner.GetComponent<PlayerFpsController>() != null)
            {
                ArenaNoise.EmitPlayerNoise(origin, 58f);
            }

            var ray = new Ray(origin, direction.normalized);
            var endPoint = origin + ray.direction * currentWeapon.Range;

            if (TryGetWeaponHit(ray, currentWeapon.Range, out var hit))
            {
                endPoint = hit.point;
                var target = hit.collider.GetComponentInParent<CombatantHealth>();
                if (target != null && target != owner)
                {
                    if (CombatantTeam.AreEnemies(owner, target))
                    {
                        target.TakeDamage(currentWeapon.Damage * damageMultiplier, owner);
                    }
                }
                else
                {
                    var destructible = hit.collider.GetComponentInParent<DestructibleArenaPiece>();
                    if (destructible != null)
                    {
                        var structureDamage = Mathf.Max(currentWeapon.Damage, 32f);
                        destructible.TakeDamage(structureDamage, hit.point, hit.normal, hit.collider);
                    }
                }
            }

            var beamStart = firePoint != null ? firePoint.position : origin;
            StartCoroutine(ShowBeam(beamStart, endPoint));
            return true;
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
                        continue;
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

        private IEnumerator ShowBeam(Vector3 start, Vector3 end)
        {
            var beam = new GameObject("Pulse Beam");
            var line = beam.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.widthMultiplier = 0.055f;
            line.numCapVertices = 3;
            line.sharedMaterial = beamMaterial;

            yield return new WaitForSeconds(0.055f);
            Destroy(beam);
        }
    }
}
