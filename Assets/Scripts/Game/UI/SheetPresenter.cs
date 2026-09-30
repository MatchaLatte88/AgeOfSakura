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
            LayoutRebuilder.ForceRebuildLayoutImmediate(sheet.Content);
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

        /// <summary>Sets the plaque on the sheet and creates the flexible body row (content is vertically centred in it).</summary>
        private RectTransform Header(Sprite icon, string title, string subtitle)
        {
            sheet.SetHeader(icon, title, subtitle);
            var body = kit.Row(sheet.Content, "Body", 28f, TextAnchor.MiddleCenter);
            UiKit.Size(body.gameObject, flexHeight: 1f);
            return body;
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
                    subtitle = def.ProductionIds.Count > 0 ? Loc.T("choose_job") : Loc.BuildingDescription(def);
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
            var column = kit.Column(body, "Info", 10f, TextAnchor.MiddleLeft);
            UiKit.Size(column.gameObject, flexWidth: 1f);
            var text = kit.Text(column, Loc.BuildingDescription(def), TextStyle.Ink, UiTheme.FontHeading, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, "Description", wrap: true);
            UiKit.Size(text.gameObject, height: 90f, flexWidth: 1f);
            EffectLines(column, def);
        }

        /// <summary>One line per environment effect the building spreads to its neighbours (Beauty, Faith, Noise).</summary>
        private void EffectLines(Transform parent, BuildingDefinition def)
        {
            foreach (var effect in def.Emits)
            {
                var row = kit.Row(parent, "Effect_" + effect.Variable, 14f, TextAnchor.MiddleLeft);
                UiKit.Size(row.gameObject, height: 56f);
                kit.Picture(row, "Icon", EnvIcon(effect.Variable), new Vector2(50f, 50f));
                var label = kit.Body(row, Loc.Emits(effect), UiTheme.FontBody, "Text");
                UiKit.Size(label.gameObject, height: 50f);
            }
        }

        private string EnvIcon(EnvironmentVariable variable) => variable.ToString().ToLowerInvariant();

        private string NeedIcon(NeedType type) => type == NeedType.Served ? "rice" : type.ToString().ToLowerInvariant();

        private void ProductionSelection(RectTransform body, BuildingInstance instance, BuildingDefinition def, UpgradeInfo upgrade)
        {
            var ids = def.GetProductionIds(instance.Level);
            foreach (var id in ids) MakeOption(body, instance.InstanceId, session.Definitions.GetProduction(id), ids.Count == 1);
            if (!upgrade.IsMaxLevel) MakeUpgradeTile(body, upgrade);
            else if (def.Levels.Count > 0) MaxLevelNote(body);
        }

        private void MaxLevelNote(RectTransform body)
        {
            var note = kit.Text(body, Loc.T("upgrade.max"), TextStyle.Ink, UiTheme.FontBody, Palette.InkSoft, TextAlignmentOptions.Center, FontStyles.Bold, "MaxLevel", wrap: true);
            UiKit.Size(note.gameObject, width: 300f, height: 120f);
        }

        /// <summary>Entry into the upgrade page. A glow and check appear as soon as every wish is met and it is affordable.</summary>
        private void MakeUpgradeTile(Transform parent, UpgradeInfo upgrade)
        {
            bool ready = upgrade.CanUpgrade;
            var tile = kit.Tile(parent, "UpgradeTile", ready ? "card_paper" : "card_locked", new Vector2(360f, 236f), out var button);
            UiKit.AddGroup(tile.gameObject, true, 4f, TextAnchor.MiddleCenter, 16, 12, 16, 14);
            var icon = kit.Picture(tile.transform, "Icon", "upgrade", new Vector2(96f, 96f));
            icon.color = ready ? new Color(0.36f, 0.62f, 0.28f, 1f) : new Color(0.45f, 0.4f, 0.34f, 0.8f);
            var label = kit.Text(tile.transform, Loc.T("upgrade.to", upgrade.NextLevel), TextStyle.Ink, 40, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold, "Label");
            UiKit.Size(label.gameObject, height: 56f);
            int met = 0;
            foreach (var n in upgrade.Needs) if (n.Met) met++;
            var progress = kit.Text(tile.transform, met + " / " + upgrade.Needs.Count, TextStyle.Ink, 34, ready ? Palette.UiGreenDark : Palette.InkSoft, TextAlignmentOptions.Center, FontStyles.Bold, "Progress");
            UiKit.Size(progress.gameObject, height: 44f);
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
            var needs = kit.Column(body, "Needs", 6f, TextAnchor.MiddleLeft);
            UiKit.Size(needs.gameObject, flexWidth: 1f);
            bool hinted = false;
            foreach (var need in upgrade.Needs)
            {
                var row = kit.Row(needs, "Need_" + need.Requirement.Type, 16f, TextAnchor.MiddleLeft);
                UiKit.Size(row.gameObject, height: 60f);
                kit.Picture(row, "Icon", NeedIcon(need.Requirement.Type), new Vector2(54f, 54f));
                var label = kit.Text(row, Loc.NeedLabel(need.Requirement, need.Current), TextStyle.Ink, UiTheme.FontBody + 2, need.Met ? Palette.Ink : Palette.UiRed,
                    TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Label");
                UiKit.Size(label.gameObject, height: 56f, flexWidth: 1f);
                var mark = kit.Picture(row, "Mark", need.Met ? "check" : "cross", new Vector2(48f, 48f));
                mark.color = need.Met ? Palette.UiGreen : Palette.UiRed;
                if (!need.Met && !hinted)
                {
                    hinted = true;
                    var hint = kit.Text(needs, Loc.NeedHint(need.Requirement.Type), TextStyle.Ink, 28, Palette.InkSoft, TextAlignmentOptions.MidlineLeft, FontStyles.Italic, "Hint", wrap: true);
                    UiKit.Size(hint.gameObject, height: 36f, flexWidth: 1f);
                }
            }

            var side = kit.Column(body, "Actions", 10f, TextAnchor.MiddleCenter, fillWidth: false);
            UiKit.Size(side.gameObject, width: 660f);
            var cost = kit.Row(side, "Cost", 10f, TextAnchor.MiddleCenter);
            UiKit.Size(cost.gameObject, height: 70f);
            foreach (var c in upgrade.Cost)
            {
                bool enough = session.Wallet.CanAfford(c.Currency, c.Amount);
                kit.PriceChip(cost, kit.Icons.ForCurrency(c.Currency), c.Amount.ToString(), enough ? Palette.Cream : new Color(1f, 0.6f, 0.5f), 66f, 42);
            }

            var go = kit.Button(side, "Upgrade", Loc.T("upgrade.to", upgrade.NextLevel), new Vector2(660f, 128f), UiButtonStyle.Primary, kit.Icons.Get("upgrade"), 48);
            go.SetState(upgrade.CanUpgrade ? UiButtonState.Normal : UiButtonState.Unaffordable);
            if (upgrade.CanUpgrade) kit.AddShine(go, 2.6f);
            string id = instance.InstanceId;
            go.OnClick(() => DoUpgrade(id));

            var back = kit.Button(side, "Back", Loc.T("back"), new Vector2(660f, 84f), UiButtonStyle.Secondary, null, 38);
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
            bool hasInputs = production.Inputs.Count > 0;
            bool inputsOk = session.Wallet.CanAfford(production.Inputs);
            var tile = kit.Tile(parent, "Option_" + production.Id, inputsOk ? "card_paper" : "card_locked", new Vector2(400f, hasInputs ? 250f : 196f), out var button);
            if (single) UiKit.Size(tile.gameObject, width: 560f); else UiKit.Size(tile.gameObject, flexWidth: 1f);
            UiKit.AddGroup(tile.gameObject, true, 4f, TextAnchor.MiddleCenter, 20, 12, 20, 16);

            var top = kit.Row(tile.transform, "Duration", 12f, TextAnchor.MiddleCenter);
            UiKit.Size(top.gameObject, height: 70f);
            kit.Picture(top, "Clock", "clock", new Vector2(54f, 54f));
            var duration = kit.Text(top, Loc.Duration(production.DurationSeconds), TextStyle.Ink, 56, Palette.Ink, TextAlignmentOptions.Midline, FontStyles.Bold, "Duration");
            UiKit.Size(duration.gameObject, height: 70f);

            // supply chain: what the job uses up (red when the store is short), then what it yields
            if (hasInputs)
            {
                var inputs = kit.Row(tile.transform, "Inputs", 10f, TextAnchor.MiddleCenter);
                UiKit.Size(inputs.gameObject, height: 58f);
                foreach (var input in production.Inputs)
                {
                    bool enough = session.Wallet.CanAfford(input.Currency, input.Amount);
                    kit.PriceChip(inputs, kit.Icons.ForCurrency(input.Currency), "-" + input.Amount, enough ? Palette.Cream : new Color(1f, 0.6f, 0.5f), 58f, 38);
                }
            }
            var rewards = kit.Row(tile.transform, "Rewards", 10f, TextAnchor.MiddleCenter);
            UiKit.Size(rewards.gameObject, height: 66f);
            foreach (var reward in production.Rewards)
                kit.PriceChip(rewards, kit.Icons.ForCurrency(reward.Currency), "+" + reward.Amount, Palette.Cream, 66f, 42);

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
                kit.Picture(body, "RewardIcon", kit.Icons.ForCurrency(production.Rewards[0].Currency), new Vector2(112f, 112f));

            var timer = kit.Column(body, "Timer", 6f, TextAnchor.MiddleLeft);
            UiKit.Size(timer.gameObject, flexWidth: 1f);
            countdownText = kit.Text(timer, Loc.Countdown(seconds), TextStyle.Ink, UiTheme.FontHero, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Countdown");
            UiKit.Size(countdownText.gameObject, height: 104f);

            var trough = kit.Sliced(timer, "Trough", "trough", false);
            UiKit.Size(trough.gameObject, height: 46f);
            var fill = kit.Sliced(trough.transform, "Fill", "bar_green", false);
            progressFill = fill.rectTransform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(Mathf.Max(0.05f, session.Production.GetProgress01(instance)), 1f);
            progressFill.offsetMin = new Vector2(5f, 5f);
            progressFill.offsetMax = new Vector2(-5f, -5f);

            int cost = session.Production.GetSkipCost(instance);
            skipButton = kit.Button(body, "FinishNow", Loc.T("finish_now"), new Vector2(660f, 160f), UiButtonStyle.Premium, null, 50);
            var chip = kit.PriceChip(skipButton.Content, kit.Icons.ForCurrency(CurrencyType.Diamonds), cost.ToString(), Palette.Cream, 84f, 48);
            skipCostText = chip.Find("Amount").GetComponent<TMP_Text>();
            kit.AddShine(skipButton);
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

            var message = kit.Text(body, Loc.T("work_done"), TextStyle.Ink, 46, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Message", wrap: true);
            UiKit.Size(message.gameObject, height: 110f, flexWidth: 1f);

            var collect = kit.Button(body, "Collect", Loc.T("collect") + " " + sb, new Vector2(720f, 168f), UiButtonStyle.Primary,
                production != null && production.Rewards.Count > 0 ? kit.Icons.ForCurrency(production.Rewards[0].Currency) : null, 56);
            kit.AddShine(collect, 2.6f);
            var pulse = collect.GameObject.AddComponent<UiPulse>();
            pulse.Target = collect.Rect;
            pulse.Amount = 0.025f;
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
            UiKit.Size(scrollGo, flexWidth: 1f, flexHeight: 1f);
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

            var content = kit.Row(viewport.transform, "Cards", 28f, TextAnchor.MiddleLeft);
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
            var card = kit.Tile(parent, "Card_" + def.Id, affordable ? "card_paper" : "card_locked", new Vector2(620f, 196f), out var button);
            UiKit.Size(card.gameObject, flexWidth: 1f);
            UiKit.AddGroup(card.gameObject, false, 18f, TextAnchor.MiddleLeft, 26, 12, 26, 16);

            var icon = kit.Picture(card.transform, "Icon", kit.Icons.Get(def.IconId), new Vector2(148f, 148f));
            if (!affordable) icon.color = new Color(1f, 1f, 1f, 0.55f);

            var column = kit.Column(card.transform, "Text", 6f, TextAnchor.MiddleLeft);
            UiKit.Size(column.gameObject, flexWidth: 1f);

            var name = kit.Title(column, Loc.BuildingName(def), 52, "Name");
            UiKit.Size(name.gameObject, height: 62f);

            if (affordable)
            {
                var prices = kit.Row(column, "Prices", 10f, TextAnchor.MiddleLeft);
                UiKit.Size(prices.gameObject, height: 70f);
                foreach (var cost in def.BuildCost)
                    kit.PriceChip(prices, kit.Icons.ForCurrency(cost.Currency), cost.Amount.ToString(), Palette.Cream, 70f, 44);
            }
            else
            {
                // never colour alone: an explicit sentence and a padlock say why it is unavailable
                var need = kit.Text(column, NeedText(def), TextStyle.Ink, UiTheme.FontCaption + 2, Palette.UiRed, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "NeedMore");
                UiKit.Size(need.gameObject, height: 44f);
                var lockIcon = kit.Picture(card.transform, "Lock", "lock", new Vector2(58f, 58f));
                lockIcon.GetComponent<LayoutElement>().ignoreLayout = true;
                UiKit.Place(lockIcon.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -18f), new Vector2(58f, 58f));
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
