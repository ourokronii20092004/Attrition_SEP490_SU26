using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Attrition.Persistence.Dtos;

namespace Attrition.Persistence
{
    /// <summary>Giá trị override config item do admin sửa trên web (null field = giữ default SO).</summary>
    public class ItemConfigOverride
    {
        public string name;
        public string description;
        public int? maxStack;
        public bool? isKeyItem;
        public List<(string stat, int amount)> modifiers;
    }

    /// <summary>
    /// Cầu nối WEB → GAME cho config item (giống EnemyStatProvider cho quái).
    /// Admin sửa item trên web → Postgres/Redis. Host gọi PrefetchAll khi vào trận:
    /// hỏi version, chỉ tải lại khi đổi; cache override theo itemId. Solo/offline dùng default SO.
    /// </summary>
    public class ItemConfigProvider : MonoBehaviour
    {
        public static ItemConfigProvider Instance { get; private set; }

        [Tooltip("Base URL gateway. Để trống = bỏ qua override, dùng default trong SO (chơi offline).")]
        public string baseUrl = "http://localhost:5130/api";

        private readonly Dictionary<string, ItemConfigOverride> _cache = new();
        private bool _ready;
        private string _loadedVersion;

        public bool IsReady => _ready;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static ItemConfigProvider Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("ItemConfigProvider");
                Instance = go.AddComponent<ItemConfigProvider>();
            }
            if (APIManager.Instance != null && !string.IsNullOrEmpty(APIManager.Instance.BaseUrl))
                Instance.baseUrl = APIManager.Instance.BaseUrl;
            return Instance;
        }

        /// <summary>
        /// Host gọi 1 lần khi vào scene: tải cục item config (GET /api/itemconfig) → cache theo itemId.
        /// baseUrl rỗng = offline, dùng default SO. Lỗi mạng → giữ cache cũ / default SO.
        /// </summary>
        public IEnumerator PrefetchAll(Action onDone = null)
        {
            yield return PrefetchBundle(null, onDone);
        }

        /// <summary>
        /// Tải item config khi đã biết trước version (NetworkSpawner gọi version gộp enemy+item 1 lần).
        /// remoteVersion null/khác cache → tải full /api/itemconfig; trùng + cache còn data → bỏ qua.
        /// baseUrl rỗng = offline, dùng default SO. Lỗi mạng → giữ cache cũ / default SO.
        /// </summary>
        public IEnumerator PrefetchBundle(string remoteVersion, Action onDone = null)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                _ready = true;
                onDone?.Invoke();
                yield break;
            }

            if (!string.IsNullOrEmpty(remoteVersion) && remoteVersion == _loadedVersion && _cache.Count > 0)
            {
                // Web không đổi từ lần tải trước trong phiên → giữ nguyên cache, khỏi tải.
                _ready = true;
                onDone?.Invoke();
                yield break;
            }

            string url = $"{baseUrl}/itemconfig";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[ItemConfigProvider] itemconfig: {req.error} — giữ cache cũ / default SO.");
                }
                else
                {
                    try
                    {
                        var resp = JsonConvert.DeserializeObject<ApiResponse<ItemConfigBundleDto>>(req.downloadHandler.text);
                        if (resp != null && resp.Success && resp.Data != null && resp.Data.Version != _loadedVersion)
                        {
                            _cache.Clear();
                            if (resp.Data.Items != null)
                                foreach (var d in resp.Data.Items)
                                    if (d != null && !string.IsNullOrEmpty(d.ItemId))
                                        _cache[d.ItemId] = ToOverride(d);
                            _loadedVersion = resp.Data.Version;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ItemConfigProvider] itemconfig parse fail: {e.Message}");
                    }
                }
            }

            _ready = true;
            onDone?.Invoke();
        }

        private static ItemConfigOverride ToOverride(ItemResponseDto d)
        {
            var ov = new ItemConfigOverride
            {
                name = d.Name,
                description = d.Description,
                maxStack = d.MaxStack,
                isKeyItem = d.IsKeyItem,
            };
            if (d.Modifiers != null && d.Modifiers.Count > 0)
            {
                ov.modifiers = new List<(string, int)>();
                foreach (var m in d.Modifiers)
                    if (m != null && !string.IsNullOrEmpty(m.Stat))
                        ov.modifiers.Add((m.Stat, m.Amount));
            }
            return ov;
        }

        /// <summary>Lấy override đã cache (null = không có, dùng default SO). Gọi đồng bộ lúc cần.</summary>
        public ItemConfigOverride GetOverride(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            return _cache.TryGetValue(itemId, out var o) ? o : null;
        }
    }
}
