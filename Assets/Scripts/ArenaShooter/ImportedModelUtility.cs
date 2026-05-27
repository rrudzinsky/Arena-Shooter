using UnityEngine;
using System.Collections.Generic;

namespace ArenaShooter
{
    internal static class ImportedModelUtility
    {
        private const int MaxModelLogsPerLabel = 8;
        private static readonly Dictionary<string, int> ModelDiagnosticCounts = new Dictionary<string, int>();

        public static void RemoveColliders(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                DestroyObject(collider);
            }
        }

        public static void RemoveImportedCamerasAndLights(GameObject instance)
        {
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true))
            {
                DestroyObject(camera.gameObject);
            }

            foreach (var light in instance.GetComponentsInChildren<Light>(true))
            {
                DestroyObject(light.gameObject);
            }
        }

        public static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
                return;
            }

            Object.DestroyImmediate(target);
        }

        public static bool TryNormalizeRendererBounds(GameObject root, Transform scaleTarget, float targetMaxDimension, string label)
        {
            if (root == null || scaleTarget == null || targetMaxDimension <= 0f)
            {
                return false;
            }

            if (!TryCalculateRendererBounds(root, out var bounds))
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning(
                        "[Arena Shooter Model Diagnostics] Bounds normalization skipped: " +
                        $"label={label} root={root.name} reason=no-renderer-bounds.");
                }

                return false;
            }

            var currentMaxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (currentMaxDimension <= 0.0001f)
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning(
                        "[Arena Shooter Model Diagnostics] Bounds normalization skipped: " +
                        $"label={label} root={root.name} reason=tiny-bounds currentBounds={bounds.size}.");
                }

                return false;
            }

            var scaleFactor = targetMaxDimension / currentMaxDimension;
            if (Mathf.Abs(scaleFactor - 1f) <= 0.02f)
            {
                return false;
            }

            var beforeScale = scaleTarget.localScale;
            scaleTarget.localScale = Vector3.Scale(scaleTarget.localScale, Vector3.one * scaleFactor);

            if (Application.isPlaying)
            {
                var afterBounds = TryCalculateRendererBounds(root, out var updatedBounds) ? updatedBounds.size.ToString() : "none";
                Debug.Log(
                    "[Arena Shooter Model Diagnostics] Bounds normalization applied: " +
                    $"label={label} root={root.name} scaleTarget={scaleTarget.name} " +
                    $"targetMaxDimension={targetMaxDimension:F3} currentMaxDimension={currentMaxDimension:F6} " +
                    $"scaleFactor={scaleFactor:F3} beforeScale={beforeScale} afterScale={scaleTarget.localScale} " +
                    $"beforeBounds={bounds.size} afterBounds={afterBounds}.");
            }

            return true;
        }

        private static bool TryCalculateRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        public static void LogModelInstanceDiagnostics(string label, GameObject root)
        {
            if (!Application.isPlaying || root == null)
            {
                return;
            }

            ModelDiagnosticCounts.TryGetValue(label, out var count);
            count++;
            ModelDiagnosticCounts[label] = count;
            if (count > MaxModelLogsPerLabel && count != 25 && count != 100 && count % 250 != 0)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            var skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var activeRenderers = 0;
            var enabledRenderers = 0;
            var transparentRenderers = 0;
            var missingMaterials = 0;
            var zeroBounds = 0;
            var totalVertices = 0;
            var bounds = default(Bounds);
            var hasBounds = false;
            var examples = string.Empty;

            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    totalVertices += meshFilter.sharedMesh.vertexCount;
                }
            }

            foreach (var skinnedMesh in skinnedMeshes)
            {
                if (skinnedMesh != null && skinnedMesh.sharedMesh != null)
                {
                    totalVertices += skinnedMesh.sharedMesh.vertexCount;
                }
            }

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.gameObject.activeInHierarchy)
                {
                    activeRenderers++;
                }

                if (renderer.enabled)
                {
                    enabledRenderers++;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                if (renderer.bounds.size.sqrMagnitude <= 0.000001f)
                {
                    zeroBounds++;
                }

                var material = renderer.sharedMaterial;
                if (material == null)
                {
                    missingMaterials++;
                }
                else if (material.renderQueue > 2500)
                {
                    transparentRenderers++;
                }

                if (examples.Length < 900)
                {
                    examples +=
                        $" [{renderer.name}|active={renderer.gameObject.activeInHierarchy}|enabled={renderer.enabled}|mask={renderer.renderingLayerMask}|maskName={DroidRenderSetup.DescribeRenderingLayerMask(renderer.renderingLayerMask)}|queue={(material != null ? material.renderQueue.ToString() : "none")}|shader={(material != null && material.shader != null ? material.shader.name : "none")}|bounds={renderer.bounds.size}]";
                }
            }

            Debug.Log(
                "[Arena Shooter Model Diagnostics] Imported/model instance: " +
                $"label={label} labelCount={count} root={root.name} rootActive={root.activeInHierarchy} " +
                $"localPosition={root.transform.localPosition} localScale={root.transform.localScale} worldScale={root.transform.lossyScale} " +
                $"renderers={renderers.Length} activeRenderers={activeRenderers} enabledRenderers={enabledRenderers} " +
                $"meshFilters={meshFilters.Length} skinnedMeshes={skinnedMeshes.Length} totalVertices={totalVertices} " +
                $"transparentRenderers={transparentRenderers} missingMaterials={missingMaterials} zeroBounds={zeroBounds} " +
                $"aggregateBounds={(hasBounds ? bounds.size.ToString() : "none")} examples={examples}");
        }
    }
}
