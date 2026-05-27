using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArenaShooterSceneSetup
{
    [MenuItem("Arena Shooter/Setup Prototype Scene")]
    public static void SetupPrototypeScene()
    {
        var bootstrap = GameObject.Find("GameBootstrap");
        if (bootstrap == null)
        {
            bootstrap = new GameObject("GameBootstrap");
        }

        if (bootstrap.GetComponent<CyberArenaBootstrap>() == null)
        {
            bootstrap.AddComponent<CyberArenaBootstrap>();
        }

        EditorSceneManager.MarkSceneDirty(bootstrap.scene);
        EditorSceneManager.SaveScene(bootstrap.scene);
        Debug.Log("[Arena Shooter] Prototype scene setup complete.");
    }
}
