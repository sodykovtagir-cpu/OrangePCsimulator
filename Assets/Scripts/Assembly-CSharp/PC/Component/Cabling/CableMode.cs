using System;
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
            if (game.cablesEnabled == target) return;

            var outgoing = Snapshot(game, loader.Content);
            var incoming = game.otherCableMode ?? Snapshot(game,
                target ? loader.Content : WithoutCables(loader.Content));
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

        private static string WithoutCables(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            // The first legacy branch inherits the room and computers, not
            // powered cable connections. The wired branch is saved unchanged.
            var world = JObject.Parse(content);
            if (world["itemData"] is JArray items)
                foreach (var item in items.ToList())
                {
                    string id = item.Value<string>("spawnId");
                    if (id != null && (id.StartsWith("Power Cable ", StringComparison.Ordinal) ||
                        id.StartsWith("VGA Cable ", StringComparison.Ordinal) ||
                        id.StartsWith("HDMI Cable ", StringComparison.Ordinal)))
                        item.Remove();
                }
            return world.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
