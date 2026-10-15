using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.ktgame.utils.asset_bookmarker.editor
{
    public class AssetBookmarker : EditorWindow
    {
        private BookmarkRootData _data;
        private int _selectedTabIndex;
        private string _searchTabQuery = string.Empty;

        private Vector2 _sidebarScrollPos;
        private Vector2 _contentScrollPos;

        private float _sidebarWidth = 150f;
        private bool _isResizingSidebar;
        private Rect _resizerRect;

        private string _renamingTabId;
        private string _renamingTabName = string.Empty;

        private ReorderableList _reorderableList;
        private BookmarkTab _cachedListTab;

        private static Object[] _pendingRegisteredAssets;

        [MenuItem("Ktgame/Assets Bookmarker", false, 5000)]
        public static void Open()
        {
            var window = GetWindow<AssetBookmarker>("Assets Bookmarker");
            window.minSize = new Vector2(450, 260);
            window.Show();
        }

        [MenuItem("Assets/Add to Assets Bookmarker", false, 10001)]
        private static void RegisterAssetSelection()
        {
            _pendingRegisteredAssets = Selection.objects;
            Open();
        }

        [MenuItem("GameObject/Add to Assets Bookmarker", false, 10001)]
        private static void RegisterHierarchySelection()
        {
            _pendingRegisteredAssets = Selection.objects;
            Open();
        }

        private void OnEnable()
        {
            _data = BookmarkStorage.Load(out _);
            if (_data.tabs.Count == 0)
            {
                _data.tabs.Add(new BookmarkTab { name = "Common" });
            }

            _selectedTabIndex = Mathf.Clamp(_selectedTabIndex, 0, _data.tabs.Count - 1);
            UpdateReorderableList();
            ProcessPendingRegistrations();
        }

        private void OnDisable()
        {
            BookmarkStorage.Save(_data);
        }

        private void OnGUI()
        {
            if (_data == null || _data.tabs == null || _data.tabs.Count == 0)
            {
                _data = BookmarkStorage.Load(out _);
                if (_data.tabs.Count == 0) _data.tabs.Add(new BookmarkTab { name = "Common" });
            }

            _selectedTabIndex = Mathf.Clamp(_selectedTabIndex, 0, _data.tabs.Count - 1);
            var currentTab = _data.tabs[_selectedTabIndex];

            ProcessPendingRegistrations();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSidebar();
                DrawSplitter();
                DrawContent(currentTab);
            }

            HandleWindowDragAndDrop(currentTab);
        }

        #region Left Sidebar (Tabs)

        private void DrawSidebar()
        {
            var sidebarRect = EditorGUILayout.BeginVertical(GUILayout.Width(_sidebarWidth));
            EditorGUI.DrawRect(sidebarRect, new Color(0.15f, 0.15f, 0.15f, 1f));

            // Top Search Bar for Tabs
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _searchTabQuery = EditorGUILayout.TextField(_searchTabQuery, EditorStyles.toolbarSearchField);
                if (!string.IsNullOrEmpty(_searchTabQuery) && GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(16)))
                {
                    _searchTabQuery = string.Empty;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space(2);

            _sidebarScrollPos = EditorGUILayout.BeginScrollView(_sidebarScrollPos);

            for (int i = 0; i < _data.tabs.Count; i++)
            {
                var tab = _data.tabs[i];

                if (!string.IsNullOrEmpty(_searchTabQuery) &&
                    tab.name.IndexOf(_searchTabQuery, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                bool isSelected = i == _selectedTabIndex;
                var tabRect = GUILayoutUtility.GetRect(_sidebarWidth - 4, 24);
                bool isHovered = tabRect.Contains(Event.current.mousePosition);

                // Draw background
                if (isSelected)
                {
                    EditorGUI.DrawRect(tabRect, new Color(0.23f, 0.38f, 0.58f, 1f)); // Odin-style selection blue
                }
                else if (isHovered)
                {
                    EditorGUI.DrawRect(tabRect, new Color(0.22f, 0.22f, 0.22f, 1f));
                }

                // Handle inline renaming
                if (_renamingTabId == tab.id)
                {
                    var renameRect = new Rect(tabRect.x + 4, tabRect.y + 2, tabRect.width - 8, 20);
                    GUI.SetNextControlName("RenameTabInput");
                    _renamingTabName = EditorGUI.TextField(renameRect, _renamingTabName);
                    EditorGUI.FocusTextInControl("RenameTabInput");

                    if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
                    {
                        if (!string.IsNullOrEmpty(_renamingTabName)) tab.name = _renamingTabName.Trim();
                        _renamingTabId = null;
                        BookmarkStorage.Save(_data);
                        Event.current.Use();
                    }
                    else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    {
                        _renamingTabId = null;
                        Event.current.Use();
                    }
                }
                else
                {
                    // Label with item count
                    string displayLabel = $"{tab.name} ({tab.items.Count})";
                    var style = isSelected ? EditorStyles.whiteBoldLabel : EditorStyles.label;
                    var labelRect = new Rect(tabRect.x + 8, tabRect.y + 3, tabRect.width - 12, 18);
                    GUI.Label(labelRect, displayLabel, style);

                    // Interactions
                    if (Event.current.type == EventType.ContextClick && tabRect.Contains(Event.current.mousePosition))
                    {
                        ShowTabContextMenu(i);
                        Event.current.Use();
                    }
                    else if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && tabRect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.clickCount == 2)
                        {
                            _renamingTabId = tab.id;
                            _renamingTabName = tab.name;
                        }
                        else
                        {
                            _selectedTabIndex = i;
                            _renamingTabId = null;
                            UpdateReorderableList();
                            GUI.FocusControl(null);
                        }
                        Event.current.Use();
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            // Bottom Add Tab Button
            if (GUILayout.Button("+ Add Tab", EditorStyles.miniButton, GUILayout.Height(22)))
            {
                AddNewTab();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSplitter()
        {
            _resizerRect = GUILayoutUtility.GetRect(4f, Screen.height, GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(_resizerRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && _resizerRect.Contains(Event.current.mousePosition))
            {
                _isResizingSidebar = true;
                Event.current.Use();
            }

            if (_isResizingSidebar)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    _sidebarWidth = Mathf.Clamp(Event.current.mousePosition.x, 110f, 260f);
                    Repaint();
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    _isResizingSidebar = false;
                    Event.current.Use();
                }
            }
        }

        private void AddNewTab()
        {
            _data.tabs.Add(new BookmarkTab
            {
                name = $"Tab {_data.tabs.Count + 1}",
                items = new List<BookmarkItem>()
            });
            _selectedTabIndex = _data.tabs.Count - 1;
            UpdateReorderableList();
            BookmarkStorage.Save(_data);
        }

        private void ShowTabContextMenu(int tabIndex)
        {
            var menu = new GenericMenu();
            var tab = _data.tabs[tabIndex];

            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                _renamingTabId = tab.id;
                _renamingTabName = tab.name;
            });

            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                var copy = tab.Clone();
                _data.tabs.Insert(tabIndex + 1, copy);
                _selectedTabIndex = tabIndex + 1;
                UpdateReorderableList();
                BookmarkStorage.Save(_data);
            });

            if (tabIndex > 0)
            {
                menu.AddItem(new GUIContent("Move Up"), false, () =>
                {
                    var t = _data.tabs[tabIndex];
                    _data.tabs.RemoveAt(tabIndex);
                    _data.tabs.Insert(tabIndex - 1, t);
                    _selectedTabIndex = tabIndex - 1;
                    UpdateReorderableList();
                    BookmarkStorage.Save(_data);
                });
            }
            if (tabIndex < _data.tabs.Count - 1)
            {
                menu.AddItem(new GUIContent("Move Down"), false, () =>
                {
                    var t = _data.tabs[tabIndex];
                    _data.tabs.RemoveAt(tabIndex);
                    _data.tabs.Insert(tabIndex + 1, t);
                    _selectedTabIndex = tabIndex + 1;
                    UpdateReorderableList();
                    BookmarkStorage.Save(_data);
                });
            }

            if (tab.items.Count > 0)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Clear All Items"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Clear Tab", $"Remove all bookmarks in '{tab.name}'?", "Clear", "Cancel"))
                    {
                        tab.items.Clear();
                        BookmarkStorage.Save(_data);
                    }
                });
            }

            menu.AddSeparator("");
            if (_data.tabs.Count > 1)
            {
                menu.AddItem(new GUIContent("Delete"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Delete Tab", $"Delete '{tab.name}'?", "Delete", "Cancel"))
                    {
                        _data.tabs.RemoveAt(tabIndex);
                        _selectedTabIndex = Mathf.Clamp(_selectedTabIndex, 0, _data.tabs.Count - 1);
                        UpdateReorderableList();
                        BookmarkStorage.Save(_data);
                    }
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Delete"));
            }

            menu.ShowAsContext();
        }

        #endregion

        #region Right Panel (Reorderable List)

        private void UpdateReorderableList()
        {
            if (_data == null || _data.tabs.Count == 0) return;
            _selectedTabIndex = Mathf.Clamp(_selectedTabIndex, 0, _data.tabs.Count - 1);
            var tab = _data.tabs[_selectedTabIndex];

            _cachedListTab = tab;

            _reorderableList = new ReorderableList(tab.items, typeof(BookmarkItem), true, false, false, false)
            {
                drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (index < 0 || index >= tab.items.Count) return;
                    var item = tab.items[index];

                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    float buttonsWidth = 118f;
                    var assetRect = new Rect(rect.x, rect.y, rect.width - buttonsWidth - 6, rect.height);

                    var obj = item.GetTargetObject();

                    // Double click on row to open
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.clickCount == 2 && rect.Contains(Event.current.mousePosition))
                    {
                        if (obj != null)
                        {
                            item.Open();
                            Event.current.Use();
                        }
                    }

                    // Right click context menu on row
                    if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
                    {
                        var menu = new GenericMenu();
                        if (obj != null)
                        {
                            menu.AddItem(new GUIContent("Open"), false, () => item.Open());
                            menu.AddItem(new GUIContent("Ping (Highlight)"), false, () => item.Ping());
                            menu.AddSeparator("");
                        }
                        menu.AddItem(new GUIContent("Remove from Bookmarks"), false, () =>
                        {
                            tab.items.RemoveAt(index);
                            BookmarkStorage.Save(_data);
                        });
                        menu.ShowAsContext();
                        Event.current.Use();
                    }

                    // Object Field or Missing label
                    if (obj != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        var newObj = EditorGUI.ObjectField(assetRect, obj, typeof(Object), true);
                        if (EditorGUI.EndChangeCheck() && newObj != null && newObj != obj)
                        {
                            string p = AssetDatabase.GetAssetPath(newObj);
                            if (!string.IsNullOrEmpty(p))
                            {
                                item.guid = AssetDatabase.AssetPathToGUID(p);
                                item.path = p;
                                item.name = newObj.name;
                                item.location = BookmarkLocation.Asset;
                                BookmarkStorage.Save(_data);
                            }
                        }
                    }
                    else
                    {
                        var prevColor = GUI.color;
                        GUI.color = new Color(1f, 0.4f, 0.4f);
                        EditorGUI.LabelField(assetRect, $"[Missing] {item.name}", EditorStyles.boldLabel);
                        GUI.color = prevColor;
                    }

                    // Open Button
                    EditorGUI.BeginDisabledGroup(obj == null);
                    var openRect = new Rect(rect.xMax - buttonsWidth, rect.y, 44, rect.height);
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.25f, 0.65f, 1f);
                    if (GUI.Button(openRect, "Open", EditorStyles.miniButton))
                    {
                        item.Open();
                    }
                    GUI.backgroundColor = prevBg;

                    // Ping Button
                    var pingRect = new Rect(rect.xMax - buttonsWidth + 48, rect.y, 42, rect.height);
                    if (GUI.Button(pingRect, "Ping", EditorStyles.miniButton))
                    {
                        item.Ping();
                    }
                    EditorGUI.EndDisabledGroup();

                    // Remove (X) Button
                    var removeRect = new Rect(rect.xMax - buttonsWidth + 94, rect.y, 24, rect.height);
                    var prevXColor = GUI.color;
                    GUI.color = new Color(1f, 0.45f, 0.45f);
                    if (GUI.Button(removeRect, "×", EditorStyles.miniButton))
                    {
                        tab.items.RemoveAt(index);
                        BookmarkStorage.Save(_data);
                        GUIUtility.ExitGUI();
                    }
                    GUI.color = prevXColor;
                },
                drawNoneElementCallback = (Rect rect) =>
                {
                    GUI.Label(rect, "Drag & drop assets here, or click '+ Add Selected Assets'", EditorStyles.centeredGreyMiniLabel);
                },
                onReorderCallback = (ReorderableList l) =>
                {
                    BookmarkStorage.Save(_data);
                }
            };
        }

        private void DrawContent(BookmarkTab tab)
        {
            EditorGUILayout.BeginVertical();

            // Top Control Bar
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(tab.name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.25f, 0.75f, 0.35f);
                if (GUILayout.Button("+ Add Selected Assets", EditorStyles.toolbarButton))
                {
                    AddSelectedAssets(tab);
                }
                GUI.backgroundColor = prevBg;
            }

            _contentScrollPos = EditorGUILayout.BeginScrollView(_contentScrollPos);

            if (_reorderableList == null || _cachedListTab != tab)
            {
                UpdateReorderableList();
            }

            EditorGUILayout.Space(6);
            _reorderableList.DoLayoutList();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void AddSelectedAssets(BookmarkTab tab)
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Add Assets", "Please select one or more assets in Project window first.", "OK");
                return;
            }

            int addedCount = 0;
            foreach (var obj in selected)
            {
                if (obj == null) continue;
                if (AddObjectToTab(tab, obj))
                {
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                BookmarkStorage.Save(_data);
                ShowNotification(new GUIContent($"Added {addedCount} asset(s)!"));
            }
        }

        #endregion

        #region Drag and Drop Handling

        private void HandleWindowDragAndDrop(BookmarkTab currentTab)
        {
            var e = Event.current;
            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                if (currentTab != null && DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                {
                    int addedCount = 0;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj != null && AddObjectToTab(currentTab, obj))
                        {
                            addedCount++;
                        }
                    }

                    if (addedCount > 0)
                    {
                        BookmarkStorage.Save(_data);
                        ShowNotification(new GUIContent($"Added {addedCount} bookmark(s)!"));
                    }
                }

                e.Use();
            }
        }

        private void ProcessPendingRegistrations()
        {
            if (_pendingRegisteredAssets == null || _pendingRegisteredAssets.Length == 0) return;

            var tab = _data.tabs.Count > 0 ? _data.tabs[_selectedTabIndex] : null;
            if (tab == null) return;

            int addedCount = 0;
            foreach (var obj in _pendingRegisteredAssets)
            {
                if (obj != null && AddObjectToTab(tab, obj))
                {
                    addedCount++;
                }
            }

            _pendingRegisteredAssets = null;

            if (addedCount > 0)
            {
                BookmarkStorage.Save(_data);
                ShowNotification(new GUIContent($"Added {addedCount} asset(s)!"));
            }
        }

        private bool AddObjectToTab(BookmarkTab tab, Object obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);

            if (!string.IsNullOrEmpty(path))
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (tab.items.Any(i => i.guid == guid)) return false;

                tab.items.Add(new BookmarkItem
                {
                    guid = guid,
                    name = obj.name,
                    path = path,
                    location = BookmarkLocation.Asset
                });
                return true;
            }
            else if (obj is GameObject go)
            {
                string hierarchyPath = GetGameObjectHierarchyPath(go.transform);
                if (tab.items.Any(i => i.sceneHierarchyPath == hierarchyPath)) return false;

                tab.items.Add(new BookmarkItem
                {
                    name = go.name,
                    location = BookmarkLocation.SceneObject,
                    sceneHierarchyPath = hierarchyPath,
                    sceneGuid = go.scene.path
                });
                return true;
            }

            return false;
        }

        private string GetGameObjectHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        #endregion
    }
}