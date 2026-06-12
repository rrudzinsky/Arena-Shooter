using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter
{
    public sealed class GrenadeProjectile : MonoBehaviour
    {
        private const float FuseSeconds = 2.6f;
        private const float ArmSeconds = 0.12f;
        private const float StructureDamage = 130f;
        private const int MaxStructurePieces = 12;

        private CombatantHealth owner;
        private ArenaTheme theme;
        private float damage = 88f;
        private float explosionRadius = 5.2f;
        private float armedAt;
        private float detonateAt;
        private bool exploded;
        private Rigidbody body;

        public void Configure(CombatantHealth thrower, ArenaTheme arenaTheme, float explosionDamage, float radius, Vector3 initialVelocity)
        {
            owner = thrower;
            theme = arenaTheme;
            damage = explosionDamage;
            explosionRadius = Mathf.Max(1.5f, radius);
            armedAt = Time.time + ArmSeconds;
            detonateAt = Time.time + FuseSeconds;

            BuildVisual();

            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.13f;

            body = gameObject.AddComponent<Rigidbody>();
            body.mass = 0.62f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = initialVelocity;
            body.angularVelocity = Random.onUnitSphere * 7f;

            if (owner != null)
            {
                foreach (var ownerCollider in owner.GetComponentsInChildren<Collider>(true))
                {
                    if (ownerCollider != null && !ownerCollider.isTrigger)
                    {
                        Physics.IgnoreCollision(sphere, ownerCollider, true);
                    }
                }
            }
        }

        private void BuildVisual()
        {
            if (theme != null && GrenadeAsset.TryBuildProjectileModel(transform, theme))
            {
                return;
            }

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Grenade Core Fallback";
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * 0.22f;
            if (core.TryGetComponent<Collider>(out var coreCollider))
            {
                Destroy(coreCollider);
            }

            if (theme != null && core.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = theme.NeonA;
            }
        }

        private void Update()
        {
            if (exploded)
            {
                return;
            }

            if (Time.time >= detonateAt || transform.position.y < -60f)
            {
                Explode();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (exploded || Time.time < armedAt)
            {
                return;
            }

            Explode();
        }

        private void Explode()
        {
            if (exploded)
            {
                return;
            }

            exploded = true;
            var center = transform.position;

            ArenaAudio.Instance?.PlayExplosion(center);
            if (owner != null)
            {
                ArenaNoise.EmitGunfire(center, Vector3.up, owner, 74f);
            }

            DamageCombatants(center);
            DamageStructures(center);
            SpawnDetonationEffect(center);
            Destroy(gameObject);
        }

        private void DamageCombatants(Vector3 center)
        {
            var hits = Physics.OverlapSphere(center, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
            var damaged = new HashSet<CombatantHealth>();
            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                var target = hit.GetComponentInParent<CombatantHealth>();
                if (target == null || target == owner || !target.IsAlive || damaged.Contains(target))
                {
                    continue;
                }

                if (owner != null && !CombatantTeam.AreEnemies(owner, target))
                {
                    continue;
                }

                damaged.Add(target);
                var point = hit.ClosestPoint(center);
                var distance = Vector3.Distance(center, point);
                var falloff = Mathf.InverseLerp(explosionRadius * 0.25f, explosionRadius, distance);
                var amount = damage * Mathf.Lerp(1f, 0.3f, falloff);

                // soften blasts that have to pass through cover
                if (Physics.Linecast(center, point + Vector3.up * 0.05f, out var blocker, ~0, QueryTriggerInteraction.Ignore) &&
                    blocker.collider != null &&
                    blocker.collider.GetComponentInParent<CombatantHealth>() != target)
                {
                    amount *= 0.55f;
                }

                target.TakeDamage(amount, owner);
            }
        }

        private void DamageStructures(Vector3 center)
        {
            var hits = Physics.OverlapSphere(center, explosionRadius * 0.92f, ~0, QueryTriggerInteraction.Ignore);
            var damagedPieces = new HashSet<DestructibleArenaPiece>();
            foreach (var hit in hits)
            {
                if (hit == null || damagedPieces.Count >= MaxStructurePieces)
                {
                    break;
                }

                var piece = hit.GetComponentInParent<DestructibleArenaPiece>();
                if (piece == null || damagedPieces.Contains(piece))
                {
                    continue;
                }

                damagedPieces.Add(piece);
                var point = hit.ClosestPoint(center);
                var outward = point - center;
                if (outward.sqrMagnitude < 0.0001f)
                {
                    outward = Vector3.forward;
                }

                outward.Normalize();
                piece.TakeDamage(StructureDamage, point, -outward, hit, outward);
            }
        }

        private void SpawnDetonationEffect(Vector3 center)
        {
            var effectObject = new GameObject("Plasma Detonation");
            effectObject.transform.position = center;
            var effect = effectObject.AddComponent<GrenadeExplosionEffect>();
            effect.Configure(theme, explosionRadius);
        }
    }

    public sealed class GrenadeExplosionEffect : MonoBehaviour
    {
        private const float Duration = 0.34f;

        private Transform core;
        private Transform ring;
        private Light flash;
        private float elapsed;
        private float radius = 5f;

        public void Configure(ArenaTheme theme, float explosionRadius)
        {
            radius = Mathf.Max(1.5f, explosionRadius);

            core = CreateBlastSphere("Detonation Core", theme != null ? theme.Beam : null, 0.6f);
            ring = CreateBlastSphere("Detonation Ring", theme != null ? theme.NeonB : null, 0.9f);
            ring.localScale = new Vector3(1.2f, 0.18f, 1.2f);

            var lightObject = new GameObject("Detonation Flash");
            lightObject.transform.SetParent(transform, false);
            flash = lightObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.shadows = LightShadows.None;
            flash.color = new Color(0.45f, 0.95f, 1f);
            flash.range = radius * 2.4f;
            flash.intensity = 7.5f;
        }

        private Transform CreateBlastSphere(string sphereName, Material material, float startScale)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = sphereName;
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * startScale;
            if (sphere.TryGetComponent<Collider>(out var sphereCollider))
            {
                Destroy(sphereCollider);
            }

            if (sphere.TryGetComponent<Renderer>(out var renderer))
            {
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return sphere.transform;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / Duration);
            var eased = 1f - (1f - t) * (1f - t);

            if (core != null)
            {
                core.localScale = Vector3.one * Mathf.Lerp(0.6f, radius * 1.5f, eased);
            }

            if (ring != null)
            {
                var ringScale = Mathf.Lerp(1.2f, radius * 2.2f, eased);
                ring.localScale = new Vector3(ringScale, Mathf.Lerp(0.18f, 0.05f, t), ringScale);
            }

            if (flash != null)
            {
                flash.intensity = Mathf.Lerp(7.5f, 0f, t);
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
