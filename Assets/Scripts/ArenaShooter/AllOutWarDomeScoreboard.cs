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
        private const float DomeVerticalUnitsPerLatitude = 226f;
        private const float ScoreboardMeshSegmentLength = 1.6f;

        // The four boards hang as a center-court chandelier box, like a basketball
        // arena centerhung display. Depths are measured outward from each board face
        // toward the viewer, replacing the old dome-surface radius insets. The rig
        // scales and hangs lower on small maps so it stays inside a normal FOV.
        private const float ChandelierRotationDegreesPerSecond = 3.2f;
        private const float ChandelierPlateOverhang = 4f;
        private const float ChandelierFaceOffset = BoardWidth * ScoreboardLocalScale * 0.5f;
        private const float BoardBedDepth = 0f;
        private const float BoardPanelDepth = 0.5f;
        private const float BoardOuterBorderDepth = 1.0f;
        private const float BoardRowDepth = 1.5f;
        private const float BoardDetailDepth = 1.85f;
        private const float BoardFillDepth = 2.15f;
        private const float BoardTextDepth = 2.65f;
        private const float BoardOuterBorderThickness = 2.4f;

        // Board content is tuned for distance reading: one big accent-colored army
        // number, a thick remaining-forces bar, and a tall count — no small capsules.
        private const float ScoreboardLocalScale = 0.8f;
        private const float BoardWidth = 146f;
        private const float RowContentWidth = 138f;
        private const float BoardCornerRadius = 7.0f;
        private const float BoardTopPadding = 3.2f;
        private const float BoardBottomPadding = 5.8f;
        private const float HeaderHeight = 9.5f;
        private const float HeaderRowGap = 2.6f;
        private const float RowHeight = 13.5f;
        private const float RowStride = 15.8f;
        private const float RowFillWidth = 72f;
        private const float RowFillBackWidth = 76f;
        private const float RowFillHeight = 4.6f;
        private const float RowFillBackHeight = 5.8f;
        private const float RowNumberX = 59f;
        private const float RowCountX = -56f;
        private const float RowFillCenterX = 2f;
        private const float HeaderTextSize = 7.4f;
        private const float RowNumberTextSize = 9.5f;
        private const float RowCountTextSize = 9.5f;

        private const int TextFontSize = 128;
        // The bars themselves are drawn inside the mirror gallery tiles by
        // DomeGalleryGlass.shader (which mirrors the tile/pitch geometry); the
        // scoreboard only computes the per-column levels and streams them into
        // the gallery material as the _EqualizerBars vector array.
        private const int EqualizerColumnCount = 120;
        private const int EqualizerMaxTiles = 24;
        private const float EqualizerBlueBandEnd = 0.24f;
        // New tracks fade their skyline in over this window so the bars open
        // small (songs start quiet) and grow with the music.
        private const float EqualizerIntroSeconds = 12f;

        private static readonly Color HaloColor = new Color(0.04f, 0.62f, 0.95f, 1f);
        private static readonly int EqualizerBarsId = Shader.PropertyToID("_EqualizerBars");

        private readonly List<ArmyRow> rows = new();
        private readonly List<EqualizerColumn> equalizerColumns = new();
        private readonly Vector4[] equalizerBarData = new Vector4[EqualizerColumnCount];
        private readonly Dictionary<int, Material> armyMaterials = new();
        private readonly float[] spectrumSamples = new float[SpectrumSampleCount];

        private MatchController match;
        private Font font;
        private StadiumVisualMetrics metrics;
        private Transform chandelierRoot;
        private float chandelierScale = 1f;
        private float chandelierHang = 88f;
        private float nextScoreRefreshAt;
        private float nextEqualizerRefreshAt;
        private bool musicWasPlaying;
        private float songStartedAt = -1000f;
        private int builtArmyCount = -1;
        private Material panelMaterial;
        private Material rowBackMaterial;
        private Material capsuleMaterial;
        private Material fillBackMaterial;
        private Material hotPurpleRailMaterial;
        private Material haloMaterial;
        private Material bedGlowMaterial;
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
            var playfieldRadius = CalculatePlayfieldRadius(layout);
            chandelierScale = Mathf.Clamp(playfieldRadius / 560f, 0.24f, 0.7f);
            chandelierHang = Mathf.Clamp(playfieldRadius * 0.6f, 42f, 88f);
            panelMaterial = CreateUnlitMaterial("Dome Scoreboard Embedded Black Glass", new Color(0.0012f, 0.001f, 0.0058f, 1f), new Color(0.008f, 0.0015f, 0.024f, 1f));
            rowBackMaterial = CreateUnlitMaterial("Dome Scoreboard Row Black Glass", new Color(0.0024f, 0.002f, 0.010f, 1f), new Color(0.010f, 0.0015f, 0.022f, 1f));
            capsuleMaterial = CreateUnlitMaterial("Dome Scoreboard Label Capsule Glass", new Color(0.003f, 0.0025f, 0.013f, 1f), new Color(0.014f, 0.002f, 0.032f, 1f));
            fillBackMaterial = CreateUnlitMaterial("Dome Scoreboard Fill Back Glass", new Color(0.008f, 0.002f, 0.018f, 1f), new Color(0.012f, 0.0015f, 0.024f, 1f));
            hotPurpleRailMaterial = CreateUnlitMaterial("Dome Scoreboard Hot Purple Rail", new Color(1.06f, 0.04f, 2.35f, 1f), new Color(2.75f, 0.08f, 6.0f, 1f));
            haloMaterial = CreateUnlitMaterial("Chandelier Music Halo Cyan", new Color(0.02f, 0.4f, 0.62f, 1f), new Color(0.05f, 1.1f, 1.7f, 1f));
            bedGlowMaterial = CreateUnlitMaterial("Chandelier Board Bed Glow", new Color(0.17f, 0.03f, 0.36f, 1f), new Color(0.5f, 0.08f, 1.05f, 1f));
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

            if (chandelierRoot != null)
            {
                chandelierRoot.Rotate(0f, ChandelierRotationDegreesPerSecond * Time.deltaTime, 0f);
            }
        }

        private void RebuildVisuals()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            rows.Clear();
            equalizerColumns.Clear();
            chandelierRoot = null;
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

            var chandelier = new GameObject("Center Court Chandelier");
            chandelier.transform.SetParent(transform, false);
            chandelier.transform.localPosition = metrics.Center + Vector3.up * chandelierHang;
            chandelier.transform.localScale = Vector3.one * chandelierScale;
            chandelierRoot = chandelier.transform;

            for (var bank = 0; bank < BankCount; bank++)
            {
                var angle = bank / (float)BankCount * Mathf.PI * 2f;
                CreateScoreboard($"Chandelier Scoreboard {bank + 1}", new DomePatch(angle, 0f, 0f), BoardWidth, boardHeight);
            }

            CreateChandelierRig(chandelierRoot, boardHeight * ScoreboardLocalScale * 0.5f);
        }

        private void CreateScoreboard(string objectName, DomePatch patch, float boardWidth, float boardHeight)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(chandelierRoot, false);

            var boardSize = new Vector2(boardWidth, boardHeight);
            CreateReferenceScoreboardFrame(root.transform, patch, boardSize);

            var headerY = boardHeight * 0.5f - BoardTopPadding - HeaderHeight * 0.5f;
            CreateCurvedTextLabel("Header Text", root.transform, patch, "ARMY COUNT", new Vector2(0f, headerY), HeaderTextSize, TextAnchor.MiddleCenter, new Color(1f, 0.98f, 1f, 1f), BoardTextDepth);

            var rowStartY = headerY - HeaderHeight * 0.5f - HeaderRowGap - RowHeight * 0.5f;
            for (var army = 0; army < builtArmyCount; army++)
            {
                CreateArmyRow(root.transform, patch, army, rowStartY - army * RowStride);
            }
        }

        private void CreateReferenceScoreboardFrame(Transform parent, DomePatch patch, Vector2 boardSize)
        {
            CreateCurvedRoundedPanel("Board Glow Bed", parent, patch, Vector2.zero, boardSize + new Vector2(7f, 4.2f), BoardCornerRadius + 2.2f, BoardBedDepth, bedGlowMaterial);
            CreateCurvedRoundedPanel("Board Black Glass", parent, patch, Vector2.zero, boardSize - new Vector2(3.2f, 3f), Mathf.Max(1.2f, BoardCornerRadius - 1.2f), BoardPanelDepth, panelMaterial);

            // A single clean neon frame; a second inner border just smears into it at
            // chandelier scale and steals contrast from the content.
            var outerBorderSize = boardSize + new Vector2(4.4f, 3.2f);
            CreateCurvedRoundedBorder("Outer Neon Purple Border", parent, patch, Vector2.zero, outerBorderSize, BoardCornerRadius + 1.4f, BoardOuterBorderThickness, BoardOuterBorderDepth, hotPurpleRailMaterial);
        }

        private void CreateArmyRow(Transform parent, DomePatch patch, int army, float rowY)
        {
            var armyMaterial = GetArmyMaterial(army);
            var rowWidth = RowContentWidth;
            var rowSize = new Vector2(rowWidth, RowHeight);
            CreateCurvedClippedPanel("Row Black Glass", parent, patch, new Vector2(0f, rowY), rowSize, 3.0f, BoardRowDepth, rowBackMaterial);

            var fillStartX = RowFillCenterX + RowFillWidth * 0.5f;
            CreateCurvedClippedPanel("Fill Back", parent, patch, new Vector2(RowFillCenterX, rowY), new Vector2(RowFillBackWidth, RowFillBackHeight), 1.6f, BoardDetailDepth, fillBackMaterial);
            var fill = CreateCurvedSolidRect("Fill", parent, patch, new Vector2(RowFillCenterX, rowY), new Vector2(RowFillWidth, RowFillHeight), BoardFillDepth, armyMaterial);

            var accent = AllOutWarArmyVisuals.GetAccent(army);
            CreateCurvedTextLabel("Army Number", parent, patch, army.ToString(), new Vector2(RowNumberX, rowY), RowNumberTextSize, TextAnchor.MiddleCenter, new Color(accent.r * 1.5f, accent.g * 1.5f, accent.b * 1.5f, 1f), BoardTextDepth);
            var count = CreateCurvedTextLabel("Army Count", parent, patch, "000", new Vector2(RowCountX, rowY), RowCountTextSize, TextAnchor.MiddleCenter, new Color(1f, 0.99f, 0.97f, 1f), BoardTextDepth);

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

        private void CreateChandelierRig(Transform parent, float boardHalfHeight)
        {
            var plateHalf = ChandelierFaceOffset + ChandelierPlateOverhang;
            var topY = boardHalfHeight + 1.1f;
            var bottomY = -boardHalfHeight - 1.1f;
            var cornerRadius = ChandelierFaceOffset * 1.41421f;

            CreateChandelierBlock("Chandelier Top Plate", parent, Vector3.up * topY, new Vector3(plateHalf * 2f, 2.2f, plateHalf * 2f), Quaternion.identity, panelMaterial);
            CreateChandelierBlock("Chandelier Bottom Plate", parent, Vector3.up * bottomY, new Vector3(plateHalf * 2f, 2.2f, plateHalf * 2f), Quaternion.identity, panelMaterial);

            for (var corner = 0; corner < 4; corner++)
            {
                var angle = (corner + 0.5f) / 4f * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // The light strip mounts just past the pillar's outer corner edge.
                CreateChandelierBlock($"Chandelier Corner Pillar {corner + 1}", parent, direction * cornerRadius, new Vector3(8f, boardHalfHeight * 2f + 4f, 8f), Quaternion.Euler(0f, 45f, 0f), capsuleMaterial);
                CreateChandelierBlock($"Chandelier Corner Light {corner + 1}", parent, direction * (cornerRadius + 7.2f), new Vector3(3f, boardHalfHeight * 2f + 2f, 3f), Quaternion.Euler(0f, 45f, 0f), hotPurpleRailMaterial);
            }

            CreateChandelierRing("Chandelier Crown Ring", parent, topY + 1.6f, plateHalf - 5.5f, plateHalf - 1.5f, hotPurpleRailMaterial);
            CreateChandelierRing("Chandelier Music Halo", parent, bottomY - 1.6f, plateHalf - 5.5f, plateHalf - 1.5f, haloMaterial);
            CreateChandelierRing("Chandelier Under Hub", parent, bottomY - 1.3f, 1.2f, 4.5f, hotPurpleRailMaterial);

            // The rig is built in chandelier-local space, which the root scales down on
            // small maps — divide so the mast and cables still reach the dome apex.
            var apexLocalY = (metrics.BaseY + metrics.Height * Mathf.Sin(AllOutWarStadiumVisuals.LatitudeToTheta(1f)) - chandelierHang) / Mathf.Max(0.1f, chandelierScale);
            var mastBottom = topY + 1f;
            var mastHeight = Mathf.Max(2f, apexLocalY - mastBottom);
            CreateChandelierBlock("Chandelier Mast", parent, Vector3.up * (mastBottom + mastHeight * 0.5f), new Vector3(3.4f, mastHeight, 3.4f), Quaternion.identity, capsuleMaterial);
            CreateChandelierRing("Chandelier Mast Collar Low", parent, mastBottom + mastHeight * 0.3f, 2.4f, 4.2f, hotPurpleRailMaterial);
            CreateChandelierRing("Chandelier Mast Collar High", parent, mastBottom + mastHeight * 0.7f, 2.4f, 4.2f, hotPurpleRailMaterial);

            var anchor = Vector3.up * (apexLocalY - 1.5f);
            for (var corner = 0; corner < 4; corner++)
            {
                var angle = (corner + 0.5f) / 4f * Mathf.PI * 2f;
                var start = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (plateHalf * 0.82f) + Vector3.up * topY;
                var delta = anchor - start;
                CreateChandelierBlock($"Chandelier Cable {corner + 1}", parent, (start + anchor) * 0.5f, new Vector3(1.4f, delta.magnitude, 1.4f), Quaternion.FromToRotation(Vector3.up, delta.normalized), hotPurpleRailMaterial);
            }
        }

        private static float CalculatePlayfieldRadius(ArenaLayout layout)
        {
            var radius = 80f;
            if (layout?.RoomCenters == null)
            {
                return radius;
            }

            foreach (var room in layout.RoomCenters.Values)
            {
                var delta = room - layout.CircularCenter;
                delta.y = 0f;
                radius = Mathf.Max(radius, delta.magnitude + 30f);
            }

            return radius;
        }

        private GameObject CreateChandelierBlock(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            var collider = block.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = localRotation;
            block.transform.localScale = localScale;
            ConfigureRenderer(block.GetComponent<MeshRenderer>(), material);
            return block;
        }

        private GameObject CreateChandelierRing(string objectName, Transform parent, float y, float innerRadius, float outerRadius, Material material)
        {
            const int segments = 48;
            var ring = new GameObject(objectName);
            ring.transform.SetParent(parent, false);
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * innerRadius + Vector3.up * y;
                vertices[i * 2 + 1] = direction * outerRadius + Vector3.up * y;
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

            var mesh = new Mesh { name = $"{objectName} Mesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);
            return ring;
        }

        private void CreateEqualizer()
        {
            // No geometry: the gallery glass shader draws the bars inside the
            // mirror tiles. This just seeds the per-column skyline state that
            // UpdateEqualizer animates and streams into the gallery material.
            // (The per-column colour ramp lives in the shader as EqualizerRamp.)
            for (var columnIndex = 0; columnIndex < EqualizerColumnCount; columnIndex++)
            {
                var colorT = EqualizerColumnCount > 1 ? columnIndex / (float)(EqualizerColumnCount - 1) : 0f;
                var behaviorT = GetEqualizerBehaviorT(colorT);
                equalizerColumns.Add(new EqualizerColumn
                {
                    SkylineTiles = GetEqualizerSkylineTiles(behaviorT, columnIndex),
                    BehaviorT = behaviorT,
                    Phase = columnIndex * 0.371f + 4.7f,
                    CurrentLevel = 0f
                });
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
            if (equalizerColumns.Count == 0)
            {
                return;
            }

            var audio = ArenaAudio.Instance;
            var musicPlaying = audio != null && audio.IsGameplayMusicPlaying;
            if (musicPlaying && !musicWasPlaying)
            {
                songStartedAt = now;
            }

            musicWasPlaying = musicPlaying;
            var introRamp = Mathf.Clamp01((now - songStartedAt) / EqualizerIntroSeconds);
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

            var levelSum = 0f;
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
                // The skyline tracks the music level directly (no standing floor)
                // and a fresh track ramps in from small; in the intermission the
                // columns sink to zero and the threshold below removes them
                // outright — CeilToInt on the decaying residue used to pin one
                // tile alive around the whole hemisphere after the song ended.
                var skylineMotion = column.CurrentLevel * (musicPlaying ? Mathf.Lerp(0.35f, 1f, introRamp) : 1f);
                var activeTiles = column.SkylineTiles > 0 && skylineMotion > 0.035f
                    ? Mathf.Clamp(Mathf.CeilToInt(skylineMotion * column.SkylineTiles), 1, column.SkylineTiles)
                    : 0;

                // Brightness multiplier on the shader's colour ramp; matches the
                // old mesh bars' base-colour level plus emission glow.
                var glow = musicPlaying ? Mathf.Lerp(1.15f, 3.6f, column.CurrentLevel) : 0.55f;
                var intensity = Mathf.Lerp(0.48f, 1.05f, column.CurrentLevel) + glow;
                equalizerBarData[i] = new Vector4(activeTiles, intensity, 0f, 0f);
                levelSum += column.CurrentLevel;
            }

            var paneMaterial = ShieldDomeBackdrop.GalleryPaneMaterial;
            if (paneMaterial != null)
            {
                paneMaterial.SetVectorArray(EqualizerBarsId, equalizerBarData);
            }

            if (haloMaterial != null && equalizerColumns.Count > 0)
            {
                var average = levelSum / equalizerColumns.Count;
                var pulse = musicPlaying ? Mathf.Lerp(0.3f, 1f, average) : 0.22f;
                SetMaterialColor(haloMaterial, HaloColor * pulse);
                SetMaterialEmission(haloMaterial, HaloColor * (musicPlaying ? Mathf.Lerp(0.8f, 3.4f, average) : 0.45f));
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
                        WriteCurvedSolidRectMesh(row.FillMesh, row.Patch, center, new Vector2(width, RowFillHeight), BoardFillDepth);
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

        private Vector3 ScoreboardPoint(DomePatch patch, Vector2 localPosition, float depth)
        {
            // Chandelier-local space: each bank is a flat vertical face of the
            // centerhung box, like a real arena display. patch.Phi picks the face
            // direction; depth layers panels toward the viewer.
            var rolled = patch.Roll(ScaleScoreboardLocalPosition(localPosition));
            var outward = new Vector3(Mathf.Cos(patch.Phi), 0f, Mathf.Sin(patch.Phi));
            var right = new Vector3(Mathf.Sin(patch.Phi), 0f, -Mathf.Cos(patch.Phi));
            return outward * (ChandelierFaceOffset + depth) + right * rolled.x + Vector3.up * rolled.y;
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

        // Reference implementation of the per-column colour ramp; the live bars
        // are coloured by its mirror in DomeGalleryGlass.shader (EqualizerRamp).
        // Kept (and pinned by EditMode tests) so ramp changes start here.
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
            public int SkylineTiles;
            public float BehaviorT;
            public float Phase;
            public float CurrentLevel;
        }
    }
}
