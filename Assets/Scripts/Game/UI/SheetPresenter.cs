using System;
using System.Text;
using AgeOfSakura.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Fills the shared <see cref="BottomSheetView"/> for the build menu and for any selected building. The layout depends only on
    /// the building's production state and data (info / production choice / running / ready), never on which building it is.
    /// Everything is built from layout groups; entrance animations play only when the shown content changes.
    /// </summary>
    public sealed class SheetPresenter
    {
        private enum Content
        {
            None,
            BuildMenu,
            Building
        }

        private const float CardWidth = 420f;
        private const float CardHeight = 128f;

        private readonly UiKit kit;
        private readonly BottomSheetView sheet;
        private readonly GameSession session;
        private readonly IAudioService audio;
        private readonly ToastView toast;
        private readonly Action<string> beginPlace;

        private Content content = Content.None;
        private string buildingId;
        private bool showUpgrade;
        private ProductionState builtState;

        // dynamic elements of the "producing" layout
        private TMP_Text countdownText;
        private RectTransform progressFill;
        private TMP_Text skipCostText;
        private UiButton skipButton;
        private int lastCountdownSeconds = -1;

        public bool IsShowingBuildMenu => content == Content.BuildMenu;

        public SheetPresenter(UiKit kit, BottomSheetView sheet, GameSession session, IAudioService audio, ToastView toast, Action<string> beginPlace)
        {
            this.kit = kit;
            this.sheet = sheet;
            this.session = session;
            this.audio = audio;
            this.toast = toast;
            this.beginPlace = beginPlace;
        }

        // ------------------------------------------------------------------ public API

        public void ShowBuildMenu()
        {
            content = Content.BuildMenu;
            buildingId = null;
            Rebuild(true);
            sheet.Show(true);
        }

        public void ShowBuilding(string instanceId)
        {
            content = Content.Building;
            buildingId = instanceId;
            showUpgrade = false;
            Rebuild(true);
            sheet.Show(true);
        }

        /// <summary>Switches the shown building to its upgrade page (used by the UI showcase; the player taps the upgrade tile).</summary>
        public void ShowUpgradePage()
        {
            if (content != Content.Building) return;
            showUpgrade = true;
            Rebuild(true);
        }

        public void Hide()
        {
            content = Content.None;
            buildingId = null;
            sheet.Show(false);
        }

        /// <summary>Rebuilds after a data change (production event, wallet change) if something is showing. No entrance animation.</summary>
        public void Refresh()
        {
            if (content != Content.None) Rebuild(false);
        }

        /// <summary>Cheap once-per-tick update of countdown/progress; rebuilds (with a soft entrance) if the state changed underneath.</summary>
        public void Tick()
        {
            if (content != Content.Building || !session.Buildings.TryGet(buildingId, out var instance)) return;
            if (instance.State != builtState) { Rebuild(true); return; }
            if (instance.State != ProductionState.Producing || countdownText == null) return;

            var remaining = session.Production.GetRemaining(instance);
            int seconds = Mathf.CeilToInt((float)remaining.TotalSeconds);
            if (seconds != lastCountdownSeconds)
            {
                lastCountdownSeconds = seconds;
                countdownText.text = Loc.Countdown(seconds);
            }
            if (progressFill != null) progressFill.anchorMax = new Vector2(Mathf.Max(0.05f, session.Production.GetProgress01(instance)), 1f);

            int cost = session.Production.GetSkipCost(instance);
            if (skipCostText != null) skipCostText.text = cost.ToString();
            if (skipButton != null) skipButton.SetState(session.Wallet.CanAfford(CurrencyType.Diamonds, cost) ? UiButtonState.Normal : UiButtonState.Unaffordable);
        }

        // ------------------------------------------------------------------ shared pieces

        private void Rebuild(bool animate)
        {
            Clear();
            RectTransform body = null;
            if (content == Content.BuildMenu) body = BuildMenu();
            else if (content == Content.Building) body = BuildBuilding();
            // resolve the layout now so nothing flashes at the wrong place for one frame
            sheet.Relayout();
            if (animate && body != null) AnimateIn(body);
        }

        private void Clear()
        {
            var root = sheet.Content;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            countdownText = null;
            progressFill = null;
            skipCostText = null;
            skipButton = null;
            lastCountdownSeconds = -1;
        }

        /// <summary>Tiles and buttons pop in one after another with a soft overshoot.</summary>
        private void AnimateIn(RectTransform body)
        {
            if (kit.Motion == null) return;
            for (int i = 0; i < body.childCount; i++)
            {
                var child = (RectTransform)body.GetChild(i);
                kit.Motion.PopIn(child, UiKit.Fadeable(child.gameObject), 0.06f * i, 0.4f, 0.86f);
            }
        }

        /// <summary>Sets the sheet header and creates the body row below it (content is vertically centred in it).</summary>
        private RectTransform Header(Sprite icon, string title, string subtitle)
        {
            sheet.SetHeader(icon, title, subtitle);
            return kit.Row(sheet.Content, "Body", 20f, TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ building layouts

        private RectTransform BuildBuilding()
        {
            if (!session.Buildings.TryGet(buildingId, out var instance))
            {
                Hide();
                return null;
            }
            session.Production.Refresh(instance);
            var def = session.Buildings.GetDefinition(instance);
            builtState = instance.State;

            string subtitle;
            var production = session.Production.GetActiveProduction(instance);
            switch (instance.State)
            {
                case ProductionState.Producing:
                    subtitle = production != null ? Loc.T("working.detail", Loc.ProductionName(production)) : Loc.T("working");
                    break;
                case ProductionState.ReadyToCollect:
                    subtitle = Loc.T("ready");
                    break;
                default:
                    // a building without jobs shows its description as the page body, so the subtitle stays empty
                    subtitle = def.ProductionIds.Count > 0 ? Loc.T("choose_job") : string.Empty;
                    break;
            }
            // the upgrade page only exists for an idle, upgradable building; anything else falls back to the normal page
            var upgrade = session.Housing.Evaluate(instance);
            if (showUpgrade && (instance.State != ProductionState.Idle || upgrade.IsMaxLevel)) showUpgrade = false;
            if (showUpgrade) subtitle = Loc.T("upgrade.subtitle", upgrade.NextLevel);

            var body = Header(kit.Icons.Has(def.IconId) ? kit.Icons.Get(def.IconId) : null,
                Loc.BuildingName(def) + "  " + Loc.T("level", instance.Level), subtitle);

            switch (instance.State)
            {
                case ProductionState.Idle:
                    if (showUpgrade) UpgradeBody(body, instance, def, upgrade);
                    else if (def.GetProductionIds(instance.Level).Count > 0) ProductionSelection(body, instance, def, upgrade);
                    else InfoBody(body, def);
                    break;
                case ProductionState.Producing:
                    ProductionActive(body, instance, production);
                    break;
                case ProductionState.ReadyToCollect:
                    ReadyToCollect(body, instance, production);
                    break;
            }
            return body;
        }

        private void InfoBody(RectTransform body, BuildingDefinition def)
        {
            var column = kit.Column(body, "Info", 8f, TextAnchor.MiddleLeft);
            UiKit.Size(column.gameObject, flexWidth: 1f);
            var text = kit.Text(column, Loc.BuildingDescription(def), UiTheme.FontBody, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, "Description", wrap: true);
            UiKit.Size(text.gameObject, height: 80f, flexWidth: 1f);
            EffectLines(column, def);
        }

        /// <summary>One line per environment effect the building spreads to its neighbours (Beauty, Faith, Noise).</summary>
        private void EffectLines(Transform parent, BuildingDefinition def)
        {
            foreach (var effect in def.Emits)
            {
                var row = kit.Row(parent, "Effect_" + effect.Variable, 12f, TextAnchor.MiddleLeft);
                UiKit.Size(row.gameObject, height: 46f);
                kit.Picture(row, "Icon", EnvIcon(effect.Variable), new Vector2(40f, 40f));
                var label = kit.Body(row, Loc.Emits(effect), UiTheme.FontBody - 4, "Text");
                UiKit.Size(label.gameObject, height: 44f);
            }
        }

        private string EnvIcon(EnvironmentVariable variable) => variable.ToString().ToLowerInvariant();

        private string NeedIcon(NeedType type) => type == NeedType.Served ? "rice" : type == NeedType.Connected ? "road" : type.ToString().ToLowerInvariant();

        private void ProductionSelection(RectTransform body, BuildingInstance instance, BuildingDefinition def, UpgradeInfo upgrade)
        {
            var ids = def.GetProductionIds(instance.Level);
            foreach (var id in ids) MakeOption(body, instance.InstanceId, session.Definitions.GetProduction(id), ids.Count == 1);
            if (!upgrade.IsMaxLevel) MakeUpgradeTile(body, upgrade);
            else if (def.Levels.Count > 0) MaxLevelNote(body);
        }

        private void MaxLevelNote(RectTransform body)
        {
            var note = kit.Text(body, Loc.T("upgrade.max"), UiTheme.FontBody, Palette.InkSoft, TextAlignmentOptions.Center, FontStyles.Bold, "MaxLevel", wrap: true);
            UiKit.Size(note.gameObject, width: 300f, height: 90f);
        }

        /// <summary>Entry into the upgrade page. The arrow turns green and pulses as soon as every wish is met and it is affordable.</summary>
        private void MakeUpgradeTile(Transform parent, UpgradeInfo upgrade)
        {
            bool ready = upgrade.CanUpgrade;
            var tile = kit.Tile(parent, "UpgradeTile", ready ? "card_light" : "card_locked", new Vector2(380f, 132f), out var button);
            UiKit.AddGroup(tile.gameObject, false, 16f, TextAnchor.MiddleLeft, 22, 10, 22, 10);
            var icon = kit.Picture(tile.transform, "Icon", "upgrade", new Vector2(56f, 56f));
            icon.color = ready ? Palette.UiGreen : new Color(Palette.InkSoft.r, Palette.InkSoft.g, Palette.InkSoft.b, 0.7f);
            var column = kit.Column(tile.transform, "Text", 0f, TextAnchor.MiddleLeft);
            UiKit.Size(column.gameObject, flexWidth: 1f);
            var label = kit.Text(column, Loc.T("upgrade.to", upgrade.NextLevel), UiTheme.FontBody + 2, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Label");
            UiKit.Size(label.gameObject, height: 48f);
            int met = 0;
            foreach (var n in upgrade.Needs) if (n.Met) met++;
            var progress = kit.Text(column, met + " / " + upgrade.Needs.Count, UiTheme.FontBody - 4, ready ? Palette.UiGreenDark : Palette.InkSoft, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Progress");
            UiKit.Size(progress.gameObject, height: 38f);
            if (ready)
            {
                var pulse = tile.gameObject.AddComponent<UiPulse>();
                pulse.Target = tile.rectTransform;
                pulse.Amount = 0.025f;
                pulse.Speed = 2.6f;
            }
            button.onClick.AddListener(() =>
            {
                showUpgrade = true;
                Rebuild(true);
            });
        }

        // ------------------------------------------------------------------ upgrade page

        private void UpgradeBody(RectTransform body, BuildingInstance instance, BuildingDefinition def, UpgradeInfo upgrade)
        {
            var needs = kit.Column(body, "Needs", 4f, TextAnchor.MiddleLeft);
            UiKit.Size(needs.gameObject, flexWidth: 1f);
            bool hinted = false;
            foreach (var need in upgrade.Needs)
            {
                var row = kit.Row(needs, "Need_" + need.Requirement.Type, 14f, TextAnchor.MiddleLeft);
                UiKit.Size(row.gameObject, height: 52f);
                kit.Picture(row, "Icon", NeedIcon(need.Requirement.Type), new Vector2(44f, 44f));
                var label = kit.Text(row, Loc.NeedLabel(need.Requirement, need.Current), UiTheme.FontBody, need.Met ? Palette.Ink : Palette.UiRed,
                    TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Label");
                UiKit.Size(label.gameObject, height: 48f, flexWidth: 1f);
                var mark = kit.Picture(row, "Mark", need.Met ? "check" : "cross", new Vector2(34f, 34f));
                mark.color = need.Met ? Palette.UiGreen : Palette.UiRed;
                if (!need.Met && !hinted)
                {
                    hinted = true;
                    var hint = kit.Text(needs, Loc.NeedHint(need.Requirement.Type), UiTheme.FontCaption, Palette.InkSoft, TextAlignmentOptions.MidlineLeft, FontStyles.Italic, "Hint", wrap: true);
                    UiKit.Size(hint.gameObject, height: 36f, flexWidth: 1f);
                }
            }

            var side = kit.Column(body, "Actions", 10f, TextAnchor.MiddleCenter, fillWidth: true);
            UiKit.Size(side.gameObject, width: 560f);
            var go = kit.Button(side, "Upgrade", Loc.T("upgrade.to", upgrade.NextLevel), new Vector2(560f, 96f), UiButtonStyle.Primary, kit.Icons.Get("upgrade"), UiTheme.FontHeading - 2);
            go.SetState(upgrade.CanUpgrade ? UiButtonState.Normal : UiButtonState.Unaffordable);
            string id = instance.InstanceId;
            go.OnClick(() => DoUpgrade(id));

            // cost on the left, back on the right: one compact row under the main action
            var lower = kit.Row(side, "Lower", 12f, TextAnchor.MiddleLeft);
            UiKit.Size(lower.gameObject, height: 64f);
            var cost = kit.Row(lower, "Cost", 8f, TextAnchor.MiddleLeft);
            UiKit.Size(cost.gameObject, height: 56f, flexWidth: 1f);
            foreach (var c in upgrade.Cost)
            {
                bool enough = session.Wallet.CanAfford(c.Currency, c.Amount);
                kit.PriceChip(cost, kit.Icons.ForCurrency(c.Currency), c.Amount.ToString(), ChipTone.Light, !enough, 52f);
            }
            var back = kit.Button(lower, "Back", Loc.T("back"), new Vector2(190f, 64f), UiButtonStyle.Secondary, null, UiTheme.FontBody - 4);
            back.OnClick(() =>
            {
                showUpgrade = false;
                Rebuild(true);
            });
        }

        private void DoUpgrade(string instanceId)
        {
            var instance = session.Buildings.Get(instanceId);
            var def = session.Buildings.GetDefinition(instance);
            var status = session.Housing.TryUpgrade(instanceId);
            switch (status)
            {
                case UpgradeStatus.Ok:
                    audio.Play(AudioCue.BuildingPlaced);
                    toast.Show(Loc.T("upgrade.done", Loc.BuildingName(def), instance.Level));
                    showUpgrade = false;
                    break;
                case UpgradeStatus.CannotAfford:
                {
                    audio.Play(AudioCue.InvalidAction);
                    var info = session.Housing.Evaluate(instance);
                    foreach (var c in info.Cost)
                    {
                        long have = session.Wallet.GetBalance(c.Currency);
                        if (have < c.Amount) { toast.Show(Loc.T("not_enough", Loc.Currency(c.Currency), have, c.Amount)); break; }
                    }
                    break;
                }
                case UpgradeStatus.NeedsNotMet:
                    audio.Play(AudioCue.InvalidAction);
                    toast.Show(Loc.T("upgrade.needs_unmet"));
                    break;
                default:
                    audio.Play(AudioCue.InvalidAction);
                    toast.Show(Loc.T("upgrade.failed", status));
                    break;
            }
            Refresh();
        }

        private void MakeOption(Transform parent, string instanceId, ProductionDefinition production, bool single = false)
        {
            bool inputsOk = session.Wallet.CanAfford(production.Inputs);
            var tile = kit.Tile(parent, "Option_" + production.Id, inputsOk ? "card_light" : "card_locked", new Vector2(400f, 132f), out var button);
            if (single) UiKit.Size(tile.gameObject, width: 520f); else UiKit.Size(tile.gameObject, flexWidth: 1f);
            UiKit.AddGroup(tile.gameObject, true, 6f, TextAnchor.MiddleCenter, 20, 12, 20, 14);

            var top = kit.Row(tile.transform, "Duration", 10f, TextAnchor.MiddleCenter);
            UiKit.Size(top.gameObject, height: 48f);
            kit.Picture(top, "Clock", "clock", new Vector2(40f, 40f));
            var duration = kit.Text(top, Loc.Duration(production.DurationSeconds), UiTheme.FontHeading, Palette.Ink, TextAlignmentOptions.Midline, FontStyles.Bold, "Duration");
            UiKit.Size(duration.gameObject, height: 48f);

            // supply chain on one line: what the job uses up (red when the store is short), then what it yields
            var chips = kit.Row(tile.transform, "Chips", 10f, TextAnchor.MiddleCenter);
            UiKit.Size(chips.gameObject, height: 52f);
            foreach (var input in production.Inputs)
            {
                bool enough = session.Wallet.CanAfford(input.Currency, input.Amount);
                kit.PriceChip(chips, kit.Icons.ForCurrency(input.Currency), "-" + input.Amount, ChipTone.Card, !enough, 52f);
            }
            foreach (var reward in production.Rewards)
                kit.PriceChip(chips, kit.Icons.ForCurrency(reward.Currency), "+" + reward.Amount, ChipTone.Card, false, 52f);

            string id = production.Id;
            button.onClick.AddListener(() =>
            {
                var status = session.Production.TryStart(instanceId, id);
                if (status == StartProductionStatus.Ok) audio.Play(AudioCue.ProductionStarted);
                else
                {
                    audio.Play(AudioCue.InvalidAction);
                    toast.Show(status == StartProductionStatus.MissingInputs ? MissingInputText(production) : Loc.T("cannot_start", status));
                }
                Refresh();
            });
        }

        private string MissingInputText(ProductionDefinition production)
        {
            foreach (var input in production.Inputs)
            {
                long have = session.Wallet.GetBalance(input.Currency);
                if (have < input.Amount) return Loc.T("input.missing", Loc.Currency(input.Currency), have, input.Amount);
            }
            return Loc.T("cannot_start", StartProductionStatus.MissingInputs);
        }

        private void ProductionActive(RectTransform body, BuildingInstance instance, ProductionDefinition production)
        {
            var remaining = session.Production.GetRemaining(instance);
            int seconds = Mathf.CeilToInt((float)remaining.TotalSeconds);
            lastCountdownSeconds = seconds;

            if (production != null && production.Rewards.Count > 0)
                kit.Picture(body, "RewardIcon", kit.Icons.ForCurrency(production.Rewards[0].Currency), new Vector2(84f, 84f));

            var timer = kit.Column(body, "Timer", 8f, TextAnchor.MiddleLeft);
            UiKit.Size(timer.gameObject, flexWidth: 1f);
            countdownText = kit.Text(timer, Loc.Countdown(seconds), UiTheme.FontTitle + 8, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Countdown");
            UiKit.Size(countdownText.gameObject, height: 66f);

            var trough = kit.Sliced(timer, "Trough", "pill_flat", false);
            trough.color = Palette.CreamLine;
            UiKit.Round(trough, 13f);
            UiKit.Size(trough.gameObject, height: 26f);
            var fill = kit.Sliced(trough.transform, "Fill", "bar_green", false);
            UiKit.Round(fill, 13f);
            progressFill = fill.rectTransform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(Mathf.Max(0.05f, session.Production.GetProgress01(instance)), 1f);
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;

            int cost = session.Production.GetSkipCost(instance);
            skipButton = kit.Button(body, "FinishNow", Loc.T("finish_now"), new Vector2(480f, 100f), UiButtonStyle.Premium, null, UiTheme.FontBody);
            var chip = kit.PriceChip(skipButton.Content, kit.Icons.ForCurrency(CurrencyType.Diamonds), cost.ToString(), ChipTone.Dark, false, 60f);
            skipCostText = chip.Find("Amount").GetComponent<TMP_Text>();
            skipButton.SetState(session.Wallet.CanAfford(CurrencyType.Diamonds, cost) ? UiButtonState.Normal : UiButtonState.Unaffordable);

            string id = instance.InstanceId;
            skipButton.OnClick(() =>
            {
                // Explicit button only: tapping the Diamond icon alone is not a separate target.
                var result = session.Production.TrySkip(id);
                if (result.Status == SkipStatus.InsufficientDiamonds)
                {
                    audio.Play(AudioCue.InvalidAction);
                    toast.Show(Loc.T("not_enough", Loc.Currency(CurrencyType.Diamonds), session.Wallet.GetBalance(CurrencyType.Diamonds), result.Cost));
                }
                Refresh();
            });
        }

        private void ReadyToCollect(RectTransform body, BuildingInstance instance, ProductionDefinition production)
        {
            var sb = new StringBuilder();
            if (production != null)
            {
                foreach (var r in production.Rewards)
                {
                    if (sb.Length > 0) sb.Append("  ");
                    sb.Append('+').Append(r.Amount);
                }
            }

            var message = kit.Text(body, Loc.T("work_done"), UiTheme.FontHeading, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Message", wrap: true);
            UiKit.Size(message.gameObject, height: 90f, flexWidth: 1f);

            var collect = kit.Button(body, "Collect", Loc.T("collect") + " " + sb, new Vector2(520f, 104f), UiButtonStyle.Primary,
                production != null && production.Rewards.Count > 0 ? kit.Icons.ForCurrency(production.Rewards[0].Currency) : null, UiTheme.FontHeading);
            var pulse = collect.GameObject.AddComponent<UiPulse>();
            pulse.Target = collect.Rect;
            pulse.Amount = 0.02f;
            pulse.Speed = 2.6f;
            string id = instance.InstanceId;
            collect.OnClick(() =>
            {
                session.Production.TryCollect(id);
                audio.Play(AudioCue.ProductionCollected);
                Refresh();
            });
        }

        // ------------------------------------------------------------------ build menu

        private RectTransform BuildMenu()
        {
            var body = Header(kit.Icons.Get("hammer"), Loc.T("build"), Loc.T("build.subtitle"));
            var cards = ScrollRow(body);
            foreach (var def in session.Definitions.Buildings)
            {
                if (!def.Buildable) continue;
                MakeCard(cards, def);
            }
            return cards;
        }

        /// <summary>A horizontally scrolling row: the build menu has more cards than fit next to each other.</summary>
        private RectTransform ScrollRow(RectTransform parent)
        {
            var scrollGo = kit.Empty(parent, "Scroll");
            UiKit.Size(scrollGo, height: CardHeight + 8f, flexWidth: 1f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            var viewport = kit.Empty(scrollGo.transform, "Viewport");
            var viewportRect = UiKit.Rect(viewport);
            UiKit.Stretch(viewportRect);
            viewport.AddComponent<RectMask2D>();
            var catcher = viewport.AddComponent<Image>(); // invisible, so a drag that starts between two cards still scrolls
            catcher.color = new Color(1f, 1f, 1f, 0f);

            var content = kit.Row(viewport.transform, "Cards", 18f, TextAnchor.MiddleLeft);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = viewportRect;
            scroll.content = content;
            return content;
        }

        private void MakeCard(Transform parent, BuildingDefinition def)
        {
            bool affordable = session.Wallet.CanAfford(def.BuildCost);
            var card = kit.Tile(parent, "Card_" + def.Id, affordable ? "card_light" : "card_locked", new Vector2(CardWidth, CardHeight), out var button);
            UiKit.AddGroup(card.gameObject, false, 14f, TextAnchor.MiddleLeft, 20, 10, 20, 10);

            var icon = kit.Picture(card.transform, "Icon", kit.Icons.Get(def.IconId), new Vector2(92f, 92f));
            if (!affordable) icon.color = new Color(1f, 1f, 1f, 0.55f);

            var column = kit.Column(card.transform, "Text", 4f, TextAnchor.MiddleLeft);
            UiKit.Size(column.gameObject, flexWidth: 1f);

            var name = kit.Title(column, Loc.BuildingName(def), UiTheme.FontHeading - 2, "Name");
            UiKit.Size(name.gameObject, height: 46f);

            if (affordable)
            {
                var prices = kit.Row(column, "Prices", 8f, TextAnchor.MiddleLeft);
                UiKit.Size(prices.gameObject, height: 48f);
                foreach (var cost in def.BuildCost)
                    kit.PriceChip(prices, kit.Icons.ForCurrency(cost.Currency), cost.Amount.ToString(), ChipTone.Card, false, 48f);
            }
            else
            {
                // never colour alone: an explicit sentence and a padlock say why it is unavailable
                var need = kit.Text(column, NeedText(def), UiTheme.FontCaption, Palette.UiRed, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "NeedMore");
                UiKit.Size(need.gameObject, height: 40f);
                var lockIcon = kit.Picture(card.transform, "Lock", "lock", new Vector2(40f, 40f));
                lockIcon.GetComponent<LayoutElement>().ignoreLayout = true;
                UiKit.Place(lockIcon.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(40f, 40f));
            }

            string id = def.Id;
            button.onClick.AddListener(() => beginPlace(id));
        }

        private string NeedText(BuildingDefinition def)
        {
            foreach (var c in def.BuildCost)
            {
                long have = session.Wallet.GetBalance(c.Currency);
                if (have < c.Amount) return Loc.T("need_more", c.Amount - have, Loc.Currency(c.Currency));
            }
            return string.Empty;
        }
    }
}
