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

            if (location == BookmarkLocation.Asset)
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".unity")
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(path);
                    }
                    return;
                }

                if (ext == ".prefab")
                {
                    PrefabStageUtility.OpenPrefab(path);
                    return;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    EditorUtility.FocusProjectWindow();
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    return;
                }

                AssetDatabase.OpenAsset(obj);
            }
            else if (location == BookmarkLocation.SceneObject)
            {
                if (obj is GameObject go)
                {
                    if (SceneView.lastActiveSceneView != null)
                    {
                        SceneView.lastActiveSceneView.LookAt(go.transform.position);
                    }
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                }
            }
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
