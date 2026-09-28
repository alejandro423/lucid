using System;
using System.Collections.Generic;
using UnityEngine;

// Database schema version. Bump when setting ids or defaults change so that
// stale JSON on disk can be detected and migrated instead of misapplied.
namespace Lucid.Settings
{
    /// <summary>
    /// Control type used to render a setting in the settings menu.
    /// </summary>
    public enum LucidSettingType
    {
        Slider,
        Toggle,
        Dropdown
    }

    /// <summary>
    /// A single setting. Holds the default, the range/options metadata and the
    /// current live value. Ids are unique across the whole database.
    /// </summary>
    [Serializable]
    public class LucidSetting
    {
        [Tooltip("Unique id across the database, e.g. 'master'.")]
        public string id = string.Empty;

        [Tooltip("Label shown in the settings menu.")]
        public string displayName = string.Empty;

        public LucidSettingType type = LucidSettingType.Toggle;

        // Slider data.
        public float minValue = 0f;
        public float maxValue = 100f;
        public float defaultFloatValue = 50f;
        public float floatValue = 50f;

        // Toggle data.
        public bool defaultBoolValue = false;
        public bool boolValue = false;

        // Dropdown data.
        public string[] options = Array.Empty<string>();
        public int defaultIntValue = 0;
        public int intValue = 0;

        public void ResetToDefault()
        {
            floatValue = defaultFloatValue;
            boolValue = defaultBoolValue;
            intValue = defaultIntValue;
        }
    }

    /// <summary>
    /// A named group of settings rendered as one sidebar section.
    /// </summary>
    [Serializable]
    public class LucidSettingCategory
    {
        [Tooltip("Stable lookup key, e.g. 'Audio'.")]
        public string categoryId = string.Empty;

        [Tooltip("Header shown in the sidebar.")]
        public string displayName = string.Empty;

        public List<LucidSetting> settings = new List<LucidSetting>();
    }

    /// <summary>
    /// ScriptableObject holding every setting category. The manager reads
    /// categories from here instead of hardcoding them, so new settings only
    /// require editing this asset (or BuildDefault below).
    /// Create via Lucid/SettingsDatabase menu or the Ensure helper.
    /// </summary>
    [CreateAssetMenu(menuName = "Lucid/SettingsDatabase", fileName = "LucidSettingsDatabase")]
    public class LucidSettingsDatabase : ScriptableObject
    {
        public const int CurrentVersion = 1;

        [Tooltip("Schema version, persisted to JSON for migration checks.")]
        public int version = CurrentVersion;

        public List<LucidSettingCategory> categories = new List<LucidSettingCategory>();

        public LucidSettingCategory GetCategory(string categoryId)
        {
            return categories.Find(c => c.categoryId == categoryId);
        }

        public LucidSetting GetSetting(string categoryId, string settingId)
        {
            var category = GetCategory(categoryId);
            return category == null ? null : category.settings.Find(s => s.id == settingId);
        }

        /// <summary>
        /// Canonical default database for Slice 1: Audio / Video / Gameplay.
        /// </summary>
        public static LucidSettingsDatabase BuildDefault()
        {
            var db = CreateInstance<LucidSettingsDatabase>();
            db.version = CurrentVersion;
            db.categories = new List<LucidSettingCategory>
            {
                new LucidSettingCategory
                {
                    categoryId = "Audio",
                    displayName = "Audio",
                    settings = new List<LucidSetting>
                    {
                        new LucidSetting { id = "master", displayName = "Master", type = LucidSettingType.Slider, minValue = 0f, maxValue = 100f, defaultFloatValue = 50f, floatValue = 50f },
                        new LucidSetting { id = "music", displayName = "Music", type = LucidSettingType.Slider, minValue = 0f, maxValue = 100f, defaultFloatValue = 100f, floatValue = 100f },
                        new LucidSetting { id = "sfx", displayName = "SFX", type = LucidSettingType.Slider, minValue = 0f, maxValue = 100f, defaultFloatValue = 100f, floatValue = 100f },
                    }
                },
                new LucidSettingCategory
                {
                    categoryId = "Video",
                    displayName = "Video",
                    settings = new List<LucidSetting>
                    {
                        new LucidSetting { id = "fullscreen", displayName = "Fullscreen", type = LucidSettingType.Toggle, defaultBoolValue = true, boolValue = true },
                        new LucidSetting { id = "vsync", displayName = "VSync", type = LucidSettingType.Toggle, defaultBoolValue = true, boolValue = true },
                        new LucidSetting { id = "quality", displayName = "Quality", type = LucidSettingType.Dropdown, options = new[] { "Low", "Medium", "High" }, defaultIntValue = 0, intValue = 0 },
                    }
                },
                new LucidSettingCategory
                {
                    categoryId = "Gameplay",
                    displayName = "Gameplay",
                    settings = new List<LucidSetting>
                    {
                        new LucidSetting { id = "noflash", displayName = "No Flash", type = LucidSettingType.Toggle, defaultBoolValue = false, boolValue = false },
                    }
                },
            };
            return db;
        }
    }
}
