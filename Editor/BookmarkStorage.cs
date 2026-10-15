using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace com.ktgame.utils.asset_bookmarker.editor
{
    public static class BookmarkStorage
    {
        private const string LocalSettingsPath = "UserSettings/Ktgame/AssetBookmarker.json";
        private const string SharedSettingsPath = "ProjectSettings/Ktgame/AssetBookmarkerShared.json";
        private const string LegacyEditorPrefsKeySuffix = "AssetsBookmarkSave";

        public static string GetActiveStoragePath(int storageScope)
        {
            return storageScope == 1 ? SharedSettingsPath : LocalSettingsPath;
        }

        public static BookmarkRootData Load(out bool migratedFromLegacy)
        {
            migratedFromLegacy = false;

            // 1. Try to load Local JSON
            if (File.Exists(LocalSettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(LocalSettingsPath);
                    var data = JsonUtility.FromJson<BookmarkRootData>(json);
                    if (data != null && data.tabs != null && data.tabs.Count > 0)
                    {
                        EnsureItemGUIDs(data);
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AssetBookmarker] Failed to load local bookmarks JSON: {ex.Message}");
                }
            }

            // 2. Try to load Shared JSON
            if (File.Exists(SharedSettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(SharedSettingsPath);
                    var data = JsonUtility.FromJson<BookmarkRootData>(json);
                    if (data != null && data.tabs != null && data.tabs.Count > 0)
                    {
                        EnsureItemGUIDs(data);
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AssetBookmarker] Failed to load shared bookmarks JSON: {ex.Message}");
                }
            }

            // 3. Try to migrate from legacy EditorPrefs
            string legacyKey = Application.dataPath + LegacyEditorPrefsKeySuffix;
            if (EditorPrefs.HasKey(legacyKey))
            {
                string legacyJson = EditorPrefs.GetString(legacyKey);
                if (!string.IsNullOrEmpty(legacyJson))
                {
                    try
                    {
                        var root = MigrateFromLegacy(legacyJson);
                        if (root != null && root.tabs.Count > 0)
                        {
                            migratedFromLegacy = true;
                            Save(root);
                            return root;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AssetBookmarker] Failed to migrate legacy bookmarks: {ex.Message}");
                    }
                }
            }

            // 4. Default empty structure
            var defaultRoot = new BookmarkRootData
            {
                version = 2,
                tabs = new List<BookmarkTab>
                {
                    new BookmarkTab
                    {
                        id = Guid.NewGuid().ToString("N"),
                        name = "Common",
                        items = new List<BookmarkItem>()
                    }
                },
                settings = new BookmarkSettings()
            };

            Save(defaultRoot);
            return defaultRoot;
        }

        public static void Save(BookmarkRootData data)
        {
            if (data == null) return;

            string targetPath = GetActiveStoragePath(data.settings.storageScope);

            try
            {
                string dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(targetPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AssetBookmarker] Failed to save bookmarks to {targetPath}: {ex.Message}");
            }
        }

        private static void EnsureItemGUIDs(BookmarkRootData data)
        {
            bool modified = false;
            foreach (var tab in data.tabs)
            {
                foreach (var item in tab.items)
                {
                    if (item.location == BookmarkLocation.Asset)
                    {
                        if (string.IsNullOrEmpty(item.guid) && !string.IsNullOrEmpty(item.path))
                        {
                            item.guid = AssetDatabase.AssetPathToGUID(item.path);
                            modified = true;
                        }
                        else if (!string.IsNullOrEmpty(item.guid))
                        {
                            string resolvedPath = AssetDatabase.GUIDToAssetPath(item.guid);
                            if (!string.IsNullOrEmpty(resolvedPath) && resolvedPath != item.path)
                            {
                                item.path = resolvedPath;
                                modified = true;
                            }
                        }
                    }
                }
            }

            if (modified)
            {
                Save(data);
            }
        }

        #region Legacy Migration

        [Serializable]
        private class LegacyEditorCacheSave
        {
            public List<LegacyCacheTabInfo> tabs = new List<LegacyCacheTabInfo>();
        }

        [Serializable]
        private class LegacyCacheTabInfo
        {
            public int id;
            public string tabName;
            public List<LegacyCacheObjectInfo> list = new List<LegacyCacheObjectInfo>();
            public float iconSize = 20;
            public bool gridView;
        }

        [Serializable]
        private class LegacyCacheObjectInfo
        {
            public string path;
            public string prefabPath;
            public string Name;
            public int location;
        }

        private static BookmarkRootData MigrateFromLegacy(string json)
        {
            var legacy = JsonUtility.FromJson<LegacyEditorCacheSave>(json);
            if (legacy == null || legacy.tabs == null) return null;

            var root = new BookmarkRootData();
            foreach (var legTab in legacy.tabs)
            {
                var tab = new BookmarkTab
                {
                    id = Guid.NewGuid().ToString("N"),
                    name = string.IsNullOrEmpty(legTab.tabName) ? "Tab" : legTab.tabName,
                    items = new List<BookmarkItem>()
                };

                if (legTab.list != null)
                {
                    foreach (var legObj in legTab.list)
                    {
                        string assetPath = !string.IsNullOrEmpty(legObj.path) ? legObj.path : legObj.prefabPath;
                        string guid = !string.IsNullOrEmpty(assetPath) ? AssetDatabase.AssetPathToGUID(assetPath) : string.Empty;

                        tab.items.Add(new BookmarkItem
                        {
                            guid = guid,
                            name = legObj.Name,
                            path = assetPath,
                            location = legObj.location == 1 ? BookmarkLocation.SceneObject : BookmarkLocation.Asset
                        });
                    }
                }

                root.tabs.Add(tab);
            }

            return root;
        }

        #endregion

        #region Import & Export

        public static bool ExportTab(BookmarkTab tab, string destinationPath)
        {
            try
            {
                string json = JsonUtility.ToJson(tab, true);
                File.WriteAllText(destinationPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AssetBookmarker] Failed to export tab: {ex.Message}");
                return false;
            }
        }

        public static BookmarkTab ImportTab(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath)) return null;
                string json = File.ReadAllText(sourcePath);
                var tab = JsonUtility.FromJson<BookmarkTab>(json);
                if (tab != null)
                {
                    tab.id = Guid.NewGuid().ToString("N");
                    return tab;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AssetBookmarker] Failed to import tab: {ex.Message}");
            }
            return null;
        }

        #endregion
    }
}
