using System.Collections.Generic;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    public enum Language
    {
        English = 0,
        German = 1
    }

    /// <summary>
    /// All player-facing UI text lives here (English + German), so no string is hard-coded in views and further
    /// languages are a new column, not a code change. Follows the device language; data-driven names (buildings,
    /// productions) use a key like "building.woodcutter.name" and fall back to the definition's own text.
    /// A missing UI key is logged as an error and shown as the key itself, so it cannot go unnoticed.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // { key, { English, German } }
            { "build", new[] { "Build", "Bauen" } },
            { "build.subtitle", new[] { "Choose a building, then place it on the land", "Wähle ein Gebäude und platziere es auf dem Land" } },
            { "choose_job", new[] { "Choose a job", "Wähle eine Aufgabe" } },
            { "working", new[] { "Working", "Arbeitet" } },
            { "working.detail", new[] { "Working: {0}", "Arbeitet: {0}" } },
            { "ready", new[] { "Ready to collect!", "Bereit zum Einsammeln!" } },
            { "work_done", new[] { "Your work is done.", "Die Arbeit ist erledigt." } },
            { "collect", new[] { "Collect", "Einsammeln" } },
            { "finish_now", new[] { "Finish now", "Sofort fertig" } },
            { "level", new[] { "Lv.{0}", "St.{0}" } },
            { "sec", new[] { "{0} sec", "{0} Sek." } },
            { "min", new[] { "{0} min", "{0} Min." } },
            { "min.mixed", new[] { "{0}:{1:00} min", "{0}:{1:00} Min." } },
            { "need_more", new[] { "Need {0} more {1}", "Noch {0} {1} nötig" } },
            { "not_enough", new[] { "Not enough {0} ({1}/{2})", "Nicht genug {0} ({1}/{2})" } },
            { "cannot_afford", new[] { "Cannot afford this building", "Das kannst du dir nicht leisten" } },
            { "cannot_start", new[] { "Cannot start: {0}", "Start nicht möglich: {0}" } },
            { "cannot_move", new[] { "{0} cannot be moved.", "{0} kann nicht verschoben werden." } },
            { "move_title", new[] { "Move {0}", "{0} verschieben" } },
            { "move_free", new[] { "Moving is free", "Verschieben ist kostenlos" } },
            { "drag_hint", new[] { "Drag to move, tap the check to place", "Ziehen zum Verschieben, Häkchen zum Bauen" } },
            { "hint.build", new[] { "Build: {0}", "Baue: {0}" } },
            { "place.outside", new[] { "Outside the map", "Außerhalb der Karte" } },
            { "place.locked", new[] { "This land is not unlocked yet", "Dieses Land ist noch gesperrt" } },
            { "place.blocked", new[] { "Blocked by terrain", "Durch Gelände blockiert" } },
            { "place.occupied", new[] { "Space is already taken", "Hier steht schon etwas" } },
            { "save.corrupt", new[] { "The save file was damaged. A new game was started (the old file was kept).", "Der Spielstand war beschädigt. Ein neues Spiel wurde gestartet (die alte Datei bleibt erhalten)." } },
            { "save.too_new", new[] { "This save is from a newer version. Playing a temporary game; the save is untouched.", "Dieser Spielstand stammt aus einer neueren Version. Du spielst ein temporäres Spiel; der Spielstand bleibt unberührt." } },

            { "currency.coins", new[] { "Coins", "Münzen" } },
            { "currency.wood", new[] { "Wood", "Holz" } },
            { "currency.diamonds", new[] { "Diamonds", "Diamanten" } },
            { "currency.rice", new[] { "Rice", "Reis" } },

            { "upgrade", new[] { "Upgrade", "Aufwerten" } },
            { "upgrade.to", new[] { "Upgrade to Lv.{0}", "Aufwerten auf St.{0}" } },
            { "upgrade.title", new[] { "What this house wants", "Das wünscht sich das Haus" } },
            { "upgrade.subtitle", new[] { "Meet every wish, then upgrade to Lv.{0}", "Erfülle alle Wünsche, dann auf St.{0} aufwerten" } },
            { "upgrade.cost", new[] { "Cost", "Kosten" } },
            { "upgrade.done", new[] { "{0} is now level {1}!", "{0} ist jetzt Stufe {1}!" } },
            { "upgrade.needs_unmet", new[] { "Not all wishes are met yet", "Noch nicht alle Wünsche erfüllt" } },
            { "upgrade.max", new[] { "Highest level reached", "Höchste Stufe erreicht" } },
            { "upgrade.failed", new[] { "Cannot upgrade: {0}", "Aufwerten nicht möglich: {0}" } },
            { "back", new[] { "Back", "Zurück" } },
            { "input.missing", new[] { "Not enough {0} ({1}/{2})", "Nicht genug {0} ({1}/{2})" } },
            { "emits.beauty", new[] { "Beauty +{0} within {1} tiles", "Schönheit +{0} im Umkreis von {1} Feldern" } },
            { "emits.faith", new[] { "Faith +{0} within {1} tiles", "Glaube +{0} im Umkreis von {1} Feldern" } },
            { "emits.noise", new[] { "Noise +{0} within {1} tiles", "Lärm +{0} im Umkreis von {1} Feldern" } },

            { "need.served", new[] { "Well supplied: {0} / {1} tax runs", "Gut versorgt: {0} / {1} Steuerläufe" } },
            { "need.beauty", new[] { "Beauty nearby: {0} / {1}", "Schönheit in der Nähe: {0} / {1}" } },
            { "need.faith", new[] { "Faith nearby: {0} / {1}", "Glaube in der Nähe: {0} / {1}" } },
            { "need.noise", new[] { "Quiet: noise {0} (at most {1})", "Ruhe: Lärm {0} (höchstens {1})" } },
            { "need.served.hint", new[] { "Feed the house with Rice and collect its taxes", "Versorge das Haus mit Reis und sammle Steuern" } },
            { "need.beauty.hint", new[] { "Build a Garden or Shrine nearby, or live by cherry trees and water", "Baue Garten oder Schrein in der Nähe, oder wohne bei Kirschbäumen und Wasser" } },
            { "need.faith.hint", new[] { "Build a Shrine nearby", "Baue einen Schrein in der Nähe" } },
            { "need.noise.hint", new[] { "Move the Woodcutter further away", "Rücke den Holzfäller weiter weg" } },

            { "goal.gather", new[] { "Gather {0} {1} for: {2}", "Sammle {0} {1} für: {2}" } },
            { "goal.harvest_rice", new[] { "Harvest Rice at the Rice Paddy", "Ernte Reis auf dem Reisfeld" } },
            { "goal.start_taxes", new[] { "Tap a House to collect taxes", "Tippe ein Haus an: Steuern eintreiben" } },
            { "goal.collect_taxes", new[] { "Tap the House to collect its taxes", "Tippe das Haus an und sammle die Steuern" } },
            { "goal.upgrade", new[] { "A house is ready: tap it, then Upgrade", "Ein Haus ist bereit: antippen, dann Aufwerten" } },
            { "goal.need.served", new[] { "Keep the house supplied ({0}/{1})", "Halte das Haus versorgt ({0}/{1})" } },
            { "goal.need.beauty", new[] { "A house wants more Beauty: build a Garden nearby", "Ein Haus wünscht mehr Schönheit: baue einen Garten in der Nähe" } },
            { "goal.need.faith", new[] { "A house wants Faith: build a Shrine nearby", "Ein Haus wünscht Glaube: baue einen Schrein in der Nähe" } },
            { "goal.need.noise", new[] { "A house finds it too loud: move the Woodcutter", "Einem Haus ist es zu laut: verschiebe den Holzfäller" } },

            { "building.town_hall.name", new[] { "Town Hall", "Rathaus" } },
            { "building.town_hall.description", new[] { "The heart of your settlement.", "Das Herz deiner Siedlung." } },
            { "building.woodcutter.name", new[] { "Woodcutter", "Holzfäller" } },
            { "building.woodcutter.description", new[] { "Fells timber for construction.", "Fällt Holz für den Bau." } },
            { "building.house.name", new[] { "House", "Haus" } },
            { "building.house.description", new[] { "A home. Its people eat rice and pay coins.", "Ein Heim. Die Bewohner essen Reis und zahlen Münzen." } },
            { "building.rice_paddy.name", new[] { "Rice Paddy", "Reisfeld" } },
            { "building.rice_paddy.description", new[] { "Flooded fields that feed your people.", "Geflutete Felder, die deine Leute ernähren." } },
            { "building.garden.name", new[] { "Stone Garden", "Steingarten" } },
            { "building.garden.description", new[] { "A raked garden. Neighbours enjoy the view.", "Ein geharkter Garten. Die Nachbarn genießen den Anblick." } },
            { "building.shrine.name", new[] { "Shrine", "Schrein" } },
            { "building.shrine.description", new[] { "A small shrine. Faith lifts the spirit nearby.", "Ein kleiner Schrein. Der Glaube hebt die Stimmung ringsum." } },

            { "production.wood_small", new[] { "Small Load", "Kleine Ladung" } },
            { "production.wood_medium", new[] { "Normal Load", "Normale Ladung" } },
            { "production.wood_large", new[] { "Large Load", "Große Ladung" } },
            { "production.coins_house_basic", new[] { "Collect Taxes", "Steuern eintreiben" } },
            { "production.coins_house_l2", new[] { "Collect Taxes", "Steuern eintreiben" } },
            { "production.coins_house_l3", new[] { "Collect Taxes", "Steuern eintreiben" } },
            { "production.rice_small", new[] { "Small Harvest", "Kleine Ernte" } },
            { "production.rice_medium", new[] { "Normal Harvest", "Normale Ernte" } },
            { "production.rice_large", new[] { "Large Harvest", "Große Ernte" } },
        };

        public static Language Current { get; set; } = Detect();

        private static Language Detect() => Application.systemLanguage == SystemLanguage.German ? Language.German : Language.English;

        public static string T(string key)
        {
            if (Table.TryGetValue(key, out var values)) return values[(int)Current];
            GameLogUnity.Error($"Missing localisation key '{key}'.");
            return key;
        }

        public static string T(string key, params object[] args) => string.Format(T(key), args);

        private static string Optional(string key, string fallback) =>
            Table.TryGetValue(key, out var values) ? values[(int)Current] : fallback;

        public static string Currency(CurrencyType currency) => T("currency." + currency.ToString().ToLowerInvariant());
        public static string BuildingName(BuildingDefinition def) => Optional($"building.{def.Id}.name", def.DisplayName);
        public static string BuildingDescription(BuildingDefinition def) => Optional($"building.{def.Id}.description", def.Description);
        public static string ProductionName(ProductionDefinition p) => Optional("production." + p.Id, p.DisplayName);

        /// <summary>Numbers of needs and effects: whole numbers plain, fractions with one decimal.</summary>
        public static string Num(float value) => Mathf.Approximately(value, Mathf.Round(value)) ? Mathf.RoundToInt(value).ToString() : value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        public static string NeedLabel(NeedRequirement need, float current)
        {
            string key = "need." + need.Type.ToString().ToLowerInvariant();
            return T(key, Num(current), Num(need.HasMax ? need.Max : need.Min));
        }

        public static string NeedHint(NeedType type) => T("need." + type.ToString().ToLowerInvariant() + ".hint");

        public static string Emits(EnvironmentEffect e) => T("emits." + e.Variable.ToString().ToLowerInvariant(), Num(e.Value), Num(e.Radius));

        public static string GoalText(Goal goal, BuildingDefinition buildingDef)
        {
            switch (goal.Kind)
            {
                case GoalKind.Build: return T("hint.build", BuildingName(buildingDef));
                case GoalKind.Gather: return T("goal.gather", goal.Amount, Currency(goal.Currency), BuildingName(buildingDef));
                case GoalKind.HarvestRice: return T("goal.harvest_rice");
                case GoalKind.StartTaxes: return T("goal.start_taxes");
                case GoalKind.CollectTaxes: return T("goal.collect_taxes");
                case GoalKind.UpgradeHouse: return T("goal.upgrade");
                case GoalKind.ImproveNeed:
                    return goal.Need == NeedType.Served
                        ? T("goal.need.served", Num(goal.Value), Num(goal.Target))
                        : T("goal.need." + goal.Need.ToString().ToLowerInvariant());
                default: return string.Empty;
            }
        }

        public static string Duration(int seconds)
        {
            if (seconds < 60) return T("sec", seconds);
            if (seconds % 60 == 0) return T("min", seconds / 60);
            return T("min.mixed", seconds / 60, seconds % 60);
        }

        public static string Countdown(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int h = seconds / 3600;
            int m = seconds % 3600 / 60;
            int s = seconds % 60;
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
        }
    }
}
