using ArenaShooter;
using ArenaShooter.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class DroidOutlineRendererInstaller
{
    private static readonly string[] RendererAssetPaths =
    {
        "Assets/Settings/PC_Renderer.asset",
        "Assets/Settings/Mobile_Renderer.asset"
    };

    [MenuItem("Arena Shooter/Rendering/Install Droid Outline Renderer Feature")]
    public static void Install()
    {
        EnsureDroidOutlineLayer();

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
                feature.name = "Synthwave Screen Space Outlines";

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

            feature.name = "Synthwave Screen Space Outlines";
            feature.settings.outlineBands = DroidOutlineRendererFeature.CreateDefaultBands();
            feature.settings.downsample = 1;
            feature.settings.thicknessPixels = 2;
            feature.settings.glowPixels = 3;
            feature.settings.intensity = 2.35f;
            feature.settings.normalEdgeThreshold = 0.12f;
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

    private static void EnsureDroidOutlineLayer()
    {
        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var serializedTagManager = new SerializedObject(tagManager);
        var layers = serializedTagManager.FindProperty("layers");

        for (var i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == DroidRenderSetup.OutlineLayerName)
            {
                return;
            }
        }

        var fallbackLayer = Mathf.Clamp(DroidRenderSetup.FallbackOutlineLayer, 0, layers.arraySize - 1);
        layers.GetArrayElementAtIndex(fallbackLayer).stringValue = DroidRenderSetup.OutlineLayerName;
        serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
    }
}
