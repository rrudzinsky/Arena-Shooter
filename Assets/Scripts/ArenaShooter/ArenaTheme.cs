using UnityEngine;

namespace ArenaShooter
{
    public sealed class ArenaTheme
    {
        public Material Floor { get; }
        public Material Wall { get; }
        public Material Pillar { get; }
        public Material Ceiling { get; }
        public Material GateInterior { get; }
        public Material GateEndWall { get; }
        public Material FloorGrid { get; }
        public Material NeonA { get; }
        public Material NeonB { get; }
        public Material Player { get; }
        public Material Opponent { get; }
        public Material DroidArmor { get; }
        public Material DroidJoint { get; }
        public Material DroidEye { get; }
        public Material Pickup { get; }
        public Material Scrap { get; }
        public Material Health { get; }
        public Material MedicalWhite { get; }
        public Material Beam { get; }

        public ArenaTheme()
        {
            Floor = CreateUnlit("Synthwave Matte Black Floor", new Color(0.0015f, 0.001f, 0.004f));
            Wall = CreateLit("Synthwave Matte Black Wall", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            Pillar = CreateLit("Synthwave Matte Black Pillar", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            Ceiling = CreateLit("Synthwave Matte Black Ceiling", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            GateInterior = CreateUnlit("Gate Interior Matte Black Floor", new Color(0.002f, 0.0015f, 0.006f));
            GateEndWall = CreateLit("Spawn Corridor Matte Black Rear Wall", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            FloorGrid = CreateUnlit("Clean Violet Floor Line", new Color(0.72f, 0.16f, 1.25f), new Color(0.72f, 0.16f, 1.25f));
            NeonA = CreateUnlit("Clean Cyan Neon", new Color(0.08f, 1.32f, 1.72f), new Color(0.08f, 1.32f, 1.72f));
            NeonB = CreateUnlit("Clean Magenta Neon", new Color(1.28f, 0.16f, 1.36f), new Color(1.28f, 0.16f, 1.36f));
            Player = CreateLit("Player Matte Black Suit", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            Opponent = CreateLit("Opponent Matte Black Suit", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            DroidArmor = CreateLit("Synthwave Droid Matte Black Body", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            DroidJoint = CreateLit("Synthwave Droid Matte Black Joint", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            DroidEye = CreateLit("Synthwave Droid Matte Black Optic", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            Pickup = CreateLit("Pickup Matte Black Body", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            Scrap = CreateLit("Gold Hot Scrap Metal", new Color(0.9f, 0.60f, 0.22f), new Color(1.6f, 0.72f, 0.08f), 0.9f, 0.28f);
            Health = CreateLit("Medical Matte Black Body", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            MedicalWhite = CreateLit("Medical Matte Black Armor", new Color(0.0015f, 0.001f, 0.004f), Color.black, 0f, 0f);
            Beam = CreateLit("Weapon Beam", new Color(0.32f, 0.95f, 1f), new Color(0.05f, 2.2f, 2.7f));
        }

        private static Material CreateUnlit(string name, Color baseColor, Color emission = default)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = name };
            SetMaterialColor(material, baseColor);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission);
                }
            }

            DisableReflections(material);
            return material;
        }

        private static Material CreateLit(string name, Color baseColor, Color emission, float metallic = 0f, float smoothness = 0.38f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission);
                }
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            else if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (Mathf.Approximately(smoothness, 0f))
            {
                DisableReflections(material);
            }

            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void DisableReflections(Material material)
        {
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0f);
            }
            else if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0f);
            }

            if (material.HasProperty("_EnvironmentReflections"))
            {
                material.SetFloat("_EnvironmentReflections", 0f);
            }

            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0f);
            }

            if (material.HasProperty("_GlossyReflections"))
            {
                material.SetFloat("_GlossyReflections", 0f);
            }
        }
    }
}
