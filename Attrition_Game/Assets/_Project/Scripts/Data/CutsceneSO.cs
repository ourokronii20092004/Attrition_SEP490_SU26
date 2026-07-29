using UnityEngine;

namespace Attrition.Data
{
    /// <summary>
    /// Một "nhịp" (beat) của cutscene: lia camera tới đâu, giữ bao lâu, hiện chữ gì, thoại gì.
    /// Các nhịp chạy TUẦN TỰ. Mọi field đều tuỳ chọn — bỏ trống là bỏ qua phần đó.
    /// </summary>
    [System.Serializable]
    public class CutsceneBeat
    {
        [Tooltip("Camera lia tới điểm nào — index trong danh sách 'Focus Points' của CutscenePlayer.\n"
                 + "-1 = giữ nguyên vị trí camera của nhịp trước.")]
        public int focusPointIndex = -1;

        [Tooltip("Sau khi camera tới điểm, giữ thêm bao nhiêu giây cho người chơi kịp thấy.")]
        public float holdSeconds = 1.5f;

        [Tooltip("Chữ hiện giữa màn hình (dùng lại AreaNameBanner). Bỏ trống = không hiện.")]
        public string bannerText;

        [Tooltip("Hội thoại hiện ở nhịp này. Cutscene ĐỢI người chơi đọc hết mới sang nhịp sau.\n"
                 + "Bỏ trống = không có thoại.")]
        public DialogueSO dialogue;
    }

    /// <summary>
    /// Kịch bản cutscene. Tạo qua: Create → Attrition → Cutscene.
    ///
    /// Cố tình KHÔNG dùng Timeline: cutscene ở đây là lia camera + chữ + thoại, tức là ghép lại
    /// những thứ game đã có (CameraFocusController, AreaNameBanner, DialogueUI). Timeline sẽ phải
    /// tự quản vòng đời riêng và không tự đồng bộ qua mạng, nên không đáng thêm.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Cutscene", fileName = "NewCutscene")]
    public class CutsceneSO : ScriptableObject
    {
        [Tooltip("Id DUY NHẤT trong toàn game — dùng để nhớ 'đã xem rồi' trong file save.\n"
                 + "Đổi id = người chơi cũ sẽ xem lại cảnh này. VD: 'intro_map1', 'boss_severed_fang'.")]
        public string cutsceneId;

        [Tooltip("Chỉ chiếu MỘT LẦN cho mỗi lượt chơi (nhớ trong save). Tắt = chiếu lại mỗi lần vào vùng.")]
        public bool playOnce = true;

        [Tooltip("Cho phép bấm ESC để bỏ qua.")]
        public bool skippable = true;

        [Tooltip("Các nhịp của cutscene, chạy lần lượt từ trên xuống.")]
        public CutsceneBeat[] beats = new CutsceneBeat[0];
    }
}
