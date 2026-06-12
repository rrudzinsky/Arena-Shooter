using UnityEngine;

namespace ArenaShooter
{
    public static class PickupVisuals
    {
        public static void BuildGunPickup(Transform parent, ArenaTheme theme, WeaponModelKind kind = WeaponModelKind.PulsePistol)
        {
            switch (kind)
            {
                case WeaponModelKind.ScatterShotgun:
                    BuildShotgunPickup(parent, theme);
                    return;
                case WeaponModelKind.PlasmaGrenade:
                    BuildGrenadePickup(parent, theme);
                    return;
            }

            var model = CreateModelRoot(parent);
            if (PulsePistolAsset.TryBuildPickupModel(model, theme))
            {
                CreateGlowStand(parent, theme.NeonA, new Color(0.1f, 0.85f, 1f), 4.2f);
                return;
            }

            CreatePrimitive("Pickup Gun Body", PrimitiveType.Cube, theme.Wall, model, new Vector3(0f, 0f, 0f), new Vector3(0.82f, 0.18f, 0.22f), Vector3.zero);
            CreatePrimitive("Pickup Gun Barrel", PrimitiveType.Cube, theme.NeonA, model, new Vector3(0.44f, 0.04f, 0f), new Vector3(0.42f, 0.09f, 0.11f), Vector3.zero);
            CreatePrimitive("Pickup Gun Grip", PrimitiveType.Cube, theme.Wall, model, new Vector3(-0.18f, -0.22f, 0f), new Vector3(0.17f, 0.42f, 0.17f), new Vector3(0f, 0f, -18f));
            CreatePrimitive("Pickup Gun Core", PrimitiveType.Cube, theme.Pickup, model, new Vector3(0.04f, 0.09f, 0f), new Vector3(0.18f, 0.08f, 0.24f), Vector3.zero);
            DroidRenderSetup.Apply(model.gameObject, StylizedOutlineCategory.Gun);
            CreateGlowStand(parent, theme.NeonA, new Color(0.1f, 0.85f, 1f), 4.2f);
        }

        private static void BuildShotgunPickup(Transform parent, ArenaTheme theme)
        {
            var model = CreateModelRoot(parent);
            if (ShotgunAsset.TryBuildPickupModel(model, theme))
            {
                CreateGlowStand(parent, theme.NeonB, new Color(1f, 0.2f, 0.95f), 4.2f);
                return;
            }

            CreatePrimitive("Pickup Shotgun Body", PrimitiveType.Cube, theme.Wall, model, new Vector3(0f, 0f, 0f), new Vector3(0.95f, 0.2f, 0.2f), Vector3.zero);
            CreatePrimitive("Pickup Shotgun Barrels", PrimitiveType.Cube, theme.Wall, model, new Vector3(0.5f, 0.05f, 0f), new Vector3(0.42f, 0.1f, 0.17f), Vector3.zero);
            CreatePrimitive("Pickup Shotgun Muzzle", PrimitiveType.Cube, theme.NeonA, model, new Vector3(0.72f, 0.05f, 0f), new Vector3(0.03f, 0.06f, 0.15f), Vector3.zero);
            CreatePrimitive("Pickup Shotgun Pump", PrimitiveType.Cube, theme.Pillar, model, new Vector3(0.3f, -0.12f, 0f), new Vector3(0.26f, 0.1f, 0.16f), Vector3.zero);
            CreatePrimitive("Pickup Shotgun Grip", PrimitiveType.Cube, theme.Wall, model, new Vector3(-0.32f, -0.2f, 0f), new Vector3(0.16f, 0.36f, 0.15f), new Vector3(0f, 0f, -24f));
            CreatePrimitive("Pickup Shotgun Stock", PrimitiveType.Cube, theme.Wall, model, new Vector3(-0.52f, 0.02f, 0f), new Vector3(0.2f, 0.18f, 0.14f), Vector3.zero);
            DroidRenderSetup.Apply(model.gameObject, StylizedOutlineCategory.Gun);
            CreateGlowStand(parent, theme.NeonB, new Color(1f, 0.2f, 0.95f), 4.2f);
        }

        private static void BuildGrenadePickup(Transform parent, ArenaTheme theme)
        {
            var model = CreateModelRoot(parent);
            if (GrenadeAsset.TryBuildPickupModel(model, theme))
            {
                CreateGlowStand(parent, theme.Scrap, new Color(1f, 0.62f, 0.1f), 4f);
                return;
            }

            CreatePrimitive("Pickup Grenade Shell", PrimitiveType.Sphere, theme.Wall, model, new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.36f, 0.36f), Vector3.zero);
            CreatePrimitive("Pickup Grenade Band", PrimitiveType.Cylinder, theme.NeonA, model, new Vector3(0f, 0.08f, 0f), new Vector3(0.38f, 0.035f, 0.38f), Vector3.zero);
            CreatePrimitive("Pickup Grenade Cap", PrimitiveType.Cylinder, theme.Pillar, model, new Vector3(0f, 0.28f, 0f), new Vector3(0.12f, 0.05f, 0.12f), Vector3.zero);
            DroidRenderSetup.Apply(model.gameObject, StylizedOutlineCategory.Gun);
            CreateGlowStand(parent, theme.Scrap, new Color(1f, 0.62f, 0.1f), 4f);
        }

        public static void BuildHealthPickup(Transform parent, ArenaTheme theme)
        {
            var model = CreateModelRoot(parent);
            if (HealthPackAsset.TryBuildPickupModel(model, theme))
            {
                CreateGlowStand(parent, theme.Health, new Color(1f, 0.08f, 0.05f), 3.6f);
                return;
            }

            CreatePrimitive("Med Pack Body", PrimitiveType.Cube, theme.MedicalWhite, model, Vector3.zero, new Vector3(0.52f, 0.2f, 0.38f), Vector3.zero);
            CreatePrimitive("Med Cross Vertical", PrimitiveType.Cube, theme.Health, model, new Vector3(0f, 0.12f, 0f), new Vector3(0.09f, 0.09f, 0.43f), Vector3.zero);
            CreatePrimitive("Med Cross Horizontal", PrimitiveType.Cube, theme.Health, model, new Vector3(0f, 0.13f, 0f), new Vector3(0.36f, 0.08f, 0.09f), Vector3.zero);
            DroidRenderSetup.Apply(model.gameObject, StylizedOutlineCategory.Medical);
            CreateGlowStand(parent, theme.Health, new Color(1f, 0.08f, 0.05f), 3.6f);
        }

        public static void BuildAmmoPickup(Transform parent, ArenaTheme theme)
        {
            var model = CreateModelRoot(parent);
            if (AmmoPackAsset.TryBuildPickupModel(model, theme))
            {
                CreateGlowStand(parent, theme.Pickup, new Color(1f, 0.82f, 0.15f), 3.2f);
                return;
            }

            CreatePrimitive("Ammo Battery", PrimitiveType.Cube, theme.Pickup, model, Vector3.zero, new Vector3(0.24f, 0.48f, 0.24f), Vector3.zero);
            CreatePrimitive("Ammo Cap Top", PrimitiveType.Cube, theme.Wall, model, new Vector3(0f, 0.29f, 0f), new Vector3(0.3f, 0.08f, 0.3f), Vector3.zero);
            CreatePrimitive("Ammo Cap Bottom", PrimitiveType.Cube, theme.Wall, model, new Vector3(0f, -0.29f, 0f), new Vector3(0.3f, 0.08f, 0.3f), Vector3.zero);
            DroidRenderSetup.Apply(model.gameObject, StylizedOutlineCategory.Ammo);
            CreateGlowStand(parent, theme.Pickup, new Color(1f, 0.82f, 0.15f), 3.2f);
        }

        private static Transform CreateModelRoot(Transform parent)
        {
            var model = new GameObject("Floating Pickup Model").transform;
            model.SetParent(parent, false);
            model.localPosition = Vector3.up * 0.18f;
            return model;
        }

        private static void CreateGlowStand(Transform parent, Material material, Color lightColor, float range)
        {
            if (PickupLiftAuraAsset.TryBuild(parent, material, lightColor, range))
            {
                return;
            }

            var outer = CreatePrimitive("Pickup Glow Outer Ring", PrimitiveType.Cylinder, material, parent, new Vector3(0f, -0.46f, 0f), new Vector3(0.7f, 0.012f, 0.7f), Vector3.zero);
            var inner = CreatePrimitive("Pickup Glow Inner Ring", PrimitiveType.Cylinder, material, parent, new Vector3(0f, -0.42f, 0f), new Vector3(0.38f, 0.01f, 0.38f), Vector3.zero);
            ConfigureGlowRenderer(outer, material, 0.035f);
            ConfigureGlowRenderer(inner, material, 0.06f);

            var emitter = CreatePrimitive("Pickup Emitter Core", PrimitiveType.Cylinder, material, parent, new Vector3(0f, -0.36f, 0f), new Vector3(0.18f, 0.04f, 0.18f), Vector3.zero);
            ConfigureGlowRenderer(emitter, material, 0.18f);

            var lightObject = new GameObject("Pickup Aura Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = lightColor;
            light.range = range;
            light.intensity = 1.5f;
        }

        private static void ConfigureGlowRenderer(GameObject glowObject, Material material, float alpha)
        {
            if (glowObject.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = CreateTransparentAuraMaterial(material, alpha);
            }
        }

        private static Material CreateTransparentAuraMaterial(Material source, float alpha)
        {
            var material = new Material(source)
            {
                name = $"{source.name} Transparent Aura"
            };

            if (material.HasProperty("_BaseColor"))
            {
                var color = material.GetColor("_BaseColor");
                color.a = alpha;
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                var color = material.GetColor("_Color");
                color.a = alpha;
                material.SetColor("_Color", color);
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 1f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_BLENDMODE_ADDITIVE");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            return material;
        }

        private static GameObject CreatePrimitive(string objectName, PrimitiveType type, Material material, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localRotation)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.transform.localRotation = Quaternion.Euler(localRotation);

            if (primitive.TryGetComponent<Collider>(out var collider))
            {
                Object.Destroy(collider);
            }

            if (primitive.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                DroidRenderSetup.ApplyRenderer(renderer, StylizedOutlineCategory.None);
            }

            return primitive;
        }
    }
}
