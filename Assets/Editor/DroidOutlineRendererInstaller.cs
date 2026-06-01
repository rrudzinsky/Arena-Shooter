using ArenaShooter;
using ArenaShooter.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class DroidOutlineRendererInstaller
{
    private static readonly string[] RequiredRenderingLayerNames =
    {
        "Default",
        "Floor",
        "Wall",
        "Droid",
        "Medical",
        "Ammo",
        "Gun"
    };

    private static readonly string[] RendererAssetPaths =
    {
        "Assets/Settings/PC_Renderer.asset",
        "Assets/Settings/Mobile_Renderer.asset"
    };

    [MenuItem("Arena Shooter/Rendering/Install Stylized Outline Renderer Feature")]
    public static void Install()
    {
        EnsureProjectLayers();

        foreach (var assetPath in RendererAssetPaths)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(assetPath);
            if (rendererData == null)
            {
                continue;
            }

            var feature = FindFeature(rendererData);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<DroidOutlineRendererFeature>();
                feature.name = "Stylized Neon Screen Space Outlines";

                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var serializedRenderer = new SerializedObject(rendererData);
                var features = serializedRenderer.FindProperty("m_RendererFeatures");
                var featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");

                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                featureMap.arraySize++;
                featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;

                serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            }

            feature.name = "Stylized Neon Screen Space Outlines";
            feature.settings.outlineBands = DroidOutlineRendererFeature.CreateDefaultBands();
            feature.settings.downsample = 1;
            feature.settings.thicknessPixels = 1;
            feature.settings.glowPixels = 2;
            feature.settings.intensity = DroidRenderSetup.DefaultOutlineIntensity;
            feature.settings.normalEdgeThreshold = 0.16f;
            feature.settings.useReferenceNeonStyle = true;
            feature.settings.diagnosticMode = DroidOutlineRendererFeature.OutlineDiagnosticMode.Off;
            feature.settings.diagnosticBandIndex = -1;
            feature.settings.diagnosticIgnoreRenderingLayerFilter = false;
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(feature);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Droid outline renderer feature installed.");
    }

    private static DroidOutlineRendererFeature FindFeature(ScriptableRendererData rendererData)
    {
        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature is DroidOutlineRendererFeature droidOutline)
            {
                return droidOutline;
            }
        }

        return null;
    }

    private static void EnsureProjectLayers()
    {
        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var serializedTagManager = new SerializedObject(tagManager);
        var layers = serializedTagManager.FindProperty("layers");
        var renderingLayers = serializedTagManager.FindProperty("m_RenderingLayers");

        EnsureDroidOutlineLayer(layers);
        EnsureRenderingLayerNames(renderingLayers);
        serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureDroidOutlineLayer(SerializedProperty layers)
    {
        for (var i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == DroidRenderSetup.OutlineLayerName)
            {
                return;
            }
        }

        var fallbackLayer = Mathf.Clamp(DroidRenderSetup.FallbackOutlineLayer, 0, layers.arraySize - 1);
        layers.GetArrayElementAtIndex(fallbackLayer).stringValue = DroidRenderSetup.OutlineLayerName;
    }

    private static void EnsureRenderingLayerNames(SerializedProperty renderingLayers)
    {
        if (renderingLayers == null)
        {
            return;
        }

        for (var i = 0; i < RequiredRenderingLayerNames.Length && i < renderingLayers.arraySize; i++)
        {
            renderingLayers.GetArrayElementAtIndex(i).stringValue = RequiredRenderingLayerNames[i];
        }
    }
}
