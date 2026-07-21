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
        private string _loadedVersion; // version của bundle đã tải trong phiên (so để khỏi tải lại)

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
        /// Host gọi 1 lần khi vào scene. Tối ưu: hỏi version trước (GET /api/gameconfig/version);
        /// nếu KHỚP version đã cache trong phiên → KHỎI tải lại (dùng _cache đang có). Khác/chưa có
        /// → tải full bundle (GET /api/gameconfig) 1 request. baseUrl rỗng = offline, dùng default SO.
        /// </summary>
        public IEnumerator PrefetchAll(Action onDone = null)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                _ready = true;
                onDone?.Invoke();
                yield break;
            }

            // 1) Hỏi version (nhẹ). Nếu trùng version đã có VÀ cache còn dữ liệu → bỏ qua tải full.
            string remoteVersion = null;
            using (var vreq = UnityWebRequest.Get($"{baseUrl}/gameconfig/version"))
            {
                vreq.timeout = 3;
                yield return vreq.SendWebRequest();
                if (vreq.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var vr = JsonConvert.DeserializeObject<ApiResponse<GameConfigVersionDto>>(vreq.downloadHandler.text);
                        if (vr != null && vr.Success && vr.Data != null) remoteVersion = vr.Data.Version;
                    }
                    catch { /* parse fail → coi như không lấy được version, tải full bên dưới */ }
                }
            }

            yield return PrefetchBundle(remoteVersion, onDone);
        }

        /// <summary>
        /// Tải bundle khi đã biết trước version (NetworkSpawner gọi version gộp enemy+item 1 lần).
        /// remoteVersion null/khác cache → tải full /api/gameconfig; trùng + cache còn data → bỏ qua.
        /// baseUrl rỗng = offline, dùng default SO.
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

            // Tải full bundle.
            string url = $"{baseUrl}/gameconfig";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[EnemyStatProvider] gameconfig: {req.error} — giữ cache cũ / default SO.");
                }
                else
                {
                    try
                    {
                        var resp = JsonConvert.DeserializeObject<ApiResponse<GameConfigBundleDto>>(req.downloadHandler.text);
                        if (resp != null && resp.Success && resp.Data != null)
                        {
                            var next = new Dictionary<string, EnemyStatOverride>(StringComparer.Ordinal);
                            if (resp.Data.Enemies != null)
                                foreach (var d in resp.Data.Enemies)
                                    if (d != null && IsValid(d) && !next.ContainsKey(d.EnemyId))
                                        next.Add(d.EnemyId, ToOverride(d));
                            _cache.Clear();
                            foreach (var pair in next) _cache.Add(pair.Key, pair.Value);
                            _loadedVersion = resp.Data.Version;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[EnemyStatProvider] gameconfig parse fail: {e.Message}");
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

        private static EnemyStatOverride ToOverride(EnemyResponseDto d)
        {
            var ov = new EnemyStatOverride
            {
                maxHP = d.Hp,
                ad = d.Ad,
                ap = d.Ap,
                def = d.Def,
                res = d.Res,
                poise = d.Poise,
                poiseRecoveryTime = d.PoiseRecoveryTime,
                patrolSpeed = d.PatrolSpeed,
                chaseSpeed = d.ChaseSpeed,
                attackSpeed = d.AttackSpeed,
                expReward = d.ExpReward,
            };

            // Bảng rơi đồ admin cấu hình trên web → chuẩn hoá thành LootRule cho game.
            // ItemName trên web = itemId trong ItemDatabase. Bỏ rule rỗng / tỉ lệ <= 0.
            if (d.LootTable != null && d.LootTable.Count > 0)
            {
                ov.loot = new List<LootRule>();
                foreach (var e in d.LootTable)
                {
                    if (e == null || string.IsNullOrEmpty(e.ItemName) || e.DropChance <= 0f) continue;
                    ov.loot.Add(new LootRule
                    {
                        itemId = e.ItemName,
                        dropChance = Mathf.Clamp01(e.DropChance),
                        minQty = Mathf.Max(1, e.MinQty),
                        maxQty = Mathf.Max(Mathf.Max(1, e.MinQty), e.MaxQty),
                    });
                }
            }

            return ov;
        }

        private static bool IsValid(EnemyResponseDto d) =>
            !string.IsNullOrEmpty(d.EnemyId) && d.Hp > 0 && d.Ad >= 0 && d.Ap >= 0 && d.Def >= 0 && d.Res >= 0 &&
            d.Poise >= 0 && d.ExpReward >= 0 && IsFiniteNonNegative(d.PoiseRecoveryTime) &&
            IsFiniteNonNegative(d.PatrolSpeed) && IsFiniteNonNegative(d.ChaseSpeed) &&
            !float.IsNaN(d.AttackSpeed) && !float.IsInfinity(d.AttackSpeed) && d.AttackSpeed > 0;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;

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
