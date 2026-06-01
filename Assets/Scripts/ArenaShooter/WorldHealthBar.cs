using UnityEngine;
using UnityEngine.UI;

namespace ArenaShooter
{
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private CombatantHealth health;
        private Camera targetCamera;
        private Canvas canvas;
        private Image fill;
        private Image shieldFill;
        private Image shieldBack;
        private Text nameText;
        private float nextVisualUpdateAt;
        private Color fillColor = new Color(1f, 0.06f, 0.38f, 0.95f);
        private Color shieldColor = new Color(0.08f, 0.68f, 1f, 0.94f);
        private Color nameColor = new Color(1f, 0.78f, 0.96f);
        private Color nameOutlineColor = new Color(0f, 0f, 0f, 0.95f);
        private const float FullUpdateDistance = 32f;
        private const float VisibleDistance = 72f;

        public void Build(CombatantHealth trackedHealth, Camera cameraToFace)
        {
            Build(trackedHealth, cameraToFace, false);
        }

        public void Build(CombatantHealth trackedHealth, Camera cameraToFace, bool friendly)
        {
            health = trackedHealth;
            targetCamera = cameraToFace;
            if (friendly)
            {
                fillColor = new Color(0.18f, 1f, 0.38f, 0.95f);
                nameColor = new Color(0.78f, 1f, 0.84f);
                nameOutlineColor = new Color(0.02f, 0.55f, 0.12f, 0.98f);
            }

            if (health != null)
            {
                health.Died += OnTrackedDied;
            }

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = targetCamera;
            canvas.transform.localScale = Vector3.one * 0.012f;

            gameObject.AddComponent<CanvasScaler>();

            var background = CreateImage("Health Bar Back", transform, new Color(0f, 0f, 0f, 0.7f), new Vector2(0f, 0f), new Vector2(120f, 12f));
            fill = CreateImage("Health Bar Fill", background.transform, fillColor, new Vector2(0f, 0f), new Vector2(116f, 8f));
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(2f, 0f);

            shieldBack = CreateImage("Shield Bar Back", transform, new Color(0f, 0.02f, 0.06f, 0.68f), new Vector2(0f, 11f), new Vector2(120f, 8f));
            shieldFill = CreateImage("Shield Bar Fill", shieldBack.transform, shieldColor, new Vector2(0f, 0f), new Vector2(116f, 5f));
            shieldFill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            shieldFill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            shieldFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            shieldFill.rectTransform.anchoredPosition = new Vector2(2f, 0f);

            nameText = CreateText("Name", transform, trackedHealth.DisplayName, new Vector2(0f, 28f), 15);
        }

        private void LateUpdate()
        {
            if (health == null || targetCamera == null)
            {
                return;
            }

            if (Time.time < nextVisualUpdateAt)
            {
                return;
            }

            var toCamera = transform.position - targetCamera.transform.position;
            var distanceSqr = toCamera.sqrMagnitude;
            var viewport = targetCamera.WorldToViewportPoint(transform.position);
            var visible = health.IsAlive &&
                distanceSqr <= VisibleDistance * VisibleDistance &&
                viewport.z > 0f &&
                viewport.x >= -0.1f &&
                viewport.x <= 1.1f &&
                viewport.y >= -0.1f &&
                viewport.y <= 1.1f;
            if (canvas != null)
            {
                canvas.enabled = visible;
            }

            nextVisualUpdateAt = visible && distanceSqr <= FullUpdateDistance * FullUpdateDistance
                ? Time.time
                : Time.time + 0.22f;
            if (!visible)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position, Vector3.up);

            var ratio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
            if (fill != null)
            {
                fill.rectTransform.sizeDelta = new Vector2(116f * ratio, 8f);
            }

            var hasShield = health.MaxShield > 0f;
            if (shieldBack != null)
            {
                shieldBack.enabled = health.IsAlive && hasShield;
            }

            if (shieldFill != null)
            {
                shieldFill.enabled = health.IsAlive && hasShield;
                var shieldRatio = hasShield ? Mathf.Clamp01(health.CurrentShield / health.MaxShield) : 0f;
                shieldFill.rectTransform.sizeDelta = new Vector2(116f * shieldRatio, 5f);
            }

            if (nameText != null)
            {
                nameText.enabled = health.IsAlive;
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnTrackedDied;
            }
        }

        private void OnTrackedDied(CombatantHealth trackedHealth)
        {
            Destroy(gameObject);
        }

        private Image CreateImage(string objectName, Transform parent, Color color, Vector2 anchoredPosition, Vector2 size)
        {
            var imageObject = new GameObject(objectName);
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchoredPosition = anchoredPosition;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private Text CreateText(string objectName, Transform parent, string value, Vector2 anchoredPosition, int size)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            }

            text.fontSize = size;
            text.color = nameColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.rectTransform.anchoredPosition = anchoredPosition;
            text.rectTransform.sizeDelta = new Vector2(180f, 26f);

            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = nameOutlineColor;
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(0f, -2f);
            return text;
        }
    }
}
