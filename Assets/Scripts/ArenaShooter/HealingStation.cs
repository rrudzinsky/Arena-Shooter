using System.Collections.Generic;
using UnityEngine;

namespace ArenaShooter
{
    public sealed class HealingStation : MonoBehaviour
    {
        [SerializeField] private float healAmount = 50f;
        [SerializeField] private float cooldown = 12f;

        private Renderer[] activeVisualRenderers;
        private Renderer[] perimeterVisualRenderers;
        private Renderer[] feedbackRingRenderers;
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        private Material readyMaterial;
        private Material cooldownMaterial;
        private Material feedbackMaterial;
        private Light stationLight;
        private float readyAt;
        private float feedbackUntil;

        public bool IsAvailable => Time.time >= readyAt;

        public void Configure(float amount, float cooldownSeconds, Material activeMaterial, Material inactiveMaterial)
        {
            healAmount = amount;
            cooldown = cooldownSeconds;
            readyMaterial = activeMaterial;
            cooldownMaterial = inactiveMaterial;
            feedbackMaterial = CreateFeedbackMaterial(activeMaterial);
            activeVisualRenderers = FindActiveVisualRenderers();
            perimeterVisualRenderers = FindRenderersByName("perimeter");
            feedbackRingRenderers = FindRenderersByName("activation feedback");
            CacheOriginalMaterials(activeVisualRenderers);
            CacheOriginalMaterials(perimeterVisualRenderers);
            CacheOriginalMaterials(feedbackRingRenderers);
            stationLight = GetComponentInChildren<Light>();
            RefreshVisuals();
        }

        private void Update()
        {
            RefreshVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHeal(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHeal(other);
        }

        private void TryHeal(Collider other)
        {
            var health = other.GetComponentInParent<CombatantHealth>();
            if (health == null)
            {
                return;
            }

            var isPlayer = other.GetComponentInParent<PlayerFpsController>() != null;
            if (!TryHeal(health, health.MaxHealth, isPlayer))
            {
                return;
            }
        }

        public bool TryHealToFull(CombatantHealth health)
        {
            return TryHeal(health, health != null ? health.MaxHealth : 0f, false);
        }

        private bool TryHeal(CombatantHealth health, float amount, bool ignoreCooldown)
        {
            if ((!ignoreCooldown && !IsAvailable) || health == null || !health.Heal(amount))
            {
                return false;
            }

            readyAt = Time.time + cooldown;
            feedbackUntil = Time.time + 0.85f;
            RefreshVisuals();
            return true;
        }

        private void RefreshVisuals()
        {
            var showingFeedback = Time.time < feedbackUntil;
            if (activeVisualRenderers != null)
            {
                var material = showingFeedback ? feedbackMaterial : IsAvailable ? readyMaterial : cooldownMaterial;
                foreach (var renderer in activeVisualRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = material;
                    }
                }
            }

            SetRenderersMaterial(perimeterVisualRenderers, showingFeedback ? feedbackMaterial : null);
            SetRenderersEnabled(feedbackRingRenderers, showingFeedback, feedbackMaterial);

            if (stationLight != null)
            {
                stationLight.intensity = showingFeedback ? 4.5f : IsAvailable ? 2.8f : 0.35f;
            }
        }

        private Renderer[] FindActiveVisualRenderers()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var activeRenderers = new List<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.name.ToLowerInvariant().Contains("cross"))
                {
                    activeRenderers.Add(renderer);
                }
            }

            return activeRenderers.Count > 0 ? activeRenderers.ToArray() : renderers;
        }

        private Renderer[] FindRenderersByName(string key)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var matches = new List<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.name.ToLowerInvariant().Contains(key))
                {
                    matches.Add(renderer);
                }
            }

            return matches.ToArray();
        }

        private void CacheOriginalMaterials(Renderer[] renderers)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var renderer in renderers)
            {
                if (renderer != null && !originalMaterials.ContainsKey(renderer))
                {
                    originalMaterials[renderer] = renderer.sharedMaterials;
                }
            }
        }

        private void SetRenderersMaterial(Renderer[] renderers, Material material)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
                else if (originalMaterials.TryGetValue(renderer, out var materials))
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private void SetRenderersEnabled(Renderer[] renderers, bool enabled, Material material)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = enabled;
                if (enabled && material != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private static Material CreateFeedbackMaterial(Material source)
        {
            var material = new Material(source) { name = "Health Pad Bright Feedback" };
            var brightBase = new Color(0.12f, 1.35f, 0.42f);
            var brightEmission = new Color(0.25f, 7.0f, 1.4f);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", brightBase);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", brightBase);
            }

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", brightEmission);
            }

            return material;
        }
    }
}
