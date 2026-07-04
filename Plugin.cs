using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace BlasAchievementToggler
{
    // Blasphemous achievement toggler.
    //
    // The in-game achievements page (the Achievements tab of Gameplay.UI...ExtrasMenuWidget) shows
    // one row per achievement. Each row's locked/unlocked state is derived, in
    // AchievementsManager.GetAllAchievements(), from the LOCAL achievements list
    // (Framework.Achievements.LocalAchievementsHelper, persisted to local_achievements.data). So
    // that local list is the real switch for what the page displays.
    //
    // This mod overlays an IMGUI panel on the achievements page with a toggle button per
    // achievement. Toggling:
    //   ON  -> LocalAchievementsHelper.SetAchievementUnlocked(id)  (adds to the local list + saves)
    //   OFF -> remove id from LocalAchievementsHelper's cache + SaveLocalAchievements()
    // and sets the in-memory Achievement.Progress / CurrentStatus to match, then rebuilds the page
    // (ExtrasMenuWidget.CreateAchievements) so it updates immediately.
    //
    // Steam-safe by default: it drives the game's LOCAL achievement records (what the in-game page
    // reads), not the Steam API, so it never permanently earns/clears a Steam achievement.
    [BepInPlugin("local.blasphemous.achievementtoggler", "Blasphemous Achievement Toggler", "1.1.0")]
    public class AchievementToggler : BaseUnityPlugin
    {
        const string CoreName        = "Framework.Managers.Core";
        const string AchievementName = "Framework.Achievements.Achievement";
        const string LocalHelperName = "Framework.Achievements.LocalAchievementsHelper";
        const string ExtrasName      = "Gameplay.UI.Others.MenuLogic.ExtrasMenuWidget";

        static ManualLogSource L;

        ConfigEntry<KeyCode> cfgToggleKey;
        ConfigEntry<bool>    cfgAutoShow;

        // Manager / achievement reflection
        PropertyInfo pCoreAchievements;   // static Core.AchievementsManager
        MethodInfo mGetAll;               // AchievementsManager.GetAllAchievements()
        FieldInfo fId, fName, fProgress, fStatus;
        object statusUnlocked, statusLocked;

        // Local achievements store (the page's source of truth)
        MethodInfo mSetUnlocked, mGetLocalIds, mSaveLocal;
        FieldInfo fLocalCache;

        // Extras/achievements page
        Type extrasType;
        PropertyInfo pExtrasActive;
        FieldInfo fCurrentMenu;
        MethodInfo mCreateAchievements;
        object menuAchievement;

        bool ready;
        float gateAccum;
        bool onAchievementsPage;
        bool userHidden;   // hid the panel with the hotkey while on the Achievements page

        // UI state
        List<Row> rows;
        Vector2 scroll;
        string filter = "";
        GUIStyle titleStyle, rowStyle, hintStyle, btnStyle;
        Texture2D panelBg, rowBg, rowBgAlt;

        class Row { public object obj; public string id; public string label; }

        void Awake()
        {
            L = Logger;
            cfgToggleKey = Config.Bind("Keys", "TogglePanel", KeyCode.F8, "Hide / show the panel WHILE the Achievements page is open (does nothing elsewhere, so it won't clash with other mods during gameplay).");
            cfgAutoShow  = Config.Bind("Panel", "ShowOnAchievementsPage", true, "Show the panel while the Achievements page is open.");

            var coreType   = FindType(CoreName);
            var achType    = FindType(AchievementName);
            var localType  = FindType(LocalHelperName);
            extrasType     = FindType(ExtrasName);

            var pub = BindingFlags.Public | BindingFlags.Static;
            var ip  = BindingFlags.Public | BindingFlags.Instance;
            var np  = BindingFlags.NonPublic | BindingFlags.Instance;
            var ns  = BindingFlags.NonPublic | BindingFlags.Static;

            pCoreAchievements = coreType?.GetProperty("AchievementsManager", pub);
            var mgrType = pCoreAchievements?.PropertyType;
            mGetAll = mgrType?.GetMethod("GetAllAchievements", ip, null, Type.EmptyTypes, null);

            if (achType != null)
            {
                fId       = achType.GetField("Id", ip);
                fName     = achType.GetField("Name", ip);
                fProgress = achType.GetField("Progress", ip);
                fStatus   = achType.GetField("CurrentStatus", ip);
                var statusType = achType.GetNestedType("Status", BindingFlags.Public | BindingFlags.NonPublic);
                if (statusType != null)
                {
                    statusUnlocked = SafeGet(() => Enum.Parse(statusType, "UNLOCKED"));
                    statusLocked   = SafeGet(() => Enum.Parse(statusType, "LOCKED"));
                }
            }

            if (localType != null)
            {
                mSetUnlocked = localType.GetMethod("SetAchievementUnlocked", pub, null, new[] { typeof(string) }, null);
                mGetLocalIds = localType.GetMethod("GetLocalAchievementIds", pub, null, Type.EmptyTypes, null);
                mSaveLocal   = localType.GetMethod("SaveLocalAchievements", ns, null, Type.EmptyTypes, null);
                fLocalCache  = localType.GetField("LocalAchievementsCache", ns);
            }

            if (extrasType != null)
            {
                pExtrasActive       = extrasType.GetProperty("currentlyActive", ip);
                fCurrentMenu        = extrasType.GetField("currentMenu", np);
                mCreateAchievements = extrasType.GetMethod("CreateAchievements", np, null, Type.EmptyTypes, null);
                var menuType = extrasType.GetNestedType("MENU", BindingFlags.Public | BindingFlags.NonPublic);
                if (menuType != null) menuAchievement = SafeGet(() => Enum.Parse(menuType, "ACHIEVEMENT"));
            }

            ready = pCoreAchievements != null && mGetAll != null && fId != null && fProgress != null &&
                    mSetUnlocked != null && fLocalCache != null && mSaveLocal != null;
            L.LogInfo($"[AchToggler] v1.1 ready={ready}. extrasPage={extrasType != null && mCreateAchievements != null}. " +
                      $"{cfgToggleKey.Value}=toggle panel.");
            if (!ready) L.LogWarning("[AchToggler] achievement API not fully resolved - toggling may be unavailable.");
        }

        void Update()
        {
            gateAccum += Time.unscaledDeltaTime;
            if (gateAccum >= 0.3f) { gateAccum = 0f; onAchievementsPage = IsOnAchievementsPage(); }

            // The hotkey only hides/shows the panel WHILE the Achievements page is open, so it can't
            // clash with other mods' hotkeys during normal gameplay. Reset when we leave the page.
            if (onAchievementsPage) { if (Input.GetKeyDown(cfgToggleKey.Value)) userHidden = !userHidden; }
            else userHidden = false;

            // Rebuild the list the moment the page opens (so it's current), once.
            if (PanelVisible() && rows == null) BuildRows();
            if (!PanelVisible()) rows = null;
        }

        bool PanelVisible() => onAchievementsPage && cfgAutoShow.Value && !userHidden;

        bool IsOnAchievementsPage()
        {
            if (extrasType == null || pExtrasActive == null || fCurrentMenu == null) return false;
            var ext = SafeGet(() => UnityEngine.Object.FindObjectOfType(extrasType));
            if (ext == null) return false;
            bool active = SafeGet(() => pExtrasActive.GetValue(ext, null)) is bool b && b;
            if (!active) return false;
            object menu = SafeGet(() => fCurrentMenu.GetValue(ext));
            return menuAchievement == null || Equals(menu, menuAchievement);
        }

        // ---- data ---------------------------------------------------------------

        object GetManager() => SafeGet(() => pCoreAchievements?.GetValue(null, null));

        void BuildRows()
        {
            rows = new List<Row>();
            var mgr = GetManager();
            if (mgr == null || mGetAll == null) return;
            var list = SafeGet(() => mGetAll.Invoke(mgr, null)) as IEnumerable;   // also inits the local cache
            if (list == null) return;
            foreach (object ach in list)
            {
                if (ach == null) continue;
                string id = SafeGet(() => fId.GetValue(ach)) as string ?? "?";
                string name = fName == null ? null : SafeGet(() => fName.GetValue(ach)) as string;
                string label = string.IsNullOrEmpty(name) || name == id ? id : $"{name}  ({id})";
                rows.Add(new Row { obj = ach, id = id, label = label });
            }
        }

        bool IsGranted(object ach)
        {
            if (fStatus != null && statusUnlocked != null)
                return Equals(SafeGet(() => fStatus.GetValue(ach)), statusUnlocked);
            return SafeGet(() => fProgress.GetValue(ach)) is float p && p >= 100f;
        }

        void SetGranted(Row row, bool granted)
        {
            // 1) in-memory achievement state
            SafeSet(() => fProgress.SetValue(row.obj, granted ? 100f : 0f));
            if (fStatus != null && statusUnlocked != null && statusLocked != null)
                SafeSet(() => fStatus.SetValue(row.obj, granted ? statusUnlocked : statusLocked));

            // 2) local achievements list (what the page reads + what persists to local_achievements.data)
            if (granted)
            {
                SafeSet(() => mSetUnlocked.Invoke(null, new object[] { row.id }));
            }
            else
            {
                var cache = SafeGet(() => fLocalCache.GetValue(null)) as IList;
                if (cache != null && cache.Contains(row.id))
                {
                    SafeSet(() => cache.Remove(row.id));
                    SafeSet(() => mSaveLocal.Invoke(null, null));
                }
            }

            RefreshNativePage();
            L.LogInfo($"[AchToggler] {row.id} -> {(granted ? "UNLOCKED" : "LOCKED")}.");
        }

        void SetAll(bool granted)
        {
            if (rows == null) return;
            foreach (var row in rows) SetGranted(row, granted);
        }

        void RefreshNativePage()
        {
            if (extrasType == null || mCreateAchievements == null) return;
            var ext = SafeGet(() => UnityEngine.Object.FindObjectOfType(extrasType));
            if (ext != null) SafeSet(() => mCreateAchievements.Invoke(ext, null));
        }

        // ---- UI -----------------------------------------------------------------

        void OnGUI()
        {
            if (!ready || !PanelVisible() || rows == null) return;
            EnsureStyles();
            Cursor.visible = true;   // menus may hide the cursor; we need it to click

            const float w = 520f, x = 40f, y = 40f;
            float h = Mathf.Min(Screen.height - 80f, 620f);
            GUI.DrawTexture(new Rect(x, y, w, h), panelBg);

            GUILayout.BeginArea(new Rect(x + 12f, y + 10f, w - 24f, h - 20f));

            int total = rows.Count, unlocked = 0;
            foreach (var r in rows) if (IsGranted(r.obj)) unlocked++;
            GUILayout.Label($"Achievement Toggler    {unlocked} / {total} unlocked", titleStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Unlock all", btnStyle, GUILayout.Width(90f))) SetAll(true);
            if (GUILayout.Button("Lock all",   btnStyle, GUILayout.Width(90f))) SetAll(false);
            GUILayout.Label("Filter:", hintStyle, GUILayout.Width(44f));
            filter = GUILayout.TextField(filter ?? "", GUILayout.MinWidth(120f));
            if (GUILayout.Button("Close", btnStyle, GUILayout.Width(70f))) userHidden = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            scroll = GUILayout.BeginScrollView(scroll);
            string f = (filter ?? "").Trim().ToLowerInvariant();
            int i = 0;
            foreach (var row in rows)
            {
                if (f.Length > 0 && row.label.ToLowerInvariant().IndexOf(f, StringComparison.Ordinal) < 0) continue;
                bool granted = IsGranted(row.obj);

                GUILayout.BeginHorizontal(i++ % 2 == 0 ? RowStyleBg(rowBg) : RowStyleBg(rowBgAlt));
                rowStyle.normal.textColor = granted ? new Color(0.55f, 0.9f, 0.55f) : new Color(0.82f, 0.82f, 0.85f);
                GUILayout.Label(granted ? "✔" : "•", rowStyle, GUILayout.Width(18f));
                GUILayout.Label(row.label, rowStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(granted ? "Lock" : "Unlock", btnStyle, GUILayout.Width(72f)))
                    SetGranted(row, !granted);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Label($"Toggles the game's local achievement records (the in-game page). Steam is not touched.   [{cfgToggleKey.Value}] hide", hintStyle);
            GUILayout.EndArea();
        }

        GUIStyle RowStyleBg(Texture2D tex)
        {
            var s = new GUIStyle();
            s.normal.background = tex;
            s.padding = new RectOffset(4, 4, 2, 2);
            return s;
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = Color.white;
            rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            rowStyle.normal.textColor = Color.white;
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            hintStyle.normal.textColor = new Color(0.7f, 0.7f, 0.75f);
            btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };

            panelBg = Tex(new Color(0.09f, 0.09f, 0.12f, 0.97f));
            rowBg    = Tex(new Color(1f, 1f, 1f, 0.03f));
            rowBgAlt = Tex(new Color(1f, 1f, 1f, 0.07f));
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }

        // ---- helpers ------------------------------------------------------------

        static object SafeGet(Func<object> f) { try { return f(); } catch { return null; } }
        static void SafeSet(Action a) { try { a(); } catch (Exception e) { L.LogWarning($"[AchToggler] action failed: {e.Message}"); } }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t; try { t = asm.GetType(fullName, false); } catch { continue; }
                if (t != null) return t;
            }
            return null;
        }
    }
}
