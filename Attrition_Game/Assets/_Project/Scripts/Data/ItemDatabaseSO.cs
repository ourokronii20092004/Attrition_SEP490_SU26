using System.Collections.Generic;
using UnityEngine;

namespace Attrition.Data
{
    /// <summary>
    /// Registry tập trung mọi ItemSO trong game. Gán trong Inspector, share giữa Host &amp; Client.
    /// Photon Fusion 2 không truyền được ScriptableObject qua mạng — chỉ truyền được int.
    /// Vì vậy mọi giao tiếp mạng dùng "index trong danh sách này" thay cho object thực tế.
    ///
    /// Sử dụng:
    /// 1. Tạo asset: Create → Attrition → Item Database.
    /// 2. Kéo thả MỌI ItemSO (Equipment, Accessory, Skill, Material) vào danh sách items.
    /// 3. Gán vào GameManager hoặc set Instance trong Awake sớm nhất.
    ///
    /// Lưu ý: THỨ TỰ trong danh sách = index mạng. KHÔNG được xóa/chèn giữa chừng
    /// (sẽ làm lệch mapping save cũ). Chỉ thêm vào cuối.
    /// </summary>
    [CreateAssetMenu(menuName = "Attrition/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<ItemSO> items = new();

        // Runtime lookup — [NonSerialized] để Unity KHÔNG lưu trạng thái cũ giữa các phiên/domain reload.
        // (Dictionary không serialize được; nếu _initialized bị lưu = true mà _byIndex = null → NRE.)
        [System.NonSerialized] private Dictionary<int, ItemSO> _byIndex;
        [System.NonSerialized] private Dictionary<string, int> _byStringId;
        [System.NonSerialized] private bool _initialized;

        /// <summary>Singleton accessor — set bởi GameManager hoặc loader scene sớm nhất.</summary>
        public static ItemDatabaseSO Instance { get; set; }

        /// <summary>Tổng số item đã đăng ký.</summary>
        public int Count => items.Count;

        /// <summary>Build dictionary. Tự gọi lazy nếu chưa init. Gọi sớm để bắt lỗi trùng ID.</summary>
        public void Initialize()
        {
            if (_initialized && _byIndex != null) return;
            _byIndex = new Dictionary<int, ItemSO>(items.Count);
            _byStringId = new Dictionary<string, int>(items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    Debug.LogWarning($"[ItemDatabase] Slot {i} is null — skipped.");
                    continue;
                }
                _byIndex[i] = items[i];

                if (!string.IsNullOrEmpty(items[i].itemId))
                {
                    if (_byStringId.ContainsKey(items[i].itemId))
                        Debug.LogError($"[ItemDatabase] Duplicate itemId '{items[i].itemId}' tại index {i}!");
                    else
                        _byStringId[items[i].itemId] = i;
                }
            }
            _initialized = true;
        }

        private void EnsureInit()
        {
            if (!_initialized || _byIndex == null) Initialize();
        }

        /// <summary>Tra cứu SO từ Fusion network index. Trả null nếu không tìm thấy.</summary>
        public ItemSO GetItem(int index)
        {
            EnsureInit();
            return _byIndex.TryGetValue(index, out var item) ? item : null;
        }

        /// <summary>
        /// Index ngẫu nhiên của 1 item thuộc category cho trước (vd Equipment). Trả -1 nếu không có.
        /// seed >=0 để chọn xác định (host truyền tick để client khỏi lệch); &lt;0 = Random.Range.
        /// </summary>
        public int GetRandomIndexOfCategory(ItemCategory category, int seed = -1)
        {
            EnsureInit();
            var candidates = new List<int>();
            foreach (var kv in _byIndex)
                if (kv.Value != null && kv.Value.Category == category)
                    candidates.Add(kv.Key);
            if (candidates.Count == 0) return -1;
            int pick = seed >= 0 ? (seed % candidates.Count) : Random.Range(0, candidates.Count);
            return candidates[pick];
        }

        /// <summary>Tra cứu network index từ SO. Trả -1 nếu không có trong database.</summary>
        public int GetIndex(ItemSO item)
        {
            if (item == null) return -1;
            return GetIndex(item.itemId);
        }

        /// <summary>Tra cứu network index từ string ID. Trả -1 nếu không có.</summary>
        public int GetIndex(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return -1;
            EnsureInit();
            return _byStringId.TryGetValue(itemId, out int idx) ? idx : -1;
        }

        /// <summary>Lấy SO theo string itemId. Trả null nếu không có.</summary>
        public ItemSO GetItemByStringId(string itemId)
        {
            int idx = GetIndex(itemId);
            return idx >= 0 ? GetItem(idx) : null;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: truy cập danh sách raw để validate / custom inspector.</summary>
        public List<ItemSO> EditorItems => items;
#endif
    }
}
