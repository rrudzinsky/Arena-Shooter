using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaShooter
{
    public sealed class AllOutWarDomeScoreboard : MonoBehaviour
    {
        private const int BankCount = 4;
        private const int MaxDisplayedArmies = 8;
        private const int SpectrumSampleCount = 64;
        private const int TextRenderQueue = 3000;
        private const float ScoreRefreshInterval = 0.25f;
        private const float EqualizerRefreshInterval = 0.075f;
        private const float BoardLatitude = 0.3f;
        private const float DomeVerticalUnitsPerLatitude = 226f;
        private const float MeshSegmentLength = 3.2f;
        private const float ScoreboardMeshSegmentLength = 1.6f;
        private const float ScoreboardMonitorMountInset = -92f;
        private const float ScoreboardMonitorDownTiltDegrees = 8f;
        private const float DetailRadiusInset = -1.6f;
        private const float ScoreboardBackInset = -8.0f;
        private const float ScoreboardPanelInset = -8.5f;
        private const float ScoreboardOuterBorderInset = -9.0f;
        private const float ScoreboardInnerBorderInset = -9.25f;
        private const float ScoreboardRowInset = -9.5f;
        private const float ScoreboardDetailInset = -9.85f;
        private const float ScoreboardFillInset = -10.15f;
        private const float ScoreboardTextInset = -10.65f;
        private const float ScoreboardDomeClearance = 2f;
        private const float BoardOuterBorderThickness = 1.25f;
        private const float BoardInnerBorderThickness = 0.7f;
        private const float BoardBorderGap = 0.75f;

        private const float ScoreboardLocalScale = 0.8f;
        private const float BoardWidth = 146f;
        private const float RowContentWidth = 138f;
        private const float BoardCornerRadius = 7.0f;
        private const float BoardTopPadding = 3.2f;
        private const float BoardBottomPadding = 5.8f;
        private const float HeaderHeight = 8.2f;
        private const float HeaderRowGap = 2.8f;
        private const float RowHeight = 11f;
        private const float RowStride = 13.2f;
        private const float RowFillWidth = 62f;
        private const float RowFillBackWidth = 65f;
        private const float RowFillHeight = 2.85f;
        private const float RowFillBackHeight = 3.7f;
        private const float RowLabelX = 52f;
        private const float RowCountX = -55.5f;
        private const float RowFillCenterX = -3.5f;
        private const float HeaderTextSize = 5.8f;
        private const float RowLabelTextSize = 4.2f;
        private const float RowCountTextSize = 5.2f;

        private const int TextFontSize = 128;
        private const int EqualizerColumnCount = 120;
        private const int EqualizerMaxTiles = 22;
        private const float EqualizerCenterLatitude = 0.22f;
        private const float EqualizerTileWidth = 8.2f;
        private const float EqualizerTileHeight = 4.8f;
        private const float EqualizerTileGap = 2.0f;
        private const float EqualizerBaseY = -20f;
        private const float EqualizerLineThickness = 0.58f;
        private const float EqualizerBlueBandEnd = 0.24f;

        private readonly List<ArmyRow> rows = new();
        private readonly List<EqualizerColumn> equalizerColumns = new();
        private readonly Dictionary<int, Material> armyMaterials = new();
        private readonly float[] spectrumSamples = new float[SpectrumSampleCount];

        private MatchController match;
        private Font font;
        private StadiumVisualMetrics metrics;
        private float nextScoreRefreshAt;
        private float nextEqualizerRefreshAt;
        private int builtArmyCount = -1;
        private Material panelMaterial;
        private Material rowBackMaterial;
        private Material capsuleMaterial;
        private Material fillBackMaterial;
        private Material hotPurpleRailMaterial;
        private Material fontMaterial;

        public void Build(MatchController owner, ArenaLayout layout, float wallHeight)
        {
            match = owner;
            if (match == null || layout == null)
            {
                enabled = false;
                return;
            }

            font = Font.CreateDynamicFontFromOSFont(new[] { "Cascadia Mono", "Consolas", "Lucida Console", "Arial" }, 48);
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            font.RequestCharactersInTexture("ARMY COUNT0123456789", TextFontSize, FontStyle.Bold);
            metrics = AllOutWarStadiumVisuals.CreateMetrics(layout.CircularCenter);
            panelMaterial = CreateUnlitMaterial("Dome Scoreboard Embedded Black Glass", new Color(0.0012f, 0.001f, 0.0058f, 1f), new Color(0.008f, 0.0015f, 0.024f, 1f));
            rowBackMaterial = CreateUnlitMaterial("Dome Scoreboard Row Black Glass", new Color(0.0024f, 0.002f, 0.010f, 1f), new Color(0.010f, 0.0015f, 0.022f, 1f));
            capsuleMaterial = CreateUnlitMaterial("Dome Scoreboard Label Capsule Glass", new Color(0.003f, 0.0025f, 0.013f, 1f), new Color(0.014f, 0.002f, 0.032f, 1f));
            fillBackMaterial = CreateUnlitMaterial("Dome Scoreboard Fill Back Glass", new Color(0.008f, 0.002f, 0.018f, 1f), new Color(0.012f, 0.0015f, 0.024f, 1f));
            hotPurpleRailMaterial = CreateUnlitMaterial("Dome Scoreboard Hot Purple Rail", new Color(1.06f, 0.04f, 2.35f, 1f), new Color(2.75f, 0.08f, 6.0f, 1f));
            fontMaterial = CreateFontMaterial();

            RebuildVisuals();
            RefreshRows(true);
        }

        private void Update()
        {
            if (match == null || !match.IsAllOutWarMode)
            {
                return;
            }

            if (Time.time >= nextScoreRefreshAt)
            {
                nextScoreRefreshAt = Time.time + ScoreRefreshInterval;
                var armyCount = Mathf.Clamp(match.AllOutWarArmyCount, 0, MaxDisplayedArmies);
                if (armyCount != builtArmyCount)
                {
                    RebuildVisuals();
                }

                RefreshRows(false);
            }

            UpdateEqualizer();
        }

        private void RebuildVisuals()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            rows.Clear();
            equalizerColumns.Clear();
            builtArmyCount = match != null ? Mathf.Clamp(match.AllOutWarArmyCount, 0, MaxDisplayedArmies) : 0;
            if (builtArmyCount <= 0)
            {
                return;
            }

            CreateScoreboards();
            CreateEqualizer();
        }

        private void CreateScoreboards()
        {
            var rowBlockHeight = RowHeight + Mathf.Max(0, builtArmyCount - 1) * RowStride;
            var boardHeight = BoardTopPadding + HeaderHeight + HeaderRowGap + rowBlockHeight + BoardBottomPadding;

            for (var bank = 0; bank < BankCount; bank++)
            {
                var angle = bank / (float)BankCount * Mathf.PI * 2f;
                CreateScoreboard($"Dome Scoreboard {bank + 1}", new DomePatch(angle, BoardLatitude, 0f), BoardWidth, boardHeight);
            }
        }

        private void CreateScoreboard(string objectName, DomePatch patch, float boardWidth, float boardHeight)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(transform, false);

            var boardSize = new Vector2(boardWidth, boardHeight);
            CreateReferenceScoreboardFrame(root.transform, patch, boardSize);

            var headerY = boardHeight * 0.5f - BoardTopPadding - HeaderHeight * 0.5f;
            CreateCurvedTextLabel("Header Text", root.transform, patch, "ARMY COUNT", new Vector2(0f, headerY), HeaderTextSize, TextAnchor.MiddleCenter, new Color(1f, 0.98f, 1f, 1f), ScoreboardTextInset);

            var rowStartY = headerY - HeaderHeight * 0.5f - HeaderRowGap - RowHeight * 0.5f;
            for (var army = 0; army < builtArmyCount; army++)
            {
                CreateArmyRow(root.transform, patch, army, rowStartY - army * RowStride);
            }
        }

        private void CreateReferenceScoreboardFrame(Transform parent, DomePatch patch, Vector2 boardSize)
        {
            CreateCurvedRoundedPanel("Board Shadow Glass Bed", parent, patch, Vector2.zero, boardSize + new Vector2(7f, 4.2f), BoardCornerRadius + 2.2f, ScoreboardBackInset, panelMaterial);
            CreateCurvedRoundedPanel("Board Black Glass", parent, patch, Vector2.zero, boardSize - new Vector2(3.2f, 3f), Mathf.Max(1.2f, BoardCornerRadius - 1.2f), ScoreboardPanelInset, panelMaterial);

            var outerBorderSize = boardSize + new Vector2(4.4f, 3.2f);
            var innerBorderSize = outerBorderSize - Vector2.one * ((BoardOuterBorderThickness + BoardBorderGap) * 2f);
            CreateCurvedRoundedBorder("Outer Neon Purple Border", parent, patch, Vector2.zero, outerBorderSize, BoardCornerRadius + 1.4f, BoardOuterBorderThickness, ScoreboardOuterBorderInset, hotPurpleRailMaterial);
            CreateCurvedRoundedBorder("Inner Neon Purple Border", parent, patch, Vector2.zero, innerBorderSize, BoardCornerRadius - 0.2f, BoardInnerBorderThickness, ScoreboardInnerBorderInset, hotPurpleRailMaterial);
        }

        private void CreateArmyRow(Transform parent, DomePatch patch, int army, float rowY)
        {
            var armyMaterial = GetArmyMaterial(army);
            var rowWidth = RowContentWidth;
            var rowSize = new Vector2(rowWidth, RowHeight);
            CreateCurvedClippedPanel("Row Black Glass", parent, patch, new Vector2(0f, rowY), rowSize, 2.8f, ScoreboardRowInset, rowBackMaterial);

            CreateCurvedClippedPanel("Army Label Capsule", parent, patch, new Vector2(RowLabelX, rowY), new Vector2(34f, 7.7f), 2.4f, ScoreboardDetailInset, capsuleMaterial);

            var fillStartX = RowFillCenterX + RowFillWidth * 0.5f;
            CreateCurvedClippedPanel("Fill Back", parent, patch, new Vector2(RowFillCenterX, rowY), new Vector2(RowFillBackWidth, RowFillBackHeight), 1.25f, ScoreboardDetailInset, fillBackMaterial);
            var fill = CreateCurvedSolidRect("Fill", parent, patch, new Vector2(RowFillCenterX, rowY), new Vector2(RowFillWidth, RowFillHeight), ScoreboardFillInset, armyMaterial);

            CreateCurvedClippedPanel("Army Count Capsule", parent, patch, new Vector2(RowCountX, rowY), new Vector2(25f, 8.2f), 2.5f, ScoreboardDetailInset, capsuleMaterial);

            var accent = AllOutWarArmyVisuals.GetAccent(army);
            CreateCurvedTextLabel("Army Label Prefix", parent, patch, "ARMY", new Vector2(RowLabelX + 4.9f, rowY), RowLabelTextSize, TextAnchor.MiddleCenter, new Color(1f, 0.96f, 1f, 1f), ScoreboardTextInset);
            CreateCurvedTextLabel("Army Label Number", parent, patch, army.ToString(), new Vector2(RowLabelX - 10.2f, rowY), RowLabelTextSize * 1.08f, TextAnchor.MiddleCenter, new Color(accent.r * 1.45f, accent.g * 1.45f, accent.b * 1.45f, 1f), ScoreboardTextInset);
            var count = CreateCurvedTextLabel("Army Count", parent, patch, "000", new Vector2(RowCountX, rowY), RowCountTextSize, TextAnchor.MiddleCenter, new Color(1f, 0.98f, 0.96f, 1f), ScoreboardTextInset);

            rows.Add(new ArmyRow
            {
                Army = army,
                Patch = patch,
                FillMesh = fill.GetComponent<MeshFilter>().sharedMesh,
                FillStartX = fillStartX,
                FillDirection = -1f,
                FillY = rowY,
                FillWidth = RowFillWidth,
                Count = count,
                LastRemaining = -1,
                LastStarting = -1
            });
        }

        private void CreateEqualizer()
        {
            var root = new GameObject("Embedded Dome Equalizer Bars");
            root.transform.SetParent(transform, false);

            for (var columnIndex = 0; columnIndex < EqualizerColumnCount; columnIndex++)
            {
                var colorT = EqualizerColumnCount > 1 ? columnIndex / (float)(EqualizerColumnCount - 1) : 0f;
                var behaviorT = GetEqualizerBehaviorT(colorT);
                var phi = columnIndex / (float)EqualizerColumnCount * Mathf.PI * 2f;
                var patch = new DomePatch(phi, EqualizerCenterLatitude, 0f);
                var skylineTiles = GetEqualizerSkylineTiles(behaviorT, columnIndex);

                var color = GetEqualizerColor(colorT);
                var material = CreateUnlitMaterial($"Dome Equalizer Column {columnIndex + 1}", color * 0.72f, color * 2.15f);

                var columnObject = new GameObject($"Equalizer Column {columnIndex + 1:000}");
                columnObject.transform.SetParent(root.transform, false);
                var mesh = new Mesh { name = $"Equalizer Column {columnIndex + 1:000} Mesh" };
                columnObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = columnObject.AddComponent<MeshRenderer>();
                ConfigureRenderer(renderer, material);

                var column = new EqualizerColumn
                {
                    Patch = patch,
                    Mesh = mesh,
                    Material = material,
                    BaseColor = color,
                    LocalX = 0f,
                    SkylineTiles = skylineTiles,
                    BehaviorT = behaviorT,
                    Phase = columnIndex * 0.371f + 4.7f,
                    CurrentLevel = 0f,
                    LastActiveTiles = -1
                };

                equalizerColumns.Add(column);
                WriteEqualizerColumnMesh(column, 0);
            }
        }

        private void UpdateEqualizer()
        {
            var now = Time.unscaledTime;
            if (now < nextEqualizerRefreshAt)
            {
                return;
            }

            nextEqualizerRefreshAt = now + EqualizerRefreshInterval;
            var audio = ArenaAudio.Instance;
            var musicPlaying = audio != null && audio.IsGameplayMusicPlaying;
            var hasSpectrum = musicPlaying && audio.TryGetGameplayMusicSpectrum(spectrumSamples);
            var hasStrongSpectrum = false;
            if (hasSpectrum)
            {
                for (var i = 0; i < spectrumSamples.Length; i++)
                {
                    if (spectrumSamples[i] > 0.00045f)
                    {
                        hasStrongSpectrum = true;
                        break;
                    }
                }
            }

            for (var i = 0; i < equalizerColumns.Count; i++)
            {
                var column = equalizerColumns[i];
                var behaviorT = column.BehaviorT;
                var spectrumLevel = 0f;
                if (hasSpectrum)
                {
                    var bin = Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(behaviorT, 1.45f) * (spectrumSamples.Length - 1)), 0, spectrumSamples.Length - 1);
                    var neighbor = Mathf.Min(spectrumSamples.Length - 1, bin + 1);
                    spectrumLevel = Mathf.Clamp01((spectrumSamples[bin] + spectrumSamples[neighbor] * 0.65f) * 95f);
                    spectrumLevel = Mathf.Pow(spectrumLevel, 0.58f);
                }

                var slowNoise = Mathf.PerlinNoise(column.Phase, now * 2.2f);
                var snapNoise = Mathf.PerlinNoise(column.Phase * 2.7f, Mathf.Floor(now * 8.5f) * 0.19f);
                var fallbackMotion = Mathf.Clamp01(slowNoise * 0.72f + snapNoise * 0.34f);
                var target = 0f;
                if (musicPlaying)
                {
                    target = hasStrongSpectrum
                        ? Mathf.Clamp01(spectrumLevel * 0.82f + fallbackMotion * 0.38f)
                        : fallbackMotion;
                }

                var attack = target > column.CurrentLevel ? 0.72f : 0.24f;
                column.CurrentLevel = Mathf.Lerp(column.CurrentLevel, target, attack);
                var skylineMotion = Mathf.Lerp(0.32f, 1f, column.CurrentLevel);
                var activeTiles = musicPlaying && column.SkylineTiles > 0
                    ? Mathf.Clamp(Mathf.CeilToInt(skylineMotion * column.SkylineTiles), 1, column.SkylineTiles)
                    : 0;
                if (activeTiles != column.LastActiveTiles)
                {
                    WriteEqualizerColumnMesh(column, activeTiles);
                }

                var glow = musicPlaying ? Mathf.Lerp(1.15f, 3.6f, column.CurrentLevel) : 0.55f;
                SetMaterialColor(column.Material, column.BaseColor * Mathf.Lerp(0.48f, 1.05f, column.CurrentLevel));
                SetMaterialEmission(column.Material, column.BaseColor * glow);
            }
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
                    var width = row.FillWidth * ratio;
                    if (width <= 0.001f)
                    {
                        row.FillMesh.Clear();
                    }
                    else
                    {
                        var center = new Vector2(row.FillStartX + row.FillDirection * width * 0.5f, row.FillY);
                        WriteCurvedSolidRectMesh(row.FillMesh, row.Patch, center, new Vector2(width, RowFillHeight), ScoreboardFillInset);
                    }
                }

                if (row.Count != null)
                {
                    row.Count.SetText(this, remaining.ToString("000"));
                }
            }
        }

        private GameObject CreateCurvedClippedPanel(string objectName, Transform parent, DomePatch patch, Vector2 localCenter, Vector2 size, float cornerCut, float radiusInset, Material material)
        {
            var panel = new GameObject(objectName);
            panel.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{objectName} Dome Mesh" };
            WriteCurvedClippedPanelMesh(mesh, patch, localCenter, size, cornerCut, radiusInset);
            panel.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = panel.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);
            return panel;
        }

        private GameObject CreateCurvedClippedBorder(string objectName, Transform parent, DomePatch patch, Vector2 localCenter, Vector2 size, float cornerCut, float thickness, float radiusInset, Material material)
        {
            var panel = new GameObject(objectName);
            panel.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{objectName} Dome Mesh" };
            WriteCurvedClippedBorderMesh(mesh, patch, localCenter, size, cornerCut, thickness, radiusInset);
            panel.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = panel.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);
            return panel;
        }

        private GameObject CreateCurvedRoundedPanel(string objectName, Transform parent, DomePatch patch, Vector2 localCenter, Vector2 size, float cornerRadius, float radiusInset, Material material)
        {
            var panel = new GameObject(objectName);
            panel.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{objectName} Dome Mesh" };
            WriteCurvedRoundedPanelMesh(mesh, patch, localCenter, size, cornerRadius, radiusInset);
            panel.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = panel.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);
            return panel;
        }

        private GameObject CreateCurvedRoundedBorder(string objectName, Transform parent, DomePatch patch, Vector2 localCenter, Vector2 size, float cornerRadius, float thickness, float radiusInset, Material material)
        {
            var panel = new GameObject(objectName);
            panel.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{objectName} Dome Mesh" };
            WriteCurvedRoundedBorderMesh(mesh, patch, localCenter, size, cornerRadius, thickness, radiusInset);
            panel.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = panel.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);
            return panel;
        }

        private GameObject CreateCurvedSolidRect(string objectName, Transform parent, DomePatch patch, Vector2 localCenter, Vector2 size, float radiusInset, Material material)
        {
            var panel = new GameObject(objectName);
            panel.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{objectName} Dome Mesh" };
            WriteCurvedSolidRectMesh(mesh, patch, localCenter, size, radiusInset);
            panel.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = panel.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);
            return panel;
        }

        private CurvedTextLabel CreateCurvedTextLabel(string objectName, Transform parent, DomePatch patch, string value, Vector2 localPosition, float textHeight, TextAnchor anchor, Color color, float radiusInset)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            var mesh = new Mesh { name = $"{objectName} Dome Text Mesh" };
            textObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = textObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, fontMaterial);

            var label = new CurvedTextLabel(mesh, patch, localPosition, textHeight, anchor, color, radiusInset);
            label.SetText(this, value);
            return label;
        }

        private void WriteCurvedTextMesh(Mesh mesh, DomePatch patch, string value, Vector2 localPosition, float textHeight, TextAnchor anchor, Color color, float radiusInset)
        {
            mesh.Clear();
            if (font == null || string.IsNullOrEmpty(value))
            {
                return;
            }

            font.RequestCharactersInTexture(value, TextFontSize, FontStyle.Bold);
            var glyphs = new List<GlyphQuad>(value.Length);
            var cursorX = 0f;
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character == ' ')
                {
                    cursorX += TextFontSize * 0.34f;
                    continue;
                }

                if (!TryGetCharacterInfo(character, out var info))
                {
                    cursorX += TextFontSize * 0.34f;
                    continue;
                }

                var glyph = new GlyphQuad(
                    cursorX + info.minX,
                    cursorX + info.maxX,
                    info.minY,
                    info.maxY,
                    info.uvBottomLeft,
                    info.uvTopLeft,
                    info.uvTopRight,
                    info.uvBottomRight);
                glyphs.Add(glyph);
                minY = Mathf.Min(minY, glyph.MinY);
                maxY = Mathf.Max(maxY, glyph.MaxY);
                cursorX += info.advance > 0 ? info.advance : info.maxX - info.minX + TextFontSize * 0.08f;
            }

            if (glyphs.Count == 0)
            {
                return;
            }

            var scale = textHeight / TextFontSize;
            var offsetX = GetTextAnchorOffsetX(anchor, cursorX);
            var offsetY = GetTextAnchorOffsetY(anchor, minY, maxY);
            var vertices = new List<Vector3>(glyphs.Count * 4);
            var uvs = new List<Vector2>(glyphs.Count * 4);
            var colors = new List<Color>(glyphs.Count * 4);
            var triangles = new List<int>(glyphs.Count * 6);
            for (var i = 0; i < glyphs.Count; i++)
            {
                var glyph = glyphs[i];
                var baseIndex = vertices.Count;
                AddTextVertex(vertices, uvs, colors, patch, localPosition, GetInteriorTextOffset(glyph.MinX + offsetX, glyph.MinY + offsetY, scale), glyph.UvBottomLeft, color, radiusInset);
                AddTextVertex(vertices, uvs, colors, patch, localPosition, GetInteriorTextOffset(glyph.MinX + offsetX, glyph.MaxY + offsetY, scale), glyph.UvTopLeft, color, radiusInset);
                AddTextVertex(vertices, uvs, colors, patch, localPosition, GetInteriorTextOffset(glyph.MaxX + offsetX, glyph.MaxY + offsetY, scale), glyph.UvTopRight, color, radiusInset);
                AddTextVertex(vertices, uvs, colors, patch, localPosition, GetInteriorTextOffset(glyph.MaxX + offsetX, glyph.MinY + offsetY, scale), glyph.UvBottomRight, color, radiusInset);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static Vector2 GetInteriorTextOffset(float x, float y, float scale)
        {
            return new Vector2(-x * scale, y * scale);
        }

        private void AddTextVertex(List<Vector3> vertices, List<Vector2> uvs, List<Color> colors, DomePatch patch, Vector2 localPosition, Vector2 glyphOffset, Vector2 uv, Color color, float radiusInset)
        {
            vertices.Add(ScoreboardPoint(patch, localPosition + glyphOffset, radiusInset));
            uvs.Add(uv);
            colors.Add(color);
        }

        private bool TryGetCharacterInfo(char character, out CharacterInfo info)
        {
            if (font.GetCharacterInfo(character, out info, TextFontSize, FontStyle.Bold))
            {
                return true;
            }

            font.RequestCharactersInTexture(character.ToString(), TextFontSize, FontStyle.Bold);
            if (font.GetCharacterInfo(character, out info, TextFontSize, FontStyle.Bold))
            {
                return true;
            }

            font.RequestCharactersInTexture(character.ToString(), TextFontSize, FontStyle.Normal);
            return font.GetCharacterInfo(character, out info, TextFontSize, FontStyle.Normal);
        }

        private static float GetTextAnchorOffsetX(TextAnchor anchor, float width)
        {
            return anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.LowerCenter
                ? -width * 0.5f
                : anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight
                    ? -width
                    : 0f;
        }

        private static float GetTextAnchorOffsetY(TextAnchor anchor, float minY, float maxY)
        {
            return anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.MiddleRight
                ? -(minY + maxY) * 0.5f
                : anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight
                    ? -minY
                    : -maxY;
        }

        private void WriteCurvedClippedPanelMesh(Mesh mesh, DomePatch patch, Vector2 localCenter, Vector2 size, float cornerCut, float radiusInset)
        {
            var xSegments = Mathf.Clamp(Mathf.CeilToInt(size.x / ScoreboardMeshSegmentLength), 1, 96);
            var ySegments = Mathf.Clamp(Mathf.CeilToInt(size.y / ScoreboardMeshSegmentLength), 1, 48);
            var vertices = new Vector3[(xSegments + 1) * (ySegments + 1)];
            var triangles = new int[xSegments * ySegments * 6];
            var halfWidth = size.x * 0.5f;
            var halfHeight = size.y * 0.5f;

            for (var yIndex = 0; yIndex <= ySegments; yIndex++)
            {
                var yT = yIndex / (float)ySegments;
                var localY = Mathf.Lerp(-halfHeight, halfHeight, yT);
                var xShrink = GetCornerShrink(localY, halfHeight, cornerCut);
                var xMin = -halfWidth + xShrink;
                var xMax = halfWidth - xShrink;

                for (var xIndex = 0; xIndex <= xSegments; xIndex++)
                {
                    var xT = xIndex / (float)xSegments;
                    var localX = Mathf.Lerp(xMin, xMax, xT);
                    vertices[yIndex * (xSegments + 1) + xIndex] = ScoreboardPoint(patch, localCenter + new Vector2(localX, localY), radiusInset);
                }
            }

            var triangleIndex = 0;
            for (var yIndex = 0; yIndex < ySegments; yIndex++)
            {
                for (var xIndex = 0; xIndex < xSegments; xIndex++)
                {
                    var a = yIndex * (xSegments + 1) + xIndex;
                    var b = a + xSegments + 1;
                    var c = a + 1;
                    var d = b + 1;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = d;
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void WriteCurvedRoundedPanelMesh(Mesh mesh, DomePatch patch, Vector2 localCenter, Vector2 size, float cornerRadius, float radiusInset)
        {
            var xSegments = Mathf.Clamp(Mathf.CeilToInt(size.x / ScoreboardMeshSegmentLength), 1, 96);
            var ySegments = Mathf.Clamp(Mathf.CeilToInt(size.y / (ScoreboardMeshSegmentLength * 0.7f)), 2, 96);
            var vertices = new Vector3[(xSegments + 1) * (ySegments + 1)];
            var triangles = new int[xSegments * ySegments * 6];
            var halfWidth = size.x * 0.5f;
            var halfHeight = size.y * 0.5f;
            var radius = ClampCornerRadius(size, cornerRadius);

            for (var yIndex = 0; yIndex <= ySegments; yIndex++)
            {
                var yT = yIndex / (float)ySegments;
                var localY = Mathf.Lerp(-halfHeight, halfHeight, yT);
                var xShrink = GetRoundedCornerShrink(localY, halfHeight, radius);
                var xMin = -halfWidth + xShrink;
                var xMax = halfWidth - xShrink;

                for (var xIndex = 0; xIndex <= xSegments; xIndex++)
                {
                    var xT = xIndex / (float)xSegments;
                    var localX = Mathf.Lerp(xMin, xMax, xT);
                    vertices[yIndex * (xSegments + 1) + xIndex] = ScoreboardPoint(patch, localCenter + new Vector2(localX, localY), radiusInset);
                }
            }

            var triangleIndex = 0;
            for (var yIndex = 0; yIndex < ySegments; yIndex++)
            {
                for (var xIndex = 0; xIndex < xSegments; xIndex++)
                {
                    var a = yIndex * (xSegments + 1) + xIndex;
                    var b = a + xSegments + 1;
                    var c = a + 1;
                    var d = b + 1;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = d;
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void WriteCurvedClippedBorderMesh(Mesh mesh, DomePatch patch, Vector2 localCenter, Vector2 size, float cornerCut, float thickness, float radiusInset)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddScoreboardClippedBorderGeometry(vertices, triangles, patch, localCenter, size, cornerCut, thickness, radiusInset);
            WriteMesh(mesh, vertices, triangles);
        }

        private void WriteCurvedRoundedBorderMesh(Mesh mesh, DomePatch patch, Vector2 localCenter, Vector2 size, float cornerRadius, float thickness, float radiusInset)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddScoreboardRoundedBorderGeometry(vertices, triangles, patch, localCenter, size, cornerRadius, thickness, radiusInset);
            WriteMesh(mesh, vertices, triangles);
        }

        private void WriteCurvedSolidRectMesh(Mesh mesh, DomePatch patch, Vector2 localCenter, Vector2 size, float radiusInset)
        {
            var halfWidth = size.x * 0.5f;
            var halfHeight = size.y * 0.5f;
            var xSegments = Mathf.Clamp(Mathf.CeilToInt(size.x / ScoreboardMeshSegmentLength), 1, 96);
            var vertices = new Vector3[(xSegments + 1) * 2];
            var triangles = new int[xSegments * 6];

            for (var xIndex = 0; xIndex <= xSegments; xIndex++)
            {
                var xT = xIndex / (float)xSegments;
                var localX = Mathf.Lerp(-halfWidth, halfWidth, xT);
                vertices[xIndex * 2] = ScoreboardPoint(patch, localCenter + new Vector2(localX, -halfHeight), radiusInset);
                vertices[xIndex * 2 + 1] = ScoreboardPoint(patch, localCenter + new Vector2(localX, halfHeight), radiusInset);
            }

            var triangleIndex = 0;
            for (var xIndex = 0; xIndex < xSegments; xIndex++)
            {
                var a = xIndex * 2;
                var b = a + 1;
                var c = a + 2;
                var d = a + 3;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = d;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void WriteEqualizerColumnMesh(EqualizerColumn column, int activeTiles)
        {
            column.LastActiveTiles = activeTiles;
            var vertices = new List<Vector3>(activeTiles * 48);
            var triangles = new List<int>(activeTiles * 72);
            for (var tile = 0; tile < activeTiles; tile++)
            {
                var tileCenter = new Vector2(column.LocalX, EqualizerBaseY + tile * (EqualizerTileHeight + EqualizerTileGap));
                AddDomeHollowRectGeometry(vertices, triangles, column.Patch, tileCenter, new Vector2(EqualizerTileWidth, EqualizerTileHeight), EqualizerLineThickness, DetailRadiusInset - 0.32f);
            }

            WriteMesh(column.Mesh, vertices, triangles);
        }

        private void AddDomeHollowRectGeometry(List<Vector3> vertices, List<int> triangles, DomePatch patch, Vector2 center, Vector2 size, float thickness, float radiusInset)
        {
            AddDomeClippedBorderGeometry(vertices, triangles, patch, center, size, 0f, thickness, radiusInset);
        }

        private void AddScoreboardRoundedBorderGeometry(List<Vector3> vertices, List<int> triangles, DomePatch patch, Vector2 center, Vector2 size, float cornerRadius, float thickness, float radiusInset)
        {
            var borderInset = Mathf.Min(thickness, Mathf.Min(size.x, size.y) * 0.45f);
            if (borderInset <= 0.001f)
            {
                return;
            }

            var innerSize = new Vector2(size.x - borderInset * 2f, size.y - borderInset * 2f);
            if (innerSize.x <= 0.001f || innerSize.y <= 0.001f)
            {
                return;
            }

            var outerRadius = ClampCornerRadius(size, cornerRadius);
            var innerRadius = ClampCornerRadius(innerSize, Mathf.Max(0.001f, cornerRadius - borderInset));
            var outerHalfWidth = size.x * 0.5f;
            var outerHalfHeight = size.y * 0.5f;
            var innerHalfWidth = innerSize.x * 0.5f;
            var innerHalfHeight = innerSize.y * 0.5f;
            var topBottomLength = Mathf.Max(size.x - outerRadius * 2f, innerSize.x - innerRadius * 2f);
            var sideLength = Mathf.Max(size.y - outerRadius * 2f, innerSize.y - innerRadius * 2f);
            var horizontalSegments = Mathf.Clamp(Mathf.CeilToInt(topBottomLength / ScoreboardMeshSegmentLength), 1, 96);
            var verticalSegments = Mathf.Clamp(Mathf.CeilToInt(sideLength / ScoreboardMeshSegmentLength), 1, 64);
            var arcSegments = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * outerRadius * 0.5f / (ScoreboardMeshSegmentLength * 0.7f)), 4, 24);
            var baseIndex = vertices.Count;

            AddRoundedLinePair(vertices, patch, center,
                new Vector2(-outerHalfWidth + outerRadius, outerHalfHeight),
                new Vector2(outerHalfWidth - outerRadius, outerHalfHeight),
                new Vector2(-innerHalfWidth + innerRadius, innerHalfHeight),
                new Vector2(innerHalfWidth - innerRadius, innerHalfHeight),
                horizontalSegments,
                radiusInset);
            AddRoundedArcPair(vertices, patch, center,
                new Vector2(outerHalfWidth - outerRadius, outerHalfHeight - outerRadius),
                new Vector2(innerHalfWidth - innerRadius, innerHalfHeight - innerRadius),
                outerRadius,
                innerRadius,
                90f,
                0f,
                arcSegments,
                radiusInset);
            AddRoundedLinePair(vertices, patch, center,
                new Vector2(outerHalfWidth, outerHalfHeight - outerRadius),
                new Vector2(outerHalfWidth, -outerHalfHeight + outerRadius),
                new Vector2(innerHalfWidth, innerHalfHeight - innerRadius),
                new Vector2(innerHalfWidth, -innerHalfHeight + innerRadius),
                verticalSegments,
                radiusInset);
            AddRoundedArcPair(vertices, patch, center,
                new Vector2(outerHalfWidth - outerRadius, -outerHalfHeight + outerRadius),
                new Vector2(innerHalfWidth - innerRadius, -innerHalfHeight + innerRadius),
                outerRadius,
                innerRadius,
                0f,
                -90f,
                arcSegments,
                radiusInset);
            AddRoundedLinePair(vertices, patch, center,
                new Vector2(outerHalfWidth - outerRadius, -outerHalfHeight),
                new Vector2(-outerHalfWidth + outerRadius, -outerHalfHeight),
                new Vector2(innerHalfWidth - innerRadius, -innerHalfHeight),
                new Vector2(-innerHalfWidth + innerRadius, -innerHalfHeight),
                horizontalSegments,
                radiusInset);
            AddRoundedArcPair(vertices, patch, center,
                new Vector2(-outerHalfWidth + outerRadius, -outerHalfHeight + outerRadius),
                new Vector2(-innerHalfWidth + innerRadius, -innerHalfHeight + innerRadius),
                outerRadius,
                innerRadius,
                -90f,
                -180f,
                arcSegments,
                radiusInset);
            AddRoundedLinePair(vertices, patch, center,
                new Vector2(-outerHalfWidth, -outerHalfHeight + outerRadius),
                new Vector2(-outerHalfWidth, outerHalfHeight - outerRadius),
                new Vector2(-innerHalfWidth, -innerHalfHeight + innerRadius),
                new Vector2(-innerHalfWidth, innerHalfHeight - innerRadius),
                verticalSegments,
                radiusInset);
            AddRoundedArcPair(vertices, patch, center,
                new Vector2(-outerHalfWidth + outerRadius, outerHalfHeight - outerRadius),
                new Vector2(-innerHalfWidth + innerRadius, innerHalfHeight - innerRadius),
                outerRadius,
                innerRadius,
                180f,
                90f,
                arcSegments,
                radiusInset);

            AddClosedRingTriangles(triangles, baseIndex, (vertices.Count - baseIndex) / 2);
        }

        private void AddRoundedLinePair(List<Vector3> vertices, DomePatch patch, Vector2 center, Vector2 outerStart, Vector2 outerEnd, Vector2 innerStart, Vector2 innerEnd, int segments, float radiusInset)
        {
            for (var i = 0; i < segments; i++)
            {
                var t = i / (float)segments;
                vertices.Add(ScoreboardPoint(patch, center + Vector2.Lerp(outerStart, outerEnd, t), radiusInset));
                vertices.Add(ScoreboardPoint(patch, center + Vector2.Lerp(innerStart, innerEnd, t), radiusInset));
            }
        }

        private void AddRoundedArcPair(List<Vector3> vertices, DomePatch patch, Vector2 center, Vector2 outerCenter, Vector2 innerCenter, float outerRadius, float innerRadius, float startDegrees, float endDegrees, int segments, float radiusInset)
        {
            for (var i = 0; i < segments; i++)
            {
                var t = i / (float)segments;
                var angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.Add(ScoreboardPoint(patch, center + outerCenter + direction * outerRadius, radiusInset));
                vertices.Add(ScoreboardPoint(patch, center + innerCenter + direction * innerRadius, radiusInset));
            }
        }

        private void AddScoreboardClippedBorderGeometry(List<Vector3> vertices, List<int> triangles, DomePatch patch, Vector2 center, Vector2 size, float cornerCut, float thickness, float radiusInset)
        {
            var borderInset = Mathf.Min(thickness, Mathf.Min(size.x, size.y) * 0.45f);
            if (borderInset <= 0.001f)
            {
                return;
            }

            var innerSize = new Vector2(size.x - borderInset * 2f, size.y - borderInset * 2f);
            if (innerSize.x <= 0.001f || innerSize.y <= 0.001f)
            {
                return;
            }

            var clipped = cornerCut > 0.001f;
            var outerCut = ClampCornerCut(size, cornerCut, clipped);
            var innerCut = ClampCornerCut(innerSize, clipped ? Mathf.Max(0.001f, cornerCut - borderInset) : 0f, clipped);
            var outerCorners = GetClippedRectCorners(size, outerCut, clipped);
            var innerCorners = GetClippedRectCorners(innerSize, innerCut, clipped);
            var baseIndex = vertices.Count;

            for (var cornerIndex = 0; cornerIndex < outerCorners.Length; cornerIndex++)
            {
                var nextCornerIndex = (cornerIndex + 1) % outerCorners.Length;
                var outerStart = outerCorners[cornerIndex];
                var outerEnd = outerCorners[nextCornerIndex];
                var innerStart = innerCorners[cornerIndex];
                var innerEnd = innerCorners[nextCornerIndex];
                var edgeLength = Mathf.Max(Vector2.Distance(outerStart, outerEnd), Vector2.Distance(innerStart, innerEnd));
                var segments = Mathf.Clamp(Mathf.CeilToInt(edgeLength / ScoreboardMeshSegmentLength), 1, 64);

                for (var i = 0; i < segments; i++)
                {
                    var t = i / (float)segments;
                    vertices.Add(ScoreboardPoint(patch, center + Vector2.Lerp(outerStart, outerEnd, t), radiusInset));
                    vertices.Add(ScoreboardPoint(patch, center + Vector2.Lerp(innerStart, innerEnd, t), radiusInset));
                }
            }

            AddClosedRingTriangles(triangles, baseIndex, (vertices.Count - baseIndex) / 2);
        }

        private void AddDomeClippedBorderGeometry(List<Vector3> vertices, List<int> triangles, DomePatch patch, Vector2 center, Vector2 size, float cornerCut, float thickness, float radiusInset)
        {
            var borderInset = Mathf.Min(thickness, Mathf.Min(size.x, size.y) * 0.45f);
            if (borderInset <= 0.001f)
            {
                return;
            }

            var innerSize = new Vector2(size.x - borderInset * 2f, size.y - borderInset * 2f);
            if (innerSize.x <= 0.001f || innerSize.y <= 0.001f)
            {
                return;
            }

            var clipped = cornerCut > 0.001f;
            var outerCut = ClampCornerCut(size, cornerCut, clipped);
            var innerCut = ClampCornerCut(innerSize, clipped ? Mathf.Max(0.001f, cornerCut - borderInset) : 0f, clipped);
            var outerCorners = GetClippedRectCorners(size, outerCut, clipped);
            var innerCorners = GetClippedRectCorners(innerSize, innerCut, clipped);
            var baseIndex = vertices.Count;

            for (var cornerIndex = 0; cornerIndex < outerCorners.Length; cornerIndex++)
            {
                var nextCornerIndex = (cornerIndex + 1) % outerCorners.Length;
                var outerStart = outerCorners[cornerIndex];
                var outerEnd = outerCorners[nextCornerIndex];
                var innerStart = innerCorners[cornerIndex];
                var innerEnd = innerCorners[nextCornerIndex];
                var edgeLength = Mathf.Max(Vector2.Distance(outerStart, outerEnd), Vector2.Distance(innerStart, innerEnd));
                var segments = Mathf.Clamp(Mathf.CeilToInt(edgeLength / MeshSegmentLength), 1, 32);

                for (var i = 0; i < segments; i++)
                {
                    var t = i / (float)segments;
                    vertices.Add(DomePoint(patch, center + Vector2.Lerp(outerStart, outerEnd, t), radiusInset));
                    vertices.Add(DomePoint(patch, center + Vector2.Lerp(innerStart, innerEnd, t), radiusInset));
                }
            }

            AddClosedRingTriangles(triangles, baseIndex, (vertices.Count - baseIndex) / 2);
        }

        private Vector3 ScoreboardPoint(DomePatch patch, Vector2 localPosition, float radiusInset)
        {
            var rolled = patch.Roll(ScaleScoreboardLocalPosition(localPosition));
            var centerTheta = AllOutWarStadiumVisuals.LatitudeToTheta(patch.Latitude);
            var bendRadius = Mathf.Max(1f, metrics.Radius * Mathf.Cos(centerTheta));
            var centerY = metrics.BaseY + metrics.Height * Mathf.Sin(centerTheta);
            var verticalScale = GetScoreboardVerticalWorldScale(patch.Latitude);
            var verticalOffset = rolled.y * verticalScale;
            var monitorRadius = Mathf.Max(1f, bendRadius + ScoreboardMonitorMountInset);
            var tiltRadians = ScoreboardMonitorDownTiltDegrees * Mathf.Deg2Rad;
            var surfaceRadius = Mathf.Max(1f, monitorRadius + radiusInset - verticalOffset * Mathf.Sin(tiltRadians));
            var phi = patch.Phi + rolled.x / monitorRadius;
            var point = metrics.Center + new Vector3(
                Mathf.Cos(phi) * surfaceRadius,
                centerY + verticalOffset * Mathf.Cos(tiltRadians),
                Mathf.Sin(phi) * surfaceRadius);
            return ClampScoreboardPointInsideDome(point, radiusInset);
        }

        private static Vector2 ScaleScoreboardLocalPosition(Vector2 localPosition)
        {
            return localPosition * ScoreboardLocalScale;
        }

        private Vector3 DomePoint(DomePatch patch, Vector2 localPosition, float radiusInset)
        {
            var rolled = patch.Roll(localPosition);
            var latitude = Mathf.Clamp(patch.Latitude + rolled.y / DomeVerticalUnitsPerLatitude, 0.015f, 0.92f);
            var theta = AllOutWarStadiumVisuals.LatitudeToTheta(latitude);
            var flatRadius = Mathf.Max(1f, metrics.Radius * Mathf.Cos(theta));
            var phi = patch.Phi + rolled.x / flatRadius;
            return AllOutWarStadiumVisuals.DomePoint(metrics, latitude, phi, radiusInset);
        }

        private float GetScoreboardVerticalWorldScale(float latitude)
        {
            var centerTheta = AllOutWarStadiumVisuals.LatitudeToTheta(latitude);
            var sampleTheta = AllOutWarStadiumVisuals.LatitudeToTheta(Mathf.Clamp01(latitude + 1f / DomeVerticalUnitsPerLatitude));
            return Mathf.Max(0.1f, metrics.Height * (Mathf.Sin(sampleTheta) - Mathf.Sin(centerTheta)));
        }

        private Vector3 ClampScoreboardPointInsideDome(Vector3 point, float radiusInset)
        {
            var local = point - metrics.Center;
            var maxTheta = AllOutWarStadiumVisuals.LatitudeToTheta(1f);
            var maxNormalizedHeight = Mathf.Sin(maxTheta);
            var maxY = metrics.BaseY + metrics.Height * maxNormalizedHeight - ScoreboardDomeClearance;
            if (local.y > maxY)
            {
                local.y = maxY;
            }

            var normalizedHeight = Mathf.Clamp((local.y - metrics.BaseY) / Mathf.Max(0.001f, metrics.Height), 0f, maxNormalizedHeight);
            var theta = Mathf.Asin(normalizedHeight);
            var effectiveInset = Mathf.Min(radiusInset, -ScoreboardDomeClearance);
            var maxRadius = Mathf.Max(1f, metrics.Radius + effectiveInset) * Mathf.Cos(theta);
            var horizontal = new Vector2(local.x, local.z);
            var horizontalMagnitude = horizontal.magnitude;
            if (horizontalMagnitude > maxRadius && horizontalMagnitude > 0.001f)
            {
                var scale = maxRadius / horizontalMagnitude;
                local.x *= scale;
                local.z *= scale;
            }

            return metrics.Center + local;
        }

        private static void AddClosedRingTriangles(List<int> triangles, int baseIndex, int pairCount)
        {
            for (var i = 0; i < pairCount; i++)
            {
                var a = baseIndex + i * 2;
                var b = a + 1;
                var c = baseIndex + ((i + 1) % pairCount) * 2;
                var d = c + 1;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
            }
        }

        private Material GetArmyMaterial(int army)
        {
            if (armyMaterials.TryGetValue(army, out var material))
            {
                return material;
            }

            var accent = AllOutWarArmyVisuals.GetAccent(army);
            var baseColor = new Color(accent.r * 0.9f, accent.g * 0.9f, accent.b * 0.9f, 1f);
            var emission = new Color(accent.r * 2.7f, accent.g * 2.7f, accent.b * 2.7f, 1f);
            material = CreateUnlitMaterial($"Dome Scoreboard Army {army} Neon", baseColor, emission);
            armyMaterials[army] = material;
            return material;
        }

        private static float GetCornerShrink(float localY, float halfHeight, float cornerCut)
        {
            if (cornerCut <= 0.001f)
            {
                return 0f;
            }

            var edgeDistance = halfHeight - Mathf.Abs(localY);
            return Mathf.Clamp(cornerCut - edgeDistance, 0f, cornerCut);
        }

        private static float GetRoundedCornerShrink(float localY, float halfHeight, float cornerRadius)
        {
            if (cornerRadius <= 0.001f)
            {
                return 0f;
            }

            var edgeDistance = halfHeight - Mathf.Abs(localY);
            if (edgeDistance >= cornerRadius)
            {
                return 0f;
            }

            var radiusDelta = cornerRadius - Mathf.Max(0f, edgeDistance);
            return cornerRadius - Mathf.Sqrt(Mathf.Max(0f, cornerRadius * cornerRadius - radiusDelta * radiusDelta));
        }

        private static float ClampCornerRadius(Vector2 size, float cornerRadius)
        {
            return Mathf.Clamp(cornerRadius, 0f, Mathf.Min(size.x, size.y) * 0.5f - 0.001f);
        }

        private static float ClampCornerCut(Vector2 size, float cornerCut, bool forceClipped)
        {
            if (!forceClipped)
            {
                return 0f;
            }

            return Mathf.Clamp(cornerCut, 0.001f, Mathf.Min(size.x, size.y) * 0.5f - 0.001f);
        }

        private static Vector2[] GetClippedRectCorners(Vector2 size, float cornerCut, bool clipped)
        {
            var halfWidth = size.x * 0.5f;
            var halfHeight = size.y * 0.5f;
            if (!clipped)
            {
                return new[]
                {
                    new Vector2(-halfWidth, -halfHeight),
                    new Vector2(halfWidth, -halfHeight),
                    new Vector2(halfWidth, halfHeight),
                    new Vector2(-halfWidth, halfHeight)
                };
            }

            var cut = ClampCornerCut(size, cornerCut, true);
            return new[]
            {
                new Vector2(-halfWidth + cut, -halfHeight),
                new Vector2(halfWidth - cut, -halfHeight),
                new Vector2(halfWidth, -halfHeight + cut),
                new Vector2(halfWidth, halfHeight - cut),
                new Vector2(halfWidth - cut, halfHeight),
                new Vector2(-halfWidth + cut, halfHeight),
                new Vector2(-halfWidth, halfHeight - cut),
                new Vector2(-halfWidth, -halfHeight + cut)
            };
        }

        private static int GetEqualizerSkylineTiles(float t, int columnIndex)
        {
            var cluster = 0f;
            cluster = Mathf.Max(cluster, EqualizerGaussian(t, 0.08f, 0.032f) * 10.4f);
            cluster = Mathf.Max(cluster, EqualizerGaussian(t, 0.25f, 0.045f) * 7.2f);
            cluster = Mathf.Max(cluster, EqualizerGaussian(t, 0.43f, 0.034f) * 10.8f);
            cluster = Mathf.Max(cluster, EqualizerGaussian(t, 0.64f, 0.046f) * 8.2f);
            cluster = Mathf.Max(cluster, EqualizerGaussian(t, 0.88f, 0.04f) * 10.6f);

            var scatteredPeaks = Mathf.PerlinNoise(columnIndex * 0.47f, 12.7f) * 6.1f;
            var sawVariation = columnIndex % 9 == 0 ? 2.6f : 0f;
            var trough = columnIndex % 13 == 0 ? -2.2f : 0f;
            return Mathf.Clamp(Mathf.RoundToInt(5.2f + cluster + scatteredPeaks + sawVariation + trough), 4, EqualizerMaxTiles);
        }

        private static float EqualizerGaussian(float value, float center, float width)
        {
            var normalized = (value - center) / Mathf.Max(0.001f, width);
            return Mathf.Exp(-0.5f * normalized * normalized);
        }

        private static Color GetEqualizerColor(float t)
        {
            if (t < EqualizerBlueBandEnd)
            {
                return Color.Lerp(new Color(0.02f, 0.46f, 1.65f, 1f), new Color(0.02f, 0.9f, 2.4f, 1f), t / EqualizerBlueBandEnd);
            }

            if (t < 0.52f)
            {
                return Color.Lerp(new Color(0.42f, 0.12f, 2.2f, 1f), new Color(1.55f, 0.06f, 2.05f, 1f), (t - 0.24f) / 0.28f);
            }

            if (t < 0.78f)
            {
                return Color.Lerp(new Color(1.9f, 0.03f, 0.26f, 1f), new Color(2.0f, 0.42f, 0.04f, 1f), (t - 0.52f) / 0.26f);
            }

            return Color.Lerp(new Color(2.0f, 0.62f, 0.02f, 1f), new Color(2.45f, 1.92f, 0.02f, 1f), (t - 0.78f) / 0.22f);
        }

        private static float GetEqualizerBehaviorT(float colorT)
        {
            if (colorT < EqualizerBlueBandEnd)
            {
                return Mathf.Lerp(EqualizerBlueBandEnd, 1f, Mathf.Clamp01(colorT / EqualizerBlueBandEnd));
            }

            return colorT;
        }

        private static void WriteMesh(Mesh mesh, List<Vector3> vertices, List<int> triangles)
        {
            mesh.Clear();
            if (vertices.Count == 0 || triangles.Count == 0)
            {
                return;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private Material CreateFontMaterial()
        {
            var sourceMaterial = font != null ? font.material : null;
            var textShader = Shader.Find("ArenaShooter/WorldSpaceTextDepthTested");
            if (textShader != null)
            {
                var material = new Material(textShader) { name = "Dome Scoreboard Font Depth Material" };
                if (sourceMaterial != null && sourceMaterial.mainTexture != null && material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", sourceMaterial.mainTexture);
                }

                SetMaterialColor(material, Color.white);
                ConfigureDepthTestedTextMaterial(material);
                return material;
            }

            if (sourceMaterial != null)
            {
                var material = new Material(sourceMaterial) { name = "Dome Scoreboard Font Material" };
                ConfigureDepthTestedTextMaterial(material);
                return material;
            }

            var fallback = CreateUnlitMaterial("Dome Scoreboard Font Fallback", Color.white, Color.white * 1.5f);
            ConfigureDepthTestedTextMaterial(fallback);
            return fallback;
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
            SetMaterialColor(material, baseColor);
            SetMaterialEmission(material, emission);
            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }

            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void SetMaterialEmission(Material material, Color emission)
        {
            if (material == null || emission.maxColorComponent <= 0f)
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
            }
        }

        private static void ConfigureDepthTestedTextMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_ZTest"))
            {
                material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            }

            material.renderQueue = TextRenderQueue;
        }

        private static void ConfigureRenderer(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private readonly struct DomePatch
        {
            public DomePatch(float phi, float latitude, float rollDegrees)
            {
                Phi = phi;
                Latitude = latitude;
                var rollRadians = rollDegrees * Mathf.Deg2Rad;
                RollCos = Mathf.Cos(rollRadians);
                RollSin = Mathf.Sin(rollRadians);
            }

            public float Phi { get; }
            public float Latitude { get; }
            private float RollCos { get; }
            private float RollSin { get; }

            public Vector2 Roll(Vector2 localPosition)
            {
                return new Vector2(
                    localPosition.x * RollCos - localPosition.y * RollSin,
                    localPosition.x * RollSin + localPosition.y * RollCos);
            }
        }

        private readonly struct GlyphQuad
        {
            public GlyphQuad(float minX, float maxX, float minY, float maxY, Vector2 uvBottomLeft, Vector2 uvTopLeft, Vector2 uvTopRight, Vector2 uvBottomRight)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
                UvBottomLeft = uvBottomLeft;
                UvTopLeft = uvTopLeft;
                UvTopRight = uvTopRight;
                UvBottomRight = uvBottomRight;
            }

            public float MinX { get; }
            public float MaxX { get; }
            public float MinY { get; }
            public float MaxY { get; }
            public Vector2 UvBottomLeft { get; }
            public Vector2 UvTopLeft { get; }
            public Vector2 UvTopRight { get; }
            public Vector2 UvBottomRight { get; }
        }

        private sealed class CurvedTextLabel
        {
            public CurvedTextLabel(Mesh mesh, DomePatch patch, Vector2 localPosition, float textHeight, TextAnchor anchor, Color color, float radiusInset)
            {
                Mesh = mesh;
                Patch = patch;
                LocalPosition = localPosition;
                TextHeight = textHeight;
                Anchor = anchor;
                Color = color;
                RadiusInset = radiusInset;
            }

            private Mesh Mesh { get; }
            private DomePatch Patch { get; }
            private Vector2 LocalPosition { get; }
            private float TextHeight { get; }
            private TextAnchor Anchor { get; }
            private Color Color { get; }
            private float RadiusInset { get; }
            private string Value { get; set; }

            public void SetText(AllOutWarDomeScoreboard owner, string value)
            {
                if (owner == null || value == Value)
                {
                    return;
                }

                Value = value;
                owner.WriteCurvedTextMesh(Mesh, Patch, value, LocalPosition, TextHeight, Anchor, Color, RadiusInset);
            }
        }

        private sealed class ArmyRow
        {
            public int Army;
            public DomePatch Patch;
            public Mesh FillMesh;
            public float FillStartX;
            public float FillDirection;
            public float FillY;
            public float FillWidth;
            public CurvedTextLabel Count;
            public int LastRemaining;
            public int LastStarting;
        }

        private sealed class EqualizerColumn
        {
            public DomePatch Patch;
            public Mesh Mesh;
            public Material Material;
            public Color BaseColor;
            public float LocalX;
            public int SkylineTiles;
            public float BehaviorT;
            public float Phase;
            public float CurrentLevel;
            public int LastActiveTiles;
        }
    }
}
