using UnityEngine;

namespace Attrition.Data
{
    /// <summary>
    /// Một dòng hội thoại: tên người nói + nội dung.
    /// Portrait bỏ theo yêu cầu — chỉ giữ text.
    /// </summary>
    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("Tên người nói hiện phía trên. VD: 'Old Sage', '???'. Bỏ trống = giữ tên trước đó.")]
        public string speakerName;

        [TextArea(2, 5)]
        [Tooltip("Nội dung câu thoại (tiếng Anh).")]
        public string text;
    }

    /// <summary>
    /// ScriptableObject chứa chuỗi các dòng hội thoại.
    /// Tạo qua: Create → Attrition → NPC → Dialogue.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/NPC/Dialogue", fileName = "NewDialogue")]
    public class DialogueSO : ScriptableObject
    {
        [Tooltip("Các dòng hội thoại hiện tuần tự (typewriter).")]
        public DialogueLine[] lines = new DialogueLine[0];
    }
}
