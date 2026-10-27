using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Pathea;
using Pathea.CameraSystemNs;
using Pathea.CustomCursorNs;
using Pathea.ItemSystem;
using Pathea.InputSolutionNs;
using Pathea.ModuleNs;
using Pathea.Missions;
using Pathea.Museum;
using UnityEngine;

[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace PortiaGrabItem
{
    [BepInPlugin("local.portia.grabitems", "Portia Grab Items", "1.0.0")]
    public sealed class GrabItems : BaseUnityPlugin
    {
        private sealed class Entry
        {
            public ItemBaseConfData Data;
            public string Name;
            public string ChineseName;
            public Sprite Icon;
        }

        private readonly List<Entry> items = new List<Entry>();
        private readonly Dictionary<int, string> englishNames = new Dictionary<int, string>();
        private readonly Dictionary<int, string> chineseNames = new Dictionary<int, string>();
        private readonly Dictionary<int, HashSet<int>> recipeIngredients = new Dictionary<int, HashSet<int>>();
        private readonly HashSet<int> taskItems = new HashSet<int>();
        private readonly HashSet<int> taskMaterials = new HashSet<int>();
        private const string CurrentTasks = "Current tasks / 当前任务";
        private static GrabItems active;
        private Harmony harmony;
        private bool open;
        private bool wasVisible;
        private CursorLockMode priorLock;
        private string search = "";
        private string category = "All / 全部";
        private int page;
        private string message = "Left click: +1   Right click: +10 / 左键1件 右键10件";
        private float messageUntil;
        private readonly string[] categories = { "All / 全部", CurrentTasks, "Tools & gear / 工具装备", "Materials / 材料", "Quest / 任务", "Food / 食物", "Seeds & farming / 种植", "Furniture / 家具", "Facilities / 设施", "Books / 图纸", "Museum / 博物馆", "Other / 其他" };

        private void Awake()
        {
            active = this;
            LoadEnglishNames();
            LoadRecipes();
            try
            {
                harmony = new Harmony("local.portia.grabitems.cursor");
                var zero = new HarmonyMethod(typeof(GrabItems).GetMethod("ZeroCameraDelta", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
                harmony.Patch(AccessTools.PropertyGetter(typeof(MouseInputSource), "DeltaH"), zero);
                harmony.Patch(AccessTools.PropertyGetter(typeof(MouseInputSource), "DeltaV"), zero);
                var cursor = new HarmonyMethod(typeof(GrabItems).GetMethod("KeepCursorFree", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
                harmony.Patch(AccessTools.PropertySetter(typeof(CustomCursor), "lockState"), cursor);
                harmony.Patch(AccessTools.PropertySetter(typeof(CustomCursor), "visible"), cursor);
                var blockInput = new HarmonyMethod(typeof(GrabItems).GetMethod("BlockGameInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
                harmony.Patch(AccessTools.Method(typeof(InputSolutionModule), "Update"), blockInput);
            }
            catch (Exception ex) { Logger.LogError("Camera cursor patches: " + ex); }
        }

        private static bool ZeroCameraDelta(ref float __result)
        {
            if (active == null || !active.open) return true;
            __result = 0f;
            return false;
        }

        private static bool KeepCursorFree() { return active == null || !active.open; }

        private static bool BlockGameInput() { return active == null || !active.open; }

        private void LoadEnglishNames()
        {
            string path = Path.Combine(Paths.PluginPath, "PortiaGrabItem.names.tsv");
            if (!File.Exists(path)) { Logger.LogWarning("Item name catalog missing: " + path); return; }
            foreach (string line in File.ReadAllLines(path))
            {
                string[] fields = line.Split('\t');
                int id;
                if (fields.Length < 2 || !Int32.TryParse(fields[0], out id)) continue;
                if (!String.IsNullOrEmpty(fields[1])) englishNames[id] = fields[1].Trim();
                if (fields.Length > 2 && !String.IsNullOrEmpty(fields[2])) chineseNames[id] = fields[2].Trim();
            }
            Logger.LogInfo("Item names loaded: English=" + englishNames.Count + ", Chinese=" + chineseNames.Count);
        }

        private void LoadRecipes()
        {
            string path = Path.Combine(Paths.PluginPath, "PortiaGrabItem.recipes.tsv");
            if (!File.Exists(path)) { Logger.LogWarning("Recipe catalog missing: " + path); return; }
            foreach (string line in File.ReadAllLines(path))
            {
                string[] fields = line.Split('\t');
                int product, ingredient;
                if (fields.Length < 2 || !Int32.TryParse(fields[0], out product) || !Int32.TryParse(fields[1], out ingredient)) continue;
                HashSet<int> inputs;
                if (!recipeIngredients.TryGetValue(product, out inputs))
                {
                    inputs = new HashSet<int>();
                    recipeIngredients.Add(product, inputs);
                }
                inputs.Add(ingredient);
            }
            Logger.LogInfo("One-step recipe products loaded: " + recipeIngredients.Count);
        }

        private void RefreshCurrentTasks()
        {
            try
            {
                var missions = Module<MissionManager>.Self;
                var player = Module<Player>.Self;
                if (missions == null || player == null || player.bag == null)
                {
                    Status("Load a save first / 请先载入存档");
                    return;
                }
                var direct = new HashSet<int>();
                foreach (Mission mission in missions.GetMissionRunning())
                {
                    if (mission == null) continue;
                    var target = mission.curTarget as MissionTargetGetItem;
                    if (target == null || target.generalArgs == null) continue;
                    foreach (IdCount need in target.generalArgs)
                        if (need != null && need.id > 0 && need.count > 0) direct.Add(need.id);
                }
                foreach (OrderMission order in missions.runningOrder)
                    if (order != null && order.itemId > 0 && order.itemCount > 0) direct.Add(order.itemId);
                var ingredients = new HashSet<int>();
                foreach (int product in direct)
                {
                    HashSet<int> inputs;
                    if (recipeIngredients.TryGetValue(product, out inputs)) ingredients.UnionWith(inputs);
                }
                ingredients.ExceptWith(direct);
                taskItems.Clear(); taskItems.UnionWith(direct);
                taskMaterials.Clear(); taskMaterials.UnionWith(ingredients);
                page = 0;
                Status("Tasks / 任务 " + taskItems.Count + "; one-step materials / 一级材料 " + taskMaterials.Count);
                Logger.LogInfo("Task refresh: direct=" + taskItems.Count + ", one-step materials=" + taskMaterials.Count);
            }
            catch (Exception ex) { Logger.LogError("Task refresh failed: " + ex); Status("Task refresh failed / 任务刷新失败; see log / 查看日志"); }
        }

        private void SetOpen(bool value)
        {
            if (open == value) return;
            if (value)
            {
                priorLock = Cursor.lockState;
                wasVisible = Cursor.visible;
                open = true;
                Rebuild();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                open = false;
                Cursor.lockState = priorLock;
                Cursor.visible = wasVisible;
            }
        }

        private void LateUpdate()
        {
            if (open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        private void OnDestroy()
        {
            SetOpen(false);
            if (harmony != null) harmony.UnpatchSelf();
            if (active == this) active = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) SetOpen(!open);
        }

        private void Rebuild()
        {
            items.Clear();
            if (ItemDataMgr.Instance == null) return;
            int omitted = 0;
            ItemDataMgr.Instance.ForeachItemBase(data =>
            {
                if (data == null || data.ID <= 0 || !data.CanPickup) return;
                string name = "";
                string chinese = "";
                englishNames.TryGetValue(data.ID, out name);
                chineseNames.TryGetValue(data.ID, out chinese);
                if (String.IsNullOrEmpty(name) && String.IsNullOrEmpty(chinese))
                {
                    omitted++;
                    return;
                }
                items.Add(new Entry { Data = data, Name = String.IsNullOrEmpty(name) ? chinese : name, ChineseName = chinese });
            });
            items.Sort((a, b) => String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            Logger.LogInfo("Catalog: " + items.Count + " named items; omitted without a name: " + omitted);
        }

        private static bool Matches(ItemBaseConfData d, string c)
        {
            if (c == "All / 全部") return true;
            if (c == "Museum / 博物馆") return ExhibitData.Get(d.ID) != null;
            if (c == "Quest / 任务") return d.ContainsType(ItemType.TaskProp);
            if (c == "Food / 食物") return d.ContainsType(ItemType.Food);
            if (c == "Seeds & farming / 种植") return d.ContainsType(ItemType.Seed) || d.ContainsType(ItemType.FarmItem) || d.ContainsType(ItemType.FarmNormalItem) || d.ContainsType(ItemType.FarmFloor);
            if (c == "Facilities / 设施") return d.ContainsType(ItemType.HomeSystemUnit) || d.ContainsType(ItemType.NutrientContainer) || d.ContainsType(ItemType.BoxItem);
            if (c == "Furniture / 家具") return d.ContainsType(ItemType.Home) || d.ContainsType(ItemType.PutDownItem);
            if (c == "Books / 图纸") return d.ContainsType(ItemType.ProductionBook) || d.ContainsType(ItemType.CreationBook);
            if (c == "Materials / 材料") return d.ContainsType(ItemType.Material) || d.ContainsType(ItemType.HomeMaterial) || d.ContainsType(ItemType.Nutrient);
            if (c == "Tools & gear / 工具装备") return d.ContainsType(ItemType.Equipment);
            foreach (string x in new[] { "Quest / 任务", "Food / 食物", "Seeds & farming / 种植", "Facilities / 设施", "Furniture / 家具", "Books / 图纸", "Materials / 材料", "Tools & gear / 工具装备" })
                if (Matches(d, x)) return false;
            return true;
        }

        private void OnGUI()
        {
            if (!open) return;
            float w = Mathf.Min(Screen.width - 24, 900), h = Mathf.Min(Screen.height - 24, 650);
            Rect window = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(window, "Portia Grab Items / 波西亚取物");
            if (GUI.Button(new Rect(window.xMax - 32, window.y + 6, 25, 24), "×")) SetOpen(false);
            float left = window.x + 12, top = window.y + 38;
            for (int i = 0; i < categories.Length; i++)
                if (GUI.Button(new Rect(left, top + i * 31, 165, 27), categories[i])) { category = categories[i]; page = 0; if (category == CurrentTasks) RefreshCurrentTasks(); }
            float gx = left + 175, gridW = w - 198;
            bool taskPage = category == CurrentTasks;
            string next = GUI.TextField(new Rect(gx, top, taskPage ? gridW - 96 : gridW, 27), search);
            if (next != search) { search = next; page = 0; }
            if (taskPage && GUI.Button(new Rect(gx + gridW - 91, top, 91, 27), "Refresh / 刷新")) RefreshCurrentTasks();
            GUI.Label(new Rect(gx, top + 29, gridW, 23), "Left click +1 / Right click +10. Quest items may change story. / 任务物品可能影响剧情");
            if (Time.realtimeSinceStartup < messageUntil) GUI.Label(new Rect(gx, window.yMax - 56, gridW, 23), message);
            List<Entry> filtered = items.Where(e => (taskPage ? taskItems.Contains(e.Data.ID) || taskMaterials.Contains(e.Data.ID) : Matches(e.Data, category)) &&
                (search.Length == 0 || e.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (!String.IsNullOrEmpty(e.ChineseName) && e.ChineseName.Contains(search)) || e.Data.ID.ToString().Contains(search))).ToList();
            int pages = Math.Max(1, (filtered.Count + 19) / 20); page = Math.Min(page, pages - 1);
            float cw = gridW / 5f, ch = (h - 155) / 4f;
            for (int i = 0; i < 20 && page * 20 + i < filtered.Count; i++)
            {
                Entry e = filtered[page * 20 + i]; int col = i % 5, row = i / 5;
                Rect cell = new Rect(gx + col * cw + 2, top + 58 + row * ch, cw - 5, ch - 4);
                GUI.Box(cell, "");
                if (e.Icon == null && !String.IsNullOrEmpty(e.Data.IconPath)) { try { e.Icon = Pathea.UISystemNs.UIUtils.GetSpriteByPath(e.Data.IconPath); } catch { } }
                if (e.Icon != null) { var t = e.Icon.texture; var r = e.Icon.textureRect; GUI.DrawTextureWithTexCoords(new Rect(cell.x + (cell.width - 56) / 2, cell.y + 7, 56, 56), t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height)); }
                string full = (e.Data.ContainsType(ItemType.TaskProp) ? "!" : "") + e.Name;
                if (!String.IsNullOrEmpty(e.ChineseName) && e.ChineseName != e.Name) full += " / " + e.ChineseName;
                if (taskPage) full += taskItems.Contains(e.Data.ID) ? "  [Task / 任务]" : "  [Material / 材料]";
                string englishLabel = e.Name.Length > 17 ? e.Name.Substring(0, 15) + "…" : e.Name;
                string chineseLabel = e.ChineseName;
                if (!String.IsNullOrEmpty(chineseLabel) && chineseLabel.Length > 9) chineseLabel = chineseLabel.Substring(0, 8) + "…";
                string label = englishLabel + (String.IsNullOrEmpty(chineseLabel) || chineseLabel == e.Name ? "" : "\n" + chineseLabel);
                if (taskPage) label = (taskItems.Contains(e.Data.ID) ? "[T] " : "[M] ") + label;
                Color old = GUI.color;
                if (category == "Museum / 博物馆")
                {
                    var museum = Module<MuseumManager>.Self;
                    GUI.color = museum != null && museum.PlayerDonatePermanentExhibit.Contains(e.Data.ID) ? Color.green : Color.white;
                }
                else GUI.color = e.Data.rareLv > 0 ? new Color(0.76f, 0.54f, 1f) : Color.white;
                GUI.Label(new Rect(cell.x + 3, cell.y + 69, cell.width - 6, 38), label);
                GUI.color = old;
                if (cell.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && (Event.current.button == 0 || Event.current.button == 1))
                { Add(e, Event.current.button == 0 ? 1 : 10); Event.current.Use(); }
                if (cell.Contains(Event.current.mousePosition)) GUI.Label(new Rect(gx, window.yMax - 81, gridW, 24), full);
            }
            if (GUI.Button(new Rect(gx, window.yMax - 31, 65, 25), "< Prev")) page = Math.Max(0, page - 1);
            GUI.Label(new Rect(gx + 76, window.yMax - 31, 120, 25), (page + 1) + " / " + pages);
            if (GUI.Button(new Rect(gx + 190, window.yMax - 31, 65, 25), "Next >")) page = Math.Min(pages - 1, page + 1);
            if (window.Contains(Event.current.mousePosition) && Event.current.isMouse && Event.current.type != EventType.Used) Event.current.Use();
        }

        private void Add(Entry e, int count)
        {
            try
            {
                var player = Module<Player>.Self;
                if (player == null || player.bag == null) { Status("Load a save first / 请先载入存档"); return; }
                var bag = player.bag;
                int max = e.Data.StackLimitedNumber;
                if (max < 1) { Status("Invalid stack limit / 无效堆叠上限"); return; }
                int capacity = bag.GetFreeSlotCount() * max;
                foreach (var stack in bag.GetItemObjectsByID(e.Data.ID)) capacity += stack.RemainCapacity();
                if (capacity < count) { Status("Insufficient bag capacity / 背包容量不足; nothing added / 未添加"); return; }
                if (!bag.AddItem(e.Data.ID, count, false)) { Status("Add failed / 添加失败"); return; }
                Status("Added / 已添加 " + count + " × " + e.Name);
            }
            catch (Exception ex) { Logger.LogError(ex); Status("Add failed / 添加失败; see log / 查看日志"); }
        }

        private void Status(string s) { message = s; messageUntil = Time.realtimeSinceStartup + 5f; }
    }
}
