using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace com.ktgame.utils.asset_bookmarker.editor
{
    public enum BookmarkLocation
    {
        Asset = 0,
        SceneObject = 1
    }

    [Serializable]
    public class BookmarkItem
    {
        public string guid = string.Empty;
        public string name = string.Empty;
        public string path = string.Empty;
        public BookmarkLocation location = BookmarkLocation.Asset;
        public string sceneHierarchyPath = string.Empty;
        public string sceneGuid = string.Empty;

        [NonSerialized] private Object _cachedObject;

        public Object GetTargetObject()
        {
            if (_cachedObject != null) return _cachedObject;

            if (location == BookmarkLocation.Asset)
            {
                if (!string.IsNullOrEmpty(guid))
                {
                    string currentPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        path = currentPath;
                        _cachedObject = AssetDatabase.LoadAssetAtPath<Object>(currentPath);
                        if (_cachedObject != null) name = _cachedObject.name;
                        return _cachedObject;
                    }
                }

                if (!string.IsNullOrEmpty(path))
                {
                    _cachedObject = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (_cachedObject != null)
                    {
                        guid = AssetDatabase.AssetPathToGUID(path);
                        name = _cachedObject.name;
                    }
                    return _cachedObject;
                }
            }
            else if (location == BookmarkLocation.SceneObject)
            {
                var go = GameObject.Find(sceneHierarchyPath);
                if (go != null)
                {
                    _cachedObject = go;
                    return _cachedObject;
                }
            }

            return null;
        }

        public bool IsValid()
        {
            if (location == BookmarkLocation.Asset)
            {
                if (!string.IsNullOrEmpty(guid))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    return !string.IsNullOrEmpty(p);
                }
                return !string.IsNullOrEmpty(path) && File.Exists(path);
            }
            return true;
        }

        public void Ping()
        {
            var obj = GetTargetObject();
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
            else
            {
                Debug.LogWarning($"[AssetBookmarker] Cannot find asset: {name} ({path})");
            }
        }

        public void Open()
        {
            var obj = GetTargetObject();
            if (obj == null) return;

            string effectivePath = !string.IsNullOrEmpty(path) ? path : AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(effectivePath) && !string.IsNullOrEmpty(guid))
            {
                effectivePath = AssetDatabase.GUIDToAssetPath(guid);
            }
            if (!string.IsNullOrEmpty(effectivePath))
            {
                path = effectivePath;
            }

            // 1. Folder -> Open inside the folder in Project Browser
            if (AssetDatabase.IsValidFolder(effectivePath))
            {
                OpenFolderInProjectBrowser(obj);
                return;
            }

            // 2. Prefab Asset -> Open directly in Prefab Stage & focus SceneView
            bool isPrefab = (!string.IsNullOrEmpty(effectivePath) && effectivePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                || (obj is GameObject && PrefabUtility.IsPartOfPrefabAsset(obj));

            if (isPrefab)
            {
                if (string.IsNullOrEmpty(effectivePath))
                {
                    effectivePath = AssetDatabase.GetAssetPath(obj);
                }

                if (!string.IsNullOrEmpty(effectivePath))
                {
                    var stage = PrefabStageUtility.OpenPrefab(effectivePath);
                    if (stage != null)
                    {
                        var sceneView = EditorWindow.GetWindow<SceneView>();
                        if (sceneView != null) sceneView.Focus();
                        return;
                    }
                }

                AssetDatabase.OpenAsset(obj);
                return;
            }

            // 3. Scene Asset -> Prompt save and open scene
            if (!string.IsNullOrEmpty(effectivePath) && effectivePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(effectivePath);
                }
                return;
            }

            // 4. Scene Object (GameObject in hierarchy)
            if (location == BookmarkLocation.SceneObject || (obj is GameObject goInScene && !EditorUtility.IsPersistent(goInScene)))
            {
                if (obj is GameObject go)
                {
                    // If it is a prefab instance in the scene, open its prefab stage
                    if (PrefabUtility.IsPartOfAnyPrefab(go))
                    {
                        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                        if (!string.IsNullOrEmpty(prefabPath))
                        {
                            var stage = PrefabStageUtility.OpenPrefab(prefabPath);
                            if (stage != null)
                            {
                                var sceneView = EditorWindow.GetWindow<SceneView>();
                                if (sceneView != null) sceneView.Focus();
                                return;
                            }
                        }
                    }

                    if (SceneView.lastActiveSceneView != null)
                    {
                        SceneView.lastActiveSceneView.LookAt(go.transform.position);
                        SceneView.lastActiveSceneView.Focus();
                    }
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                    return;
                }
            }

            // 5. Default fallback for other assets (scripts, textures, scriptable objects)
            AssetDatabase.OpenAsset(obj);
        }

        public static void OpenFolderInProjectBrowser(Object folderObj)
        {
            if (folderObj == null) return;
            try
            {
                var editorAssembly = typeof(Editor).Assembly;
                var projectBrowserType = editorAssembly.GetType("UnityEditor.ProjectBrowser");
                if (projectBrowserType != null)
                {
                    var projectBrowser = EditorWindow.GetWindow(projectBrowserType);
                    var method = projectBrowserType.GetMethod("ShowFolderContents",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        method.Invoke(projectBrowser, new object[] { folderObj.GetInstanceID(), true });
                        projectBrowser.Focus();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AssetBookmarker] Failed to show folder contents: {ex.Message}");
            }

            // Fallback: ping & select in Project window
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = folderObj;
            EditorGUIUtility.PingObject(folderObj);
        }
    }

    [Serializable]
    public class BookmarkTab
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "New Tab";
        public List<BookmarkItem> items = new List<BookmarkItem>();

        public BookmarkTab Clone()
        {
            var cloned = new BookmarkTab
            {
                id = Guid.NewGuid().ToString("N"),
                name = $"{name} (Copy)",
                items = new List<BookmarkItem>()
            };

            foreach (var item in items)
            {
                cloned.items.Add(new BookmarkItem
                {
                    guid = item.guid,
                    name = item.name,
                    path = item.path,
                    location = item.location,
                    sceneHierarchyPath = item.sceneHierarchyPath,
                    sceneGuid = item.sceneGuid
                });
            }

            return cloned;
        }
    }

    [Serializable]
    public class BookmarkSettings
    {
        public int storageScope = 0;
    }

    [Serializable]
    public class BookmarkRootData
    {
        public int version = 2;
        public List<BookmarkTab> tabs = new List<BookmarkTab>();
        public BookmarkSettings settings = new BookmarkSettings();
    }
}
