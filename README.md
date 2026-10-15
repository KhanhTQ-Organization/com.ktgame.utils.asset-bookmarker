# Asset Bookmarker

A fast, lightweight, and modern asset & scene bookmarking tool for Unity with **zero external dependencies** (no Odin Inspector required).

Pin frequently used Assets, Prefabs, Scenes, Scripts, and GameObjects into organized tabs with instant access, native drag-and-drop reordering, and GUID-based persistence.

---

## ✨ Key Features

### 1. 🚀 Native & Dependency-Free
- **100% Pure Unity Editor API** (`EditorWindow` + `ReorderableList`).
- Runs out of the box in any Unity project without third-party plugins.
- Fast, responsive, and lightweight.

### 2. ↕️ Drag-to-Reorder List
- Native Unity `:::` drag handles to reorder bookmarks up and down in any tab.
- Order is saved automatically.

### 3. 🎯 Clean, Minimal & Clutter-Free UI
- Left sidebar with Odin-style dark theme and active tab highlight.
- Filter tabs instantly with the top search field.
- **No path column or raw technical GUIDs**: Clean, native `ObjectField` for each item.
- Inline actions on every row:
  - **Open**: Smart 1-click open (opens Scenes with unsaved change check, Prefabs in isolation stage, folders in Project browser, scripts in IDE).
  - **Ping**: Highlights and selects the object in Project or Hierarchy.
  - **`×`**: Instant 1-click remove.

### 4. 🛡️ GUID-Based Persistence (Zero Lost References)
- Persists items using **Unity Asset GUIDs**.
- Renaming assets, moving folders, or reorganizing project directories **never breaks your bookmarks**.
- Automatically loads and saves to `UserSettings/Ktgame/AssetBookmarker.json` (or shared `ProjectSettings/`).
- Seamless migration from legacy v1.0 `EditorPrefs`.

### 5. 🖱️ Quick Add & Drag-and-Drop
- **Drag & Drop**: Drag any file(s) or GameObject(s) from Project or Hierarchy directly into the window.
- **`+ Add Selected Assets`**: 1-click add all currently selected assets in the Project window.
- **Context Menus**: Right-click in Project or Hierarchy > `Add to Assets Bookmarker`.

---

## 🚀 How to Use

1. **Open Window**: In Unity menu bar, select `Ktgame > Assets Bookmarker`.
2. **Add Bookmarks**:
   - Drag & drop any asset or GameObject into the window.
   - Or select assets in Project and click `+ Add Selected Assets`.
   - Or right-click any asset/GameObject > `Add to Assets Bookmarker`.
3. **Reorder Bookmarks**:
   - Grab the `:::` handle on the left of any row and drag up or down.
4. **Manage Tabs**:
   - Click `+ Add Tab` at the bottom of the sidebar.
   - Double-click a tab name to rename it inline.
   - Right-click a tab for context options: *Rename*, *Duplicate*, *Move Up*, *Move Down*, *Clear All Items*, *Delete*.
5. **Bookmark Actions**:
   - Double-click any row to open the asset immediately.
   - Click **Open** to open Scene/Prefab/Asset.
   - Click **Ping** to locate the asset in the Project window.
   - Click **`×`** or right-click > *Remove from Bookmarks* to delete.

---

## 📦 Compatibility
- Unity 2021.3, 2022.3, 2023.x, **Unity 6 (6000.x)**.
