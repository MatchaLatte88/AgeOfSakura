using AgeOfSakura.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Bar shown while placing or moving a building: a dark lacquer panel (so it stands apart from the paper sheets) with the
    /// building name, price, a status pill (icon + words, so valid/invalid is never colour-only) and large round-rect
    /// rotate / cancel / confirm buttons. Confirm stays tappable when invalid so the player is told why.
    /// Layout: [ info column (flexible) ][ rotate ][ cancel ][ confirm ].
    /// </summary>
    public sealed class PlacementBarView : MonoBehaviour
    {
        private const float Height = 262f;
        private const float MaxWidth = 1680f;

        private PlacementController placement;
        private UiKit kit;
        private TMP_Text title;
        private TMP_Text message;
        private Image statusIcon;
        private Image statusPill;
        private RectTransform priceRow;
        private UiButton confirm;
        private UiButton rotate;
        private UiPulse confirmPulse;
        private RectTransform rect;
        private RectTransform parentRect;
        private bool wasActive;

        public static PlacementBarView Create(UiKit kit, RectTransform parent, PlacementController placement)
        {
            var panel = kit.Sliced(parent, "PlacementBar", "panel_lacquer", true);
            var view = panel.gameObject.AddComponent<PlacementBarView>();
            view.Build(kit, panel, parent, placement);
            return view;
        }

        private void Build(UiKit kitRef, Image panel, RectTransform parent, PlacementController placementRef)
        {
            kit = kitRef;
            placement = placementRef;
            rect = panel.rectTransform;
            parentRect = parent;
            UiKit.AddShadow(gameObject, 14f, 0.34f);
            UiKit.Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(MaxWidth, Height));

            var row = kit.Row(rect, "Content", 22f, TextAnchor.MiddleLeft, 74, 40, 62, 40);
            UiKit.Stretch(row);

            // ---- info column: title, then price + status on one line
            var info = kit.Column(row, "Info", 8f, TextAnchor.MiddleLeft);
            UiKit.Size(info.gameObject, flexWidth: 1f);

            title = kit.Text(info, string.Empty, TextStyle.Outlined, 58, Palette.Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Title");
            UiKit.Size(title.gameObject, height: 68f);

            var line = kit.Row(info, "Line", 18f, TextAnchor.MiddleLeft);
            UiKit.Size(line.gameObject, height: 72f);
            priceRow = kit.Row(line, "Price", 10f, TextAnchor.MiddleLeft);
            UiKit.Size(priceRow.gameObject, height: 72f);

            statusPill = kit.Sliced(line, "Status", "pill_paper", false);
            UiKit.Size(statusPill.gameObject, height: 68f, flexWidth: 1f);
            UiKit.AddGroup(statusPill.gameObject, false, 12f, TextAnchor.MiddleLeft, 16, 2, 26, 2);
            statusIcon = kit.Picture(statusPill.transform, "StatusIcon", "check", new Vector2(46f, 46f));
            message = kit.Text(statusPill.transform, string.Empty, TextStyle.Ink, 36, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Message");
            UiKit.Size(message.gameObject, height: 60f, flexWidth: 1f);

            // ---- buttons (icons are centred by the button's own layout)
            float side = UiTheme.TouchMin + 12f;
            rotate = kit.Button(row, "Rotate", string.Empty, new Vector2(side, side), UiButtonStyle.Secondary, kit.Icons.Get("rotate"), iconOnly: true);
            rotate.OnClick(() => placement.Rotate());

            var cancel = kit.Button(row, "Cancel", string.Empty, new Vector2(side, side), UiButtonStyle.Danger, kit.Icons.Get("cross"), iconOnly: true);
            cancel.OnClick(() => placement.Cancel());

            confirm = kit.Button(row, "Confirm", string.Empty, new Vector2(side + 100f, side), UiButtonStyle.Primary, kit.Icons.Get("check"), iconOnly: true);
            confirm.OnClick(() => placement.Confirm());
            confirmPulse = confirm.GameObject.AddComponent<UiPulse>();
            confirmPulse.Target = confirm.Rect;
            confirmPulse.Amount = 0.028f;
            confirmPulse.Speed = 3.2f;
            kit.AddShine(confirm, 2.8f);

            placement.StateChanged += Refresh;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // narrower screens (4:3 tablets): never wider than the safe area
            float width = Mathf.Min(MaxWidth, parentRect.rect.width - 24f);
            if (!Mathf.Approximately(rect.sizeDelta.x, width)) rect.sizeDelta = new Vector2(Mathf.Max(900f, width), Height);
        }

        public void Refresh()
        {
            bool active = placement.IsActive;
            gameObject.SetActive(active);
            if (!active)
            {
                wasActive = false;
                return;
            }

            var def = placement.Definition;
            bool moving = placement.Mode == PlacementMode.MoveExisting;
            string name = Loc.BuildingName(def);
            title.text = moving ? Loc.T("move_title", name) : name;
            rotate.GameObject.SetActive(def.Rotatable);

            for (int i = priceRow.childCount - 1; i >= 0; i--)
            {
                var c = priceRow.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
            if (!moving)
            {
                foreach (var cost in def.BuildCost)
                {
                    bool enough = placement.CanAfford;
                    kit.PriceChip(priceRow, kit.Icons.ForCurrency(cost.Currency), cost.Amount.ToString(), enough ? Palette.Cream : new Color(1f, 0.55f, 0.45f), 72f, 44);
                }
            }
            else
            {
                var free = kit.Text(priceRow, Loc.T("move_free"), TextStyle.Light, 36, Palette.Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, "Free");
                UiKit.Size(free.gameObject, height: 60f);
            }

            if (placement.Check != PlacementCheck.Ok) SetStatus(false, PlacementController.Describe(placement.Check));
            else if (!placement.CanAfford) SetStatus(false, placement.NotEnoughMessage(def));
            else SetStatus(true, Loc.T("drag_hint"));

            confirm.SetState(placement.IsValid ? UiButtonState.Normal : UiButtonState.Unaffordable);
            confirmPulse.enabled = placement.IsValid;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            if (!wasActive && kit.Motion != null)
            {
                wasActive = true;
                float restY = 16f;
                kit.Motion.Cancel(rect);
                kit.Motion.Play(0.46f, Ease.OutBackSoft, t => rect.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-Height - 30f, restY, t)), 0f,
                    () => rect.anchoredPosition = new Vector2(0f, restY), rect);
            }
        }

        private void SetStatus(bool ok, string text)
        {
            message.text = text;
            message.color = ok ? Palette.Ink : Palette.UiRed;
            statusIcon.sprite = kit.Icons.Get(ok ? "check" : "cross");
            statusIcon.color = ok ? Palette.UiGreenDark : Palette.UiRed;
        }
    }
}
