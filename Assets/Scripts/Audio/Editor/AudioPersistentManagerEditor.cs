#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KKK.Audio
{
    [CustomEditor(typeof(AudioPersistentManager))]
    public class AudioPersistentManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AudioPersistentManager manager = (AudioPersistentManager)target;

            // Header Banner
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎵 Persistent Audio Manager", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Handles Main Menu & Gameplay Background Music across all scenes with seamless looping & crossfading.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);

            // Music Clips
            EditorGUILayout.LabelField("Audio Clips", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mainMenuMusic"), new GUIContent("Main Menu Music", "Clip played in MainMenu scene."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gameplayBgMusic"), new GUIContent("Gameplay BG Music", "Clip played during gameplay and looped across all levels."));

            EditorGUILayout.Space(8);

            // Volume Sliders
            EditorGUILayout.LabelField("Volume Controls", EditorStyles.boldLabel);
            SerializedProperty masterVol = serializedObject.FindProperty("masterMusicVolume");
            SerializedProperty menuVol = serializedObject.FindProperty("mainMenuVolume");
            SerializedProperty bgVol = serializedObject.FindProperty("bgMusicVolume");
            SerializedProperty sfxVol = serializedObject.FindProperty("sfxVolume");
            SerializedProperty muteProp = serializedObject.FindProperty("mute");

            EditorGUILayout.Slider(masterVol, 0f, 1f, new GUIContent("Master Music Volume", "Overall music volume multiplier."));
            EditorGUILayout.Slider(menuVol, 0f, 1f, new GUIContent("Main Menu Volume", "Specific multiplier for Main Menu music."));
            EditorGUILayout.Slider(bgVol, 0f, 1f, new GUIContent("BG Music Volume", "Specific multiplier for Gameplay BG music."));
            EditorGUILayout.Slider(sfxVol, 0f, 1f, new GUIContent("SFX Volume", "Master multiplier for Sound Effects."));
            EditorGUILayout.PropertyField(muteProp, new GUIContent("Mute Music", "Mute all music output."));

            EditorGUILayout.Space(8);

            // Crossfade & Scene Settings
            EditorGUILayout.LabelField("Settings & Routing", EditorStyles.boldLabel);
            EditorGUILayout.Slider(serializedObject.FindProperty("crossfadeDuration"), 0.1f, 5.0f, new GUIContent("Crossfade Duration (s)", "Fade transition time between clips."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mainMenuSceneName"), new GUIContent("Main Menu Scene Name", "Exact name of Main Menu scene."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoSceneRouting"), new GUIContent("Auto Scene Routing", "Automatically switch tracks on scene load."));

            serializedObject.ApplyModifiedProperties();

            // Play Mode Runtime Controls
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Playback Status (Play Mode)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Active Track: {manager.CurrentTrackType}");
                EditorGUILayout.LabelField($"Playing Clip: {(manager.CurrentClip != null ? manager.CurrentClip.name : "None")}");
                EditorGUILayout.LabelField($"Is Playing: {manager.IsPlaying}");

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Play Main Menu", GUILayout.Height(24)))
                {
                    manager.PlayMainMenuMusic(crossfade: true);
                }
                if (GUILayout.Button("Play Gameplay BG", GUILayout.Height(24)))
                {
                    manager.PlayBackgroundMusic(crossfade: true);
                }
                if (GUILayout.Button("Stop Music", GUILayout.Height(24)))
                {
                    manager.StopMusic(1.0f);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }
    }
}
#endif
