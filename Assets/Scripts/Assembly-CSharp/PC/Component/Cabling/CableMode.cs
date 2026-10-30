using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaveManagement;
using UnityEngine;

namespace PC.Component.Cabling
{
    // A setting selects one of two save branches, not a destructive conversion.
    // The inactive branch lives inside the same signed .opc file.
    public static class CableMode
    {
        private const string Preference = "UsePhysicalCables";
        public static bool Selected => PlayerPrefs.GetInt(Preference, 1) != 0;
        public static bool Enabled => SaveManager.Loader != null && SaveManager.Loader.GameData != null
            ? SaveManager.Loader.GameData.cableModeVersion == 0 ||
                SaveManager.Loader.GameData.cablesEnabled : Selected;

        public static void SetSelected(bool value)
        {
            PlayerPrefs.SetInt(Preference, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SelectForLoad(DataLoader loader)
        {
            if (loader == null || loader.GameData == null) return;
            var game = loader.GameData;
            if (game.cableModeVersion == 0)
            {
                game.cablesEnabled = true; // old .opc files have no mode field
                game.cableModeVersion = 1;
            }
            bool target = Selected;
            if (game.cablesEnabled == target)
            {
                // Repair only the unmistakably empty no-wire branch created
                // by older versions; don't overwrite a played manual link.
                if (!target && Uninitialized(game, loader.Content) &&
                    game.otherCableMode != null &&
                    !string.IsNullOrEmpty(game.otherCableMode.content))
                {
                    var wired = game.otherCableMode;
                    Restore(game, wired);
                    loader.Content = WithoutCables(wired.content);
                    if (!string.IsNullOrEmpty(loader.Path)) loader.WriteToFile();
                }
                return;
            }

            // Everything except cable and manual video connections follows
            // the CURRENT room in either direction. The inactive snapshot is
            // only a source for its mode's connection records, never for old
            // player/item positions, money, light or computer files.
            var outgoing = Snapshot(game, loader.Content);
            var previous = game.otherCableMode;
            var merged = MergeWorld(loader.Content, previous != null ? previous.content : null, target);
            game.otherCableMode = outgoing;
            game.cablesEnabled = target;
            loader.Content = merged;
            if (!string.IsNullOrEmpty(loader.Path)) loader.WriteToFile();
        }

        private static CableModeSnapshot Snapshot(GameData game, string content)
        {
            return new CableModeSnapshot
            {
                content = content ?? string.Empty,
                coin = game.coin, playtime = game.playtime, temperature = game.temperature,
                ac = game.ac, light = game.light, icon = game.icon
            };
        }

        private static void Restore(GameData game, CableModeSnapshot state)
        {
            game.coin = state.coin;
            game.playtime = state.playtime;
            game.temperature = state.temperature;
            game.ac = state.ac;
            game.light = state.light;
            game.icon = state.icon;
        }

        private static bool Uninitialized(GameData game, string content)
        {
            return string.IsNullOrEmpty(content) && game.coin == 0 &&
                game.playtime == 0f && !game.light && string.IsNullOrEmpty(game.icon);
        }

        private static bool IsCable(JObject item)
        {
            string id = item.Value<string>("spawnId");
            return id != null && (id.StartsWith("Power Cable ", StringComparison.Ordinal) ||
                id.StartsWith("VGA Cable ", StringComparison.Ordinal) ||
                id.StartsWith("HDMI Cable ", StringComparison.Ordinal));
        }

        private static string MergeWorld(string current, string previous, bool wired)
        {
            // A blank active branch has no room to synchronize. Prefer the
            // intact previous branch rather than destroying it on a switch.
            if (string.IsNullOrEmpty(current))
                return wired ? previous ?? string.Empty : WithoutCables(previous);
            if (string.IsNullOrEmpty(previous))
                return wired ? current : WithoutCables(current);

            // Deep-clone all CURRENT positions, inventory, scene/player data,
            // hardware slots and PC files. Copy only the connections belonging
            // to the destination mode from its older snapshot.
            var world = JObject.Parse(wired ? current : WithoutCables(current));
            var old = JObject.Parse(previous);
            var items = world["itemData"] as JArray;
            var oldItems = old["itemData"] as JArray;
            if (items == null || oldItems == null)
                return world.ToString(Newtonsoft.Json.Formatting.None);

            if (wired)
            {
                // The no-wire branch has no cables of its own. Reinsert the
                // exact cable records (including plug sockets, poses and rope
                // segments) kept in the wired snapshot. Do not duplicate IDs
                // that were assigned to new non-cable items in the meantime.
                foreach (var item in items.OfType<JObject>().Where(IsCable).ToList()) item.Remove();
                var ids = new HashSet<int>(items.OfType<JObject>()
                    .Select(item => item.Value<int?>("id") ?? 0));
                int nextId = ids.Count > 0 ? ids.Max() : 0;
                foreach (var cable in oldItems.OfType<JObject>().Where(IsCable))
                {
                    var copy = (JObject)cable.DeepClone();
                    int id = copy.Value<int?>("id") ?? 0;
                    if (id == 0 || ids.Contains(id))
                    {
                        do { nextId++; } while (ids.Contains(nextId));
                        copy["id"] = id = nextId;
                    }
                    ids.Add(id);
                    items.Add(copy);
                }
            }
            else
            {
                // WithoutCables(current) inherits the physical VGA/HDMI link
                // for *new* boards. Existing boards retain their separately
                // saved manual monitor choice, including an explicit 0.
                var byId = items.OfType<JObject>()
                    .Where(item => (item.Value<int?>("id") ?? 0) != 0)
                    .GroupBy(item => item.Value<int>("id"))
                    .ToDictionary(group => group.Key, group => group.First());
                foreach (var oldBoard in oldItems.OfType<JObject>())
                {
                    var manual = BoardData(oldBoard);
                    int boardId = oldBoard.Value<int?>("id") ?? 0;
                    if (manual == null || boardId == 0 ||
                        !byId.TryGetValue(boardId, out var board) ||
                        oldBoard.Value<string>("spawnId") != board.Value<string>("spawnId")) continue;
                    var data = BoardData(board);
                    if (data == null) continue;
                    int monitorId = manual.Value<int?>("monitorId") ?? 0;
                    var oldMonitor = oldItems.OfType<JObject>().FirstOrDefault(item =>
                        item.Value<int?>("id") == monitorId);
                    bool sameMonitor = monitorId != 0 && oldMonitor != null &&
                        byId.TryGetValue(monitorId, out var monitor) &&
                        oldMonitor.Value<string>("spawnId") == monitor.Value<string>("spawnId");
                    data["monitorId"] = sameMonitor ? monitorId : 0;
                }
            }
            return world.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string WithoutCables(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            // Keep the entire room/player/computer payload. Before removing
            // physical cables, convert each saved VGA/HDMI pair into the old
            // explicit motherboard -> monitor link (monitorId). Never guess a
            // nearby PC: the cable's saved owners and GPU's saved slot decide.
            var world = JObject.Parse(content);
            if (world["itemData"] is JArray items)
            {
                var byId = new Dictionary<int, JObject>();
                foreach (var entry in items.OfType<JObject>())
                {
                    int id = entry.Value<int?>("id") ?? 0;
                    if (id != 0 && !byId.ContainsKey(id)) byId.Add(id, entry);
                }

                var links = new Dictionary<int, List<int>>();
                foreach (var cable in items.OfType<JObject>())
                {
                    string spawnId = cable.Value<string>("spawnId");
                    bool vga = spawnId != null &&
                        spawnId.StartsWith("VGA Cable ", StringComparison.Ordinal);
                    bool hdmi = spawnId != null &&
                        spawnId.StartsWith("HDMI Cable ", StringComparison.Ordinal);
                    if (!vga && !hdmi) continue;
                    var data = cable["data"] as JObject;
                    if (data == null) continue;
                    for (int input = 0; input < 2; input++)
                    {
                        string inputName = data.Value<string>("cablePort" + input);
                        if (inputName != (vga ? "VGA input" : "HDMI input")) continue;
                        int monitorId = data.Value<int?>("cableOwner" + input) ?? 0;
                        int output = 1 - input;
                        int ownerId = data.Value<int?>("cableOwner" + output) ?? 0;
                        if (monitorId == 0 || !byId.ContainsKey(monitorId) ||
                            !byId.TryGetValue(ownerId, out var source)) continue;
                        string outputName = data.Value<string>("cablePort" + output);
                        JObject board = null;
                        if (outputName == "Board video") board = source;
                        else if (outputName == "GPU video")
                        {
                            int hostId = source.Value<int?>("hostId") ?? 0;
                            byId.TryGetValue(hostId, out board);
                        }
                        if (BoardData(board) == null) continue;
                        int boardId = board.Value<int>("id");
                        if (!links.TryGetValue(boardId, out var monitors))
                            links.Add(boardId, monitors = new List<int>());
                        if (!monitors.Contains(monitorId)) monitors.Add(monitorId);
                    }
                }
                foreach (var link in links)
                {
                    var board = BoardData(byId[link.Key]);
                    int previous = board.Value<int?>("monitorId") ?? 0;
                    board["monitorId"] = link.Value.Contains(previous) ? previous : link.Value[0];
                }

                foreach (var item in items.OfType<JObject>().Where(IsCable).ToList())
                    item.Remove();
            }
            return world.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static JObject BoardData(JObject item)
        {
            // Only Motherboard.ToData writes monitorId; a loose GPU or case
            // must never be mistaken for the board that owns its video output.
            var data = item?["data"] as JObject;
            return data != null && data.Property("monitorId") != null ? data : null;
        }
    }
}
