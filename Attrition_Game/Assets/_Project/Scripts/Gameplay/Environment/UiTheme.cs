using UnityEngine;
using UnityEngine.UI;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// Theme UI dùng chung cho các panel uGUI DỰNG RUNTIME bằng code (WorldMapController,
    /// TutorialPanel, TeamCreditsPanel, AreaNameBanner...).
    ///
    /// VÌ SAO CẦN: các panel này tạo `Image` rồi set màu phẳng, nên không nhận theme từ USS
    /// (USS chỉ áp cho UI Toolkit). Helper này nạp sprite Ornate Retro UI từ `Resources/UI`
    /// (sprite đã có `spriteBorder` sẵn trong .meta) và áp kiểu 9-slice.
    ///
    /// Sprite nằm trong Resources vì code runtime không dùng được AssetDatabase (editor-only).
    /// </summary>
    public static class UiTheme
    {
        // Màu chữ chuẩn của theme BrownFantasy (vàng ngà ấm, đọc rõ trên khung nâu).
        public static readonly Color TextGold = new Color(0.94f, 0.88f, 0.72f);
        public static readonly Color TextDim = new Color(0.72f, 0.68f, 0.58f);

        private const string Dir = "UI/";

        /// <summary>Nạp sprite theme theo tên file trong Resources/UI (không đuôi). Null nếu thiếu.</summary>
        public static Sprite Load(string name)
        {
            var sp = Resources.Load<Sprite>(Dir + name);
            if (sp == null)
                Debug.LogWarning($"[UiTheme] Thiếu sprite Resources/{Dir}{name} — panel sẽ giữ màu phẳng.");
            return sp;
        }

        /// <summary>
        /// Áp KHUNG panel Ornate lên 1 Image có sẵn. Dùng Sliced để 9-slice giãn đúng viền.
        /// Trả false nếu không nạp được sprite (caller cứ giữ màu cũ, không crash).
        /// </summary>
        public static bool ApplyPanel(Image img) => ApplySliced(img, "ornate_panel");

        /// <summary>Khung phụ (nhạt hơn) cho vùng lồng bên trong panel chính.</summary>
        public static bool ApplyPanelAlt(Image img) => ApplySliced(img, "ornate_panel_alt");

        /// <summary>Dải tiêu đề mảnh (tên người nói, header cột...).</summary>
        public static bool ApplyHeader(Image img) => ApplySliced(img, "ornate_header");

        /// <summary>Ô vuông (slot đồ, ô phím).</summary>
        public static bool ApplySlot(Image img) => ApplySliced(img, "ornate_slot");

        /// <summary>Nút thường.</summary>
        public static bool ApplyButton(Image img) => ApplySliced(img, "ornate_button");

        /// <summary>Rãnh thanh (HP/Mana/Stamina/tiến độ).</summary>
        public static bool ApplyBarTrack(Image img) => ApplySliced(img, "ornate_bar_track");

        /// <summary>
        /// Gán 3 sprite trạng thái cho Button (normal/hover/pressed) theo bộ Ornate.
        /// Button phải có Image làm targetGraphic.
        /// </summary>
        public static void ApplyButtonStates(Button btn)
        {
            if (btn == null) return;

            var img = btn.targetGraphic as Image;
            if (img == null) img = btn.GetComponent<Image>();
            if (img == null) return;

            if (!ApplyButton(img)) return;

            var hover = Load("ornate_button_hover");
            var press = Load("ornate_button_press");
            if (hover == null || press == null) return;

            // SpriteSwap cần targetGraphic là Image; gán lại cho chắc.
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.SpriteSwap;
            var state = btn.spriteState;
            state.highlightedSprite = hover;
            state.selectedSprite = hover;
            state.pressedSprite = press;
            btn.spriteState = state;
        }

        /// <summary>Nền dải máu theo loại: "hp" | "mana" | "stamina".</summary>
        public static bool ApplyBarFill(Image img, string kind)
            => ApplySliced(img, "bar_fill_" + kind);

        private static bool ApplySliced(Image img, string spriteName)
        {
            if (img == null) return false;

            var sp = Load(spriteName);
            if (sp == null) return false;

            img.sprite = sp;
            img.type = Image.Type.Sliced;
            // Viền 9-slice của pixel art rất nhỏ (6-24px) → KHÔNG cho Unity co viền khi panel bé,
            // nếu không góc khung bị méo.
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = Color.white;   // sprite đã có màu; tint trắng để giữ nguyên tông gốc
            return true;
        }
    }
}
