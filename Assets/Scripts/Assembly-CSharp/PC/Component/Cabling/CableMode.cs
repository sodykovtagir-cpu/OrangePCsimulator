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
                // Earlier versions could store a blank no-wire branch (zero
                // money, lights off). Recover only that clearly uninitialized
                // state from the intact wired snapshot; never reset a played
                // no-wire branch just because the player loaded it again.
                if (!target && Uninitialized(game, loader.Content) &&
                    game.otherCableMode != null &&
                    !string.IsNullOrEmpty(game.otherCableMode.content))
                {
                    var repaired = WithoutCables(game.otherCableMode);
                    Restore(game, repaired);
                    loader.Content = repaired.content;
                    if (!string.IsNullOrEmpty(loader.Path)) loader.WriteToFile();
                }
                return;
            }

            // Each mode keeps its own complete snapshot. In particular, a
            // manual monitor link saved in the no-wire room remains in the
            // inactive snapshot while physical cables are active. On the very
            // first conversion, take the exact video route from saved cables.
            var outgoing = Snapshot(game, loader.Content);
            var incoming = game.otherCableMode;
            if (incoming == null || (!target && Uninitialized(incoming) &&
                !string.IsNullOrEmpty(loader.Content)))
                incoming = target ? outgoing : WithoutCables(outgoing);
            game.otherCableMode = outgoing;
            Restore(game, incoming);
            game.cablesEnabled = target;
            loader.Content = incoming.content ?? string.Empty;
            // Switching in the menu is a save operation: if the game closes
            // before the player clicks Save, both branches still remain intact.
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

        private static bool Uninitialized(CableModeSnapshot branch)
        {
            return string.IsNullOrEmpty(branch.content) && branch.coin == 0 &&
                branch.playtime == 0f && !branch.light && string.IsNullOrEmpty(branch.icon);
        }

        private static CableModeSnapshot WithoutCables(CableModeSnapshot wired)
        {
            return new CableModeSnapshot
            {
                content = WithoutCables(wired.content),
                coin = wired.coin, playtime = wired.playtime,
                temperature = wired.temperature, ac = wired.ac,
                light = wired.light, icon = wired.icon
            };
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

                foreach (var item in items.ToList())
                {
                    string id = item.Value<string>("spawnId");
                    if (id != null && (id.StartsWith("Power Cable ", StringComparison.Ordinal) ||
                        id.StartsWith("VGA Cable ", StringComparison.Ordinal) ||
                        id.StartsWith("HDMI Cable ", StringComparison.Ordinal)))
                        item.Remove();
                }
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
