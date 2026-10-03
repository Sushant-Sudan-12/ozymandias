using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KKK.UI
{
    /// <summary>
    /// Automatically tracks and saves the last visited gameplay scene.
    /// Used by the MainMenu to show and handle the 'Continue' button.
    /// </summary>
    public static class GameProgressTracker
    {
        private const string PrefsKeyLastScene = "KKK_LastPlayedScene";
        private const string MainMenuSceneName = "MainMenu";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Do not record MainMenu as the last played gameplay scene
            if (scene.name.Equals(MainMenuSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Save the active gameplay scene
            SaveScene(scene.name);
        }

        /// <summary>
        /// Returns true if a previous gameplay scene was saved.
        /// </summary>
        public static bool HasSavedGame()
        {
            return PlayerPrefs.HasKey(PrefsKeyLastScene) && !string.IsNullOrEmpty(PlayerPrefs.GetString(PrefsKeyLastScene));
        }

        /// <summary>
        /// Gets the name of the last visited gameplay scene. Defaults to 'Forest'.
        /// </summary>
        public static string GetLastPlayedScene()
        {
            return PlayerPrefs.GetString(PrefsKeyLastScene, "Forest");
        }

        /// <summary>
        /// Saves a specific scene name as the last played scene.
        /// </summary>
        public static void SaveScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || sceneName.Equals(MainMenuSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PlayerPrefs.SetString(PrefsKeyLastScene, sceneName);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clears saved progress (useful for resetting game data).
        /// </summary>
        public static void ClearSavedProgress()
        {
            PlayerPrefs.DeleteKey(PrefsKeyLastScene);
            PlayerPrefs.Save();
        }
    }
}
