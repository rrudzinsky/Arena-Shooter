using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    public sealed class AllOutWarDomeScoreboard : MonoBehaviour
    {
        private const int BankCount = 4;
        private const int BoardCurveSegments = 32;
        private const float RefreshInterval = 0.25f;
        private const float DomeLatitude = 0.5f;
        private const float BoardInwardOffset = 4.0f;
        private const float BoardWidth = 98.0f;
        private const float BoardTopPadding = 2.65f;
        private const float BoardBottomPadding = 2.75f;
        private const float HeaderHeight = 5.7f;
        private const float HeaderRowGap = 2.2f;
        private const float RowHeight = 4.95f;
        private const float RowStride = 5.85f;
        private const float RowFillWidth = 50.0f;
        private const float RowFillHeight = 1.75f;
        private const float RowLabelX = -32.0f;
        private const float RowCountX = 36.0f;
        private const float RowFillCenterX = 3.0f;
        private const float FaceOffset = -0.035f;
        private const float TextOffset = -0.09f;
        private const float HeaderTextSize = 0.72f;
        private const float RowLabelTextSize = 0.53f;
        private const float RowCountTextSize = 0.83f;

        private readonly List<ArmyRow> rows = new();
        private readonly Dictionary<int, Material> armyMaterials = new();
        private MatchController match;
        private Font font;
        private Vector3 center;
        private float radius;
        private float boardY;
        private float lookY;
        private float nextRefreshAt;
        private int builtArmyCount = -1;
        private Material panelMaterial;
        private Material rowBackMaterial;
        private Material fillBackMaterial;
        private Material purpleRailMaterial;

        public void Build(MatchController owner, ArenaLayout layout, float wallHeight)
        {
            match = owner;
            if (match == null || layout == null)
            {
                enabled = false;
                return;
            }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Arial", 36);
            }

            var stadiumMetrics = AllOutWarStadiumVisuals.CreateMetrics(layout.CircularCenter);
            center = stadiumMetrics.Center;
            var theta = AllOutWarStadiumVisuals.LatitudeToTheta(DomeLatitude);
            radius = Mathf.Max(8f, stadiumMetrics.Radius * Mathf.Cos(theta) - BoardInwardOffset);
            boardY = stadiumMetrics.BaseY + stadiumMetrics.Height * Mathf.Sin(theta);
            lookY = Mathf.Max(stadiumMetrics.BaseY + 18f, boardY * 0.18f);

            panelMaterial = CreateUnlitMaterial("Dome Scoreboard Black Glass", new Color(0.0015f, 0.0015f, 0.006f, 1f), new Color(0.012f, 0.002f, 0.032f, 1f));
            rowBackMaterial = CreateUnlitMaterial("Dome Scoreboard Row Black", new Color(0.004f, 0.003f, 0.012f, 1f), new Color(0.012f, 0.002f, 0.024f, 1f));
            fillBackMaterial = CreateUnlitMaterial("Dome Scoreboard Fill Back", new Color(0.016f, 0.004f, 0.032f, 1f), Color.black);
            purpleRailMaterial = CreateUnlitMaterial("Dome Scoreboard Hot Purple Rail", new Color(0.9f, 0.08f, 1.55f, 1f), new Color(1.8f, 0.1f, 3.35f, 1f));

            RebuildBoards();
            RefreshRows(true);
        }

        private void Update()
        {
            if (match == null || !match.IsAllOutWarMode || Time.time < nextRefreshAt)
            {
                return;
            }

            nextRefreshAt = Time.time + RefreshInterval;
            var armyCount = Mathf.Clamp(match.AllOutWarArmyCount, 0, 8);
            if (armyCount != builtArmyCount)
            {
                RebuildBoards();
            }

            RefreshRows(false);
        }

        private void RebuildBoards()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            rows.Clear();
            builtArmyCount = match != null ? Mathf.Clamp(match.AllOutWarArmyCount, 0, 8) : 0;
            if (builtArmyCount <= 0)
            {
                return;
            }

            var rowBlockHeight = RowHeight + Mathf.Max(0, builtArmyCount - 1) * RowStride;
            var boardHeight = BoardTopPadding + HeaderHeight + HeaderRowGap + rowBlockHeight + BoardBottomPadding;
            for (var bank = 0; bank < BankCount; bank++)
            {
                CreateBank(bank, boardHeight);
            }
        }

        private void CreateBank(int bank, float boardHeight)
        {
            var angle = bank / (float)BankCount * Mathf.PI * 2f;
            var root = CreateBoardRoot($"Dome Scoreboard {bank + 1}", angle);
            CreateCurvedQuad("Board Back", root.transform, Vector3.zero, new Vector2(BoardWidth, boardHeight), panelMaterial);
            CreateCurvedQuad("Top Purple Rail", root.transform, new Vector3(0f, boardHeight * 0.5f - 0.16f, FaceOffset), new Vector2(BoardWidth, 0.32f), purpleRailMaterial);
            CreateCurvedQuad("Bottom Purple Rail", root.transform, new Vector3(0f, -boardHeight * 0.5f + 0.14f, FaceOffset), new Vector2(BoardWidth, 0.28f), purpleRailMaterial);
            CreateCurvedQuad("Left Purple Rail", root.transform, new Vector3(-BoardWidth * 0.5f + 0.13f, 0f, FaceOffset), new Vector2(0.26f, boardHeight), purpleRailMaterial);
            CreateCurvedQuad("Right Purple Rail", root.transform, new Vector3(BoardWidth * 0.5f - 0.13f, 0f, FaceOffset), new Vector2(0.26f, boardHeight), purpleRailMaterial);

            var headerY = boardHeight * 0.5f - BoardTopPadding - HeaderHeight * 0.5f;
            CreateCurvedText("Header Text", root.transform, "ARMY COUNT", new Vector3(0f, headerY, TextOffset), HeaderTextSize, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(0.92f, 0.84f, 1f, 1f));

            var rowStartY = headerY - HeaderHeight * 0.5f - HeaderRowGap - RowHeight * 0.5f;
            for (var army = 0; army < builtArmyCount; army++)
            {
                CreateArmyRow(root.transform, army, rowStartY - army * RowStride);
            }
        }

        private void CreateArmyRow(Transform parent, int army, float rowY)
        {
            var armyMaterial = GetArmyMaterial(army);
            CreateCurvedQuad("Row Back", parent, new Vector3(0f, rowY, FaceOffset), new Vector2(BoardWidth - 1.1f, RowHeight), rowBackMaterial);
            CreateCurvedQuad("Army Accent Rail", parent, new Vector3(-BoardWidth * 0.5f + 0.54f, rowY, FaceOffset * 2f), new Vector2(0.32f, RowHeight), armyMaterial);
            CreateCurvedQuad("Fill Back", parent, new Vector3(RowFillCenterX, rowY, FaceOffset * 2f), new Vector2(RowFillWidth, RowFillHeight), fillBackMaterial);
            var fill = CreateCurvedQuad("Fill", parent, new Vector3(RowFillCenterX, rowY, FaceOffset * 3f), new Vector2(RowFillWidth, RowFillHeight), armyMaterial);

            CreateCurvedText("Army Label", parent, $"ARMY {army}", new Vector3(RowLabelX, rowY, TextOffset), RowLabelTextSize, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(0.86f, 0.94f, 1f, 1f));
            var count = CreateCurvedText("Army Count", parent, "000", new Vector3(RowCountX, rowY, TextOffset), RowCountTextSize, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(1f, 0.92f, 0.36f, 1f));

            rows.Add(new ArmyRow
            {
                Army = army,
                FillMesh = fill.GetComponent<MeshFilter>().sharedMesh,
                FillLeftX = RowFillCenterX - RowFillWidth * 0.5f,
                FillY = rowY,
                Count = count,
                LastRemaining = -1,
                LastStarting = -1
            });
        }

        private GameObject CreateBoardRoot(string objectName, float angle)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            root.transform.position = center + direction * radius + Vector3.up * boardY;
            var lookTarget = new Vector3(center.x, lookY, center.z);
            root.transform.rotation = Quaternion.LookRotation(root.transform.position - lookTarget, Vector3.up);
            return root;
        }

        private GameObject CreateCurvedQuad(string objectName, Transform parent, Vector3 localPosition, Vector2 size, Material material)
        {
            var quad = new GameObject(objectName);
            quad.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{objectName} Curved Mesh" };
            WriteCurvedQuadMesh(mesh, localPosition, size);
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = quad.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);
            return quad;
        }

        private TextMesh CreateCurvedText(string objectName, Transform parent, string value, Vector3 localPosition, float characterSize, TextAnchor anchor, TextAlignment alignment, Color color)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = CurveLocalPoint(localPosition);
            textObject.transform.localRotation = CurveLocalRotation(localPosition.x);
            var text = textObject.AddComponent<TextMesh>();
            text.text = value;
            text.font = font;
            text.fontSize = 84;
            text.characterSize = characterSize;
            text.anchor = anchor;
            text.alignment = alignment;
            text.color = color;

            var renderer = textObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            return text;
        }

        private void RefreshRows(bool force)
        {
            if (match == null)
            {
                return;
            }

            foreach (var row in rows)
            {
                var remaining = match.GetAllOutWarArmyRemaining(row.Army);
                var starting = Mathf.Max(1, match.GetAllOutWarArmyStartingCount(row.Army));
                if (!force && remaining == row.LastRemaining && starting == row.LastStarting)
                {
                    continue;
                }

                row.LastRemaining = remaining;
                row.LastStarting = starting;
                var ratio = Mathf.Clamp01(remaining / (float)starting);
                if (row.FillMesh != null)
                {
                    var width = Mathf.Max(0.001f, RowFillWidth * ratio);
                    WriteCurvedQuadMesh(row.FillMesh, new Vector3(row.FillLeftX + width * 0.5f, row.FillY, FaceOffset * 3f), new Vector2(width, RowFillHeight));
                }

                if (row.Count != null)
                {
                    row.Count.text = remaining.ToString("000");
                }
            }
        }

        private Material GetArmyMaterial(int army)
        {
            if (armyMaterials.TryGetValue(army, out var material))
            {
                return material;
            }

            var accent = AllOutWarArmyVisuals.GetAccent(army);
            var baseColor = new Color(accent.r * 0.74f, accent.g * 0.74f, accent.b * 0.74f, 1f);
            var emission = new Color(accent.r * 2.1f, accent.g * 2.1f, accent.b * 2.1f, 1f);
            material = CreateUnlitMaterial($"Dome Scoreboard Army {army} Neon", baseColor, emission);
            armyMaterials[army] = material;
            return material;
        }

        private void WriteCurvedQuadMesh(Mesh mesh, Vector3 localPosition, Vector2 size)
        {
            var segments = Mathf.Clamp(Mathf.CeilToInt(size.x / 3.25f), 1, BoardCurveSegments);
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            var x0 = localPosition.x - size.x * 0.5f;
            var x1 = localPosition.x + size.x * 0.5f;
            var y0 = localPosition.y - size.y * 0.5f;
            var y1 = localPosition.y + size.y * 0.5f;

            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var x = Mathf.Lerp(x0, x1, t);
                vertices[i * 2] = CurveLocalPoint(new Vector3(x, y0, localPosition.z));
                vertices[i * 2 + 1] = CurveLocalPoint(new Vector3(x, y1, localPosition.z));
            }

            for (var i = 0; i < segments; i++)
            {
                var vertex = i * 2;
                var triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private Vector3 CurveLocalPoint(Vector3 localPosition)
        {
            var curveRadius = Mathf.Max(12f, radius);
            var angle = localPosition.x / curveRadius;
            return new Vector3(
                Mathf.Sin(angle) * curveRadius,
                localPosition.y,
                (Mathf.Cos(angle) - 1f) * curveRadius + localPosition.z);
        }

        private Quaternion CurveLocalRotation(float localX)
        {
            var curveRadius = Mathf.Max(12f, radius);
            return Quaternion.Euler(0f, localX / curveRadius * Mathf.Rad2Deg, 0f);
        }

        private static Material CreateUnlitMaterial(string name, Color baseColor, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = name };
            SetColor(material, baseColor);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission);
                }
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }

            return material;
        }

        private static void SetColor(Material material, Color color)
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

        private static void ConfigureRenderer(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private sealed class ArmyRow
        {
            public int Army;
            public Mesh FillMesh;
            public float FillLeftX;
            public float FillY;
            public TextMesh Count;
            public int LastRemaining;
            public int LastStarting;
        }
    }
}
