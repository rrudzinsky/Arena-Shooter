using UnityEditor;
using UnityEditor.SceneManagement;

namespace ArenaShooter.Editor
{
    [InitializeOnLoad]
    public static class MainMenuPlayModeStartScene
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        static MainMenuPlayModeStartScene()
        {
            var mainMenuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
            if (mainMenuScene != null && EditorSceneManager.playModeStartScene != mainMenuScene)
            {
                EditorSceneManager.playModeStartScene = mainMenuScene;
            }
        }
    }
}
