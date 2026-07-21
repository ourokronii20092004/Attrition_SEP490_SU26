using System;
using System.Collections;
using System.Collections.Generic;
using Attrition.Core;
using Attrition.Data;
using Attrition.Persistence.Dtos;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Attrition.Persistence
{
    public class SkillConfigProvider : MonoBehaviour
    {
        public static SkillConfigProvider Instance { get; private set; }
        public string baseUrl = "http://localhost:5130/api";

        private Dictionary<string, SkillResponseDto> _cache = new();
        private string _loadedVersion;
        public bool IsReady { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static SkillConfigProvider Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("SkillConfigProvider");
                Instance = go.AddComponent<SkillConfigProvider>();
            }
            if (APIManager.Instance != null && !string.IsNullOrEmpty(APIManager.Instance.BaseUrl))
                Instance.baseUrl = APIManager.Instance.BaseUrl;
            return Instance;
        }

        public IEnumerator PrefetchBundle(string remoteVersion, Action onDone = null)
        {
            if (string.IsNullOrEmpty(baseUrl)) { IsReady = true; onDone?.Invoke(); yield break; }
            if (!string.IsNullOrEmpty(remoteVersion) && remoteVersion == _loadedVersion && _cache.Count > 0)
            { IsReady = true; onDone?.Invoke(); yield break; }

            using (var req = UnityWebRequest.Get($"{baseUrl}/skillconfig"))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning($"[SkillConfigProvider] skillconfig: {req.error} — giữ cache cũ / SkillSO.");
                else
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<ApiResponse<SkillConfigBundleDto>>(req.downloadHandler.text);
                        if (response != null && response.Success && response.Data != null)
                        {
                            var next = new Dictionary<string, SkillResponseDto>(StringComparer.Ordinal);
                            if (response.Data.Skills != null)
                                foreach (var dto in response.Data.Skills)
                                    if (IsValid(dto) && !next.ContainsKey(dto.SkillId)) next.Add(dto.SkillId, dto);
                            _cache = next;
                            _loadedVersion = response.Data.Version;
                        }
                    }
                    catch (Exception e) { Debug.LogWarning($"[SkillConfigProvider] parse fail: {e.Message}"); }
                }
            }
            IsReady = true;
            onDone?.Invoke();
        }

        internal SkillRuntimeConfig ApplyOverride(string skillId, SkillRuntimeConfig config)
        {
            if (!_cache.TryGetValue(skillId, out var d)) return config;
            if (!Enum.TryParse(d.Element, out SkillElement element) ||
                !Enum.TryParse(d.DamageType, out DamageType damageType) ||
                !Enum.TryParse(d.Delivery, out SkillDelivery delivery) ||
                !Enum.TryParse(d.HitShape, out SkillHitShape hitShape)) return config;
            config.element = element; config.damageType = damageType; config.delivery = delivery; config.hitShape = hitShape;
            config.manaCost = d.ManaCost; config.castTime = d.CastTime; config.cooldown = d.Cooldown;
            config.activeStartFrac = d.ActiveStartFrac; config.activeEndFrac = d.ActiveEndFrac;
            config.baseDamage = d.BaseDamage; config.apScaling = d.ApScaling; config.knockbackForce = d.KnockbackForce;
            config.tickInterval = d.TickInterval; config.sweetSpotRadius = d.SweetSpotRadius;
            config.sweetSpotMultiplier = d.SweetSpotMultiplier; config.range = d.Range; config.angle = d.Angle;
            config.rectSize = new Vector2(d.RectWidth, d.RectHeight); config.hitboxOffset = new Vector2(d.OffsetX, d.OffsetY);
            config.projectileSpeed = d.ProjectileSpeed; config.projectileCount = d.ProjectileCount;
            config.spreadAngle = d.SpreadAngle; config.vfxLifetime = d.VfxLifetime;
            return config;
        }

        private static bool IsValid(SkillResponseDto d) =>
            d != null && !string.IsNullOrEmpty(d.SkillId) && d.ManaCost >= 0 && d.BaseDamage >= 0 &&
            d.CastTime >= 0 && d.Cooldown >= 0 && d.ActiveStartFrac >= 0 && d.ActiveEndFrac >= d.ActiveStartFrac &&
            d.ActiveEndFrac <= 1 && d.ProjectileCount >= 1 && AllFinite(d.CastTime, d.Cooldown, d.ActiveStartFrac,
                d.ActiveEndFrac, d.ApScaling, d.KnockbackForce, d.TickInterval, d.SweetSpotRadius,
                d.SweetSpotMultiplier, d.Range, d.Angle, d.RectWidth, d.RectHeight, d.OffsetX, d.OffsetY,
                d.ProjectileSpeed, d.SpreadAngle, d.VfxLifetime);

        private static bool AllFinite(params float[] values)
        {
            foreach (var value in values) if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            return true;
        }
    }
}
