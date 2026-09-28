using System;
using System.IO;
using UnityEngine;

namespace Lucid.Settings
{
    /// <summary>
    /// Persistent singleton owning the settings database. Loads values from
    /// disk, applies them (AudioListener volume, fullscreen, vsync, quality)
    /// and saves back to JSON. Never hardcodes categories: everything is read
    /// from the ScriptableObject database, falling back to built-in defaults
    /// when the asset is missing.
    /// </summary>
    public class LucidSettingManager : MonoBehaviour
    {
        public const string SettingsFileName = "lucid-settings.json";
        public const string DatabaseAssetPath = "Assets/Settings/LucidSettingsDatabase.asset";

        private static LucidSettingManager _instance;

        public static LucidSettingManager Instance
        {
            get
            {
                // FindFirstObjectByType also works in edit mode, where Awake
                // never runs. This keeps settings UI previews functional.
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<LucidSettingManager>();
                }
                return _instance;
            }
        }

        [Tooltip("Database asset. If empty, built-in defaults are used.")]
        [SerializeField] private LucidSettingsDatabase database;

        public LucidSettingsDatabase Database
        {
            get
            {
                // Lazy built-in defaults: covers edit-mode previews (where
                // Awake never assigns the serialized asset) and missing refs.
                if (database == null)
                {
                    database = LucidSettingsDatabase.BuildDefault();
                }
                return database;
            }
        }

        /// <summary>
        /// Fired whenever a value changes (live preview, load, or reset).
        /// </summary>
        public event Action OnSettingsChanged;

        private string SettingsFilePath
        {
            get { return Path.Combine(Application.persistentDataPath, SettingsFileName); }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (database == null)
            {
                // Asset lives outside Resources so it cannot be loaded by path
                // at runtime; use built-in defaults when not assigned.
                database = LucidSettingsDatabase.BuildDefault();
            }

            Load();
            ApplyAll();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        // ----- Lookup (delegates to the database, no hardcoded categories) -----

        public LucidSetting GetSetting(string categoryId, string settingId)
        {
            return database == null ? null : database.GetSetting(categoryId, settingId);
        }

        public float GetFloat(string categoryId, string settingId, float fallback = 0f)
        {
            var setting = GetSetting(categoryId, settingId);
            return setting == null ? fallback : setting.floatValue;
        }

        public bool GetBool(string categoryId, string settingId, bool fallback = false)
        {
            var setting = GetSetting(categoryId, settingId);
            return setting == null ? fallback : setting.boolValue;
        }

        public int GetInt(string categoryId, string settingId, int fallback = 0)
        {
            var setting = GetSetting(categoryId, settingId);
            return setting == null ? fallback : setting.intValue;
        }

        // ----- Mutation (live apply, caller decides when to Save) -----

        public void SetFloat(string categoryId, string settingId, float value)
        {
            var setting = GetSetting(categoryId, settingId);
            if (setting == null)
            {
                return;
            }

            setting.floatValue = Mathf.Clamp(value, setting.minValue, setting.maxValue);
            ApplyAll();
            NotifyChanged();
        }

        public void SetBool(string categoryId, string settingId, bool value)
        {
            var setting = GetSetting(categoryId, settingId);
            if (setting == null)
            {
                return;
            }

            setting.boolValue = value;
            ApplyAll();
            NotifyChanged();
        }

        public void SetInt(string categoryId, string settingId, int value)
        {
            var setting = GetSetting(categoryId, settingId);
            if (setting == null)
            {
                return;
            }

            int max = setting.options != null && setting.options.Length > 0 ? setting.options.Length - 1 : value;
            setting.intValue = Mathf.Clamp(value, 0, Math.Max(0, max));
            ApplyAll();
            NotifyChanged();
        }

        // ----- Apply -----

        /// <summary>
        /// Applies every known setting to the engine. Unknown future settings
        /// are ignored here, which keeps this method stable as categories grow.
        /// </summary>
        public void ApplyAll()
        {
            if (database == null)
            {
                return;
            }

            AudioListener.volume = Mathf.Clamp01(GetFloat("Audio", "master", 50f) / 100f);

            Screen.fullScreen = GetBool("Video", "fullscreen", true);
            QualitySettings.vSyncCount = GetBool("Video", "vsync", true) ? 1 : 0;

            int quality = GetInt("Video", "quality", 0);
            if (quality >= 0 && quality < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        // ----- Persistence (JsonUtility, versioned) -----

        [Serializable]
        private class SavedDatabase
        {
            public int version;
            public System.Collections.Generic.List<LucidSettingCategory> categories;
        }

        public void Save()
        {
            if (database == null)
            {
                return;
            }

            try
            {
                var saved = new SavedDatabase { version = database.version, categories = database.categories };
                File.WriteAllText(SettingsFilePath, JsonUtility.ToJson(saved, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LucidSettings] Save failed: " + e.Message);
            }
        }

        public void Load()
        {
            if (database == null)
            {
                return;
            }

            string path = SettingsFilePath;
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var saved = JsonUtility.FromJson<SavedDatabase>(File.ReadAllText(path));
                if (saved == null || saved.categories == null)
                {
                    return;
                }

                if (saved.version != LucidSettingsDatabase.CurrentVersion)
                {
                    Debug.Log("[LucidSettings] Settings version mismatch (disk=" + saved.version
                        + ", code=" + LucidSettingsDatabase.CurrentVersion + "). Merging known ids.");
                }

                foreach (var savedCategory in saved.categories)
                {
                    var category = database.GetCategory(savedCategory.categoryId);
                    if (category == null || savedCategory.settings == null)
                    {
                        continue;
                    }

                    foreach (var savedSetting in savedCategory.settings)
                    {
                        var setting = category.settings.Find(s => s.id == savedSetting.id);
                        if (setting == null)
                        {
                            continue;
                        }

                        setting.floatValue = Mathf.Clamp(savedSetting.floatValue, setting.minValue, setting.maxValue);
                        setting.boolValue = savedSetting.boolValue;
                        if (setting.options != null && setting.options.Length > 0)
                        {
                            setting.intValue = Mathf.Clamp(savedSetting.intValue, 0, setting.options.Length - 1);
                        }
                        else
                        {
                            setting.intValue = savedSetting.intValue;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LucidSettings] Load failed, using defaults: " + e.Message);
            }
        }

        private void NotifyChanged()
        {
            if (OnSettingsChanged != null)
            {
                OnSettingsChanged();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only helper: creates the database asset at the canonical
        /// path when missing. Also exposed as Lucid/Create Settings Database.
        /// </summary>
        public static void EnsureAssetExists()
        {
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<LucidSettingsDatabase>(DatabaseAssetPath);
            if (existing != null)
            {
                Debug.Log("[LucidSettings] Database asset already exists at " + DatabaseAssetPath);
                return;
            }

            var db = LucidSettingsDatabase.BuildDefault();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DatabaseAssetPath));
            UnityEditor.AssetDatabase.CreateAsset(db, DatabaseAssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log("[LucidSettings] Created database asset at " + DatabaseAssetPath
                + " with " + db.categories.Count + " categories.");
        }
#endif
    }
}
