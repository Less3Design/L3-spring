using System.IO;
using UnityEditor;
using UnityEngine;

namespace Less3.Springs.Editor
{
    /// <summary>
    /// Project Settings > Less3 > Spring. Shows the settings asset if the project has one,
    /// otherwise the defaults and a button that creates the asset in Assets/Resources.
    /// </summary>
    public static class SpringSettingsProvider
    {
        private const string SETTINGS_PATH = "Project/Less3/Spring";
        private const string ASSET_FOLDER = "Assets/Resources";
        private static string AssetPath => $"{ASSET_FOLDER}/{SpringSettings.RESOURCE_NAME}.asset";

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            var provider = new SettingsProvider(SETTINGS_PATH, SettingsScope.Project)
            {
                label = "Spring",
                keywords = new[] { "spring", "pool", "less3" },
                guiHandler = OnGUI,
            };
            return provider;
        }

        private static void OnGUI(string searchContext)
        {
            SpringSettings settings = SpringSettings.Load();

            EditorGUILayout.Space();
            using (new EditorGUI.IndentLevelScope())
            {
                if (settings == null)
                {
                    EditorGUILayout.HelpBox(
                        $"No settings asset found. The spring pool is using the defaults below.\n" +
                        $"Create the asset to override them for this project. It is stored at {AssetPath}.",
                        MessageType.Info);

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.IntField("Initial Pool Size", SpringManager.DEFAULT_INITIAL_POOL_SIZE);
                        EditorGUILayout.IntField("Max Pool Size", SpringManager.DEFAULT_MAX_POOL_SIZE);
                    }

                    EditorGUILayout.Space();
                    if (GUILayout.Button("Create Settings Asset", GUILayout.Width(180f)))
                    {
                        CreateAsset();
                    }
                    return;
                }

                var so = new SerializedObject(settings);
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty(nameof(SpringSettings.initialPoolSize)));
                EditorGUILayout.PropertyField(so.FindProperty(nameof(SpringSettings.maxPoolSize)));
                so.ApplyModifiedProperties();

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "Changes apply the next time play mode starts. The pool doubles whenever it runs out until it reaches the max.",
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select Asset", GUILayout.Width(120f)))
                    {
                        Selection.activeObject = settings;
                        EditorGUIUtility.PingObject(settings);
                    }
                }
            }
        }

        private static void CreateAsset()
        {
            if (!Directory.Exists(ASSET_FOLDER))
            {
                Directory.CreateDirectory(ASSET_FOLDER);
                AssetDatabase.Refresh();
            }
            var settings = ScriptableObject.CreateInstance<SpringSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
