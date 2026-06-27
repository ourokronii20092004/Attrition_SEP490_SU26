using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Attrition.Persistence.Dtos;
using Attrition.Systems;

namespace Attrition.Persistence
{
    /// <summary>
    /// Cầu nối WEB → GAME cho chỉ số quái.
    /// Luồng: admin sửa stats quái trên web → Postgres + Redis.
    /// Khi vào phòng/load scene, CHỈ Host gọi prefetch → cache override theo enemyId.
    /// NetworkSpawner đọc cache (đồng bộ) lúc spawn để build EnemyStatSheet, rồi sync con số
    /// đã chốt xuống client qua [Networked]. Client KHÔNG gọi API.
    /// "Out phòng → vào lại → quái load số mới" hoạt động vì mỗi session prefetch lại.
    /// </summary>
    public class EnemyStatProvider : MonoBehaviour
    {
        public static EnemyStatProvider Instance { get; private set; }

        [Tooltip("Base URL gateway. Để trống = bỏ qua override, dùng default trong SO (chơi offline).")]
        public string baseUrl = "http://localhost:5130/api";

        private readonly Dictionary<string, EnemyStatOverride> _cache = new();
        private bool _ready;

        public bool IsReady => _ready;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Lấy provider (tạo runtime nếu scene chưa có). baseUrl đồng bộ với APIManager để khỏi
        /// hardcode lệch port. Gọi bởi host trước khi spawn quái.
        /// </summary>
        public static EnemyStatProvider Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("EnemyStatProvider");
                Instance = go.AddComponent<EnemyStatProvider>();
            }
            if (APIManager.Instance != null && !string.IsNullOrEmpty(APIManager.Instance.BaseUrl))
                Instance.baseUrl = APIManager.Instance.BaseUrl;
            return Instance;
        }

        /// <summary>Host gọi 1 lần khi vào phòng: tải override cho các enemyId trong scene.</summary>
        public IEnumerator Prefetch(IEnumerable<string> enemyIds, Action onDone = null)
        {
            _cache.Clear();
            if (string.IsNullOrEmpty(baseUrl))
            {
                _ready = true;
                onDone?.Invoke();
                yield break;
            }

            foreach (var id in enemyIds)
            {
                if (string.IsNullOrEmpty(id) || _cache.ContainsKey(id)) continue;
                yield return FetchOne(id);
            }

            _ready = true;
            onDone?.Invoke();
        }

        /// <summary>
        /// Host gọi 1 lần khi vào scene: tải TOÀN BỘ bestiary trong 1 request (GET /api/enemies)
        /// → cache override theo enemyId. Khỏi cần biết scene có quái gì (tránh đọc component
        /// Gameplay → không tạo vòng lặp assembly). baseUrl rỗng = bỏ qua, dùng default SO (offline).
        /// </summary>
        public IEnumerator PrefetchAll(Action onDone = null)
        {
            _cache.Clear();
            if (string.IsNullOrEmpty(baseUrl))
            {
                _ready = true;
                onDone?.Invoke();
                yield break;
            }

            string url = $"{baseUrl}/enemies";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 3; // Tối đa 3 giây — nếu API không chạy thì fail nhanh, dùng default SO.
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[EnemyStatProvider] PrefetchAll: {req.error} — dùng default SO.");
                }
                else
                {
                    try
                    {
                        var resp = JsonConvert.DeserializeObject<ApiResponse<List<EnemyResponseDto>>>(req.downloadHandler.text);
                        if (resp != null && resp.Success && resp.Data != null)
                            foreach (var d in resp.Data)
                                if (d != null && !string.IsNullOrEmpty(d.EnemyId))
                                    _cache[d.EnemyId] = ToOverride(d);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[EnemyStatProvider] PrefetchAll parse fail: {e.Message}");
                    }
                }
            }

            _ready = true;
            onDone?.Invoke();
        }

        private IEnumerator FetchOne(string enemyId)
        {
            string url = $"{baseUrl}/enemies/{UnityWebRequest.EscapeURL(enemyId)}";
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[EnemyStatProvider] {enemyId}: {req.error} — dùng default SO.");
                    yield break;
                }

                try
                {
                    var resp = JsonConvert.DeserializeObject<ApiResponse<EnemyResponseDto>>(req.downloadHandler.text);
                    if (resp != null && resp.Success && resp.Data != null)
                        _cache[enemyId] = ToOverride(resp.Data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[EnemyStatProvider] parse fail {enemyId}: {e.Message}");
                }
            }
        }

        private static EnemyStatOverride ToOverride(EnemyResponseDto d) => new()
        {
            maxHP = d.Hp,
            ad = d.Ad,
            ap = d.Ap,
            def = d.Def,
            res = d.Res,
        };

        /// <summary>Lấy override đã cache (null = không có, dùng default SO). Gọi đồng bộ lúc spawn.</summary>
        public EnemyStatOverride GetOverride(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return null;
            return _cache.TryGetValue(enemyId, out var o) ? o : null;
        }

        /// <summary>Xóa cache override (gọi khi vào solo/offline để Instance singleton không giữ override
        /// của phiên coop trước → đảm bảo solo luôn dùng default SO).</summary>
        public void ClearOverrides()
        {
            _cache.Clear();
            _ready = false;
        }
    }
}
