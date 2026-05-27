using UnityEngine;

namespace ArenaShooter
{
    public sealed class ScrapPickup : MonoBehaviour
    {
        private MatchController match;
        private Transform model;
        private int amount;
        private float spinOffset;

        public void Configure(MatchController owner, ArenaTheme theme, int scrapAmount)
        {
            match = owner;
            amount = Mathf.Max(1, scrapAmount);
            spinOffset = Random.Range(0f, 12f);
            BuildVisual(theme);
        }

        private void Update()
        {
            if (model == null)
            {
                return;
            }

            model.Rotate(0f, 100f * Time.deltaTime, 0f, Space.Self);
            model.localPosition = Vector3.up * (0.28f + Mathf.Sin(Time.time * 3.2f + spinOffset) * 0.08f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerFpsController>() == null)
            {
                return;
            }

            match?.AddScrap(amount);
            Destroy(gameObject);
        }

        private void BuildVisual(ArenaTheme theme)
        {
            model = new GameObject("Floating Scrap Cluster").transform;
            model.SetParent(transform, false);
            model.localPosition = Vector3.up * 0.28f;

            CreateShard("Scrap Rib", theme.Scrap, new Vector3(-0.12f, 0f, 0f), new Vector3(0.08f, 0.26f, 0.08f), new Vector3(16f, 0f, 28f));
            CreateShard("Scrap Plate", theme.Pillar, new Vector3(0.1f, 0.04f, 0.04f), new Vector3(0.24f, 0.055f, 0.16f), new Vector3(0f, 34f, -14f));
            CreateShard("Scrap Core", theme.Scrap, new Vector3(0.02f, 0.14f, -0.08f), new Vector3(0.12f, 0.12f, 0.12f), new Vector3(45f, 35f, 0f));

            var light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, 0.56f, 0.16f);
            light.range = 2.8f;
            light.intensity = 1.1f;
        }

        private void CreateShard(string objectName, Material material, Vector3 localPosition, Vector3 localScale, Vector3 localRotation)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = objectName;
            shard.transform.SetParent(model, false);
            shard.transform.localPosition = localPosition;
            shard.transform.localScale = localScale;
            shard.transform.localRotation = Quaternion.Euler(localRotation);

            if (shard.TryGetComponent<Collider>(out var collider))
            {
                Destroy(collider);
            }

            if (shard.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
