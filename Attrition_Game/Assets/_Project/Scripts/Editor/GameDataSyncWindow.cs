using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Attrition.Data;
using Fusion;
using Fusion.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Attrition.Editor
{
    public class GameDataSyncWindow : EditorWindow
    {
        private const string ManifestPath = "Assets/_Project/Data/Items/ItemDatabaseManifest.json";
        private static readonly Regex StableId = new("^[a-z0-9]+(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant);
        private string _baseUrl = "http://localhost:8080/api";
        private string _username = "";
        private string _password = "";
        private string _token = "";
        private string _report = "Run Validate before syncing.";
        private Vector2 _scroll;
        private bool _busy;

        [MenuItem("Tools/Attrition/Game Data Sync")]
        private static void Open() => GetWindow<GameDataSyncWindow>("Game Data Sync");

        private void OnDisable()
        {
            _token = "";
            _password = "";
            if (_routineTick != null) EditorApplication.update -= _routineTick;
            _routineTick = null;
            _busy = false;
        }

        private EditorApplication.CallbackFunction _routineTick;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity → Web Game Data", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Unity owns IDs, database order, prefabs and default values. Web edits overrides. Tokens remain in memory only.", MessageType.Info);
            _baseUrl = EditorGUILayout.TextField("Gateway API URL", _baseUrl).Trim().TrimEnd('/');
            if (!IsAllowedBaseUrl(_baseUrl))
                EditorGUILayout.HelpBox("Use HTTPS, or HTTP only for localhost development.", MessageType.Error);
            _username = EditorGUILayout.TextField("Admin username", _username);
            _password = EditorGUILayout.PasswordField("Admin password", _password);
            using (new EditorGUI.DisabledScope(_busy || !IsAllowedBaseUrl(_baseUrl) || string.IsNullOrWhiteSpace(_username) || string.IsNullOrEmpty(_password)))
                if (GUILayout.Button("Login (session only)")) StartEditorCoroutine(Login());
            _token = EditorGUILayout.PasswordField("Or paste access token", _token);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_busy))
            {
                if (GUILayout.Button("Rebuild Fusion Prefab Table + Validate"))
                {
                    NetworkProjectConfigUtilities.RebuildPrefabTable();
                    ValidateAndReport();
                }
                if (GUILayout.Button("Validate / Dry Run")) ValidateAndReport();
            }
            using (new EditorGUI.DisabledScope(_busy || !IsAllowedBaseUrl(_baseUrl) || string.IsNullOrWhiteSpace(_token)))
                if (GUILayout.Button("Sync Metadata + Source Images")) StartEditorCoroutine(Sync());

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void StartEditorCoroutine(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            AsyncOperation waiting = null;
            void Tick()
            {
                try
                {
                    if (waiting != null)
                    {
                        if (!waiting.isDone) return;
                        waiting = null;
                    }
                    while (stack.Count > 0)
                    {
                        if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                        if (stack.Peek().Current is IEnumerator nested) { stack.Push(nested); continue; }
                        if (stack.Peek().Current is AsyncOperation operation) waiting = operation;
                        return;
                    }
                    EditorApplication.update -= Tick;
                    _routineTick = null;
                }
                catch (Exception e) { EditorApplication.update -= Tick; _routineTick = null; _busy = false; _report = e.Message; Repaint(); }
            }
            _routineTick = Tick;
            EditorApplication.update += Tick;
        }

        private void ValidateAndReport()
        {
            var scan = Scan();
            _report = scan.Errors.Count == 0
                ? $"VALID\nItems: {scan.Items.Count}\nEnemies: {scan.Enemies.Count}\nSkills: {scan.Skills.Count}\nImages: {scan.Images.Count}\nNo records will be deleted. Existing admin values/images will not be overwritten."
                : "INVALID — sync blocked\n\n" + string.Join("\n", scan.Errors.Select(x => "• " + x));
        }

        private IEnumerator Login()
        {
            _busy = true;
            var body = JsonConvert.SerializeObject(new { username = _username, password = _password, rememberMe = false });
            using var req = new UnityWebRequest($"{_baseUrl}/auth/login", "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;
            yield return req.SendWebRequest();
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException(RequestError(req));
                var json = JObject.Parse(req.downloadHandler.text);
                _token = (string)json["data"]?["accessToken"] ?? throw new InvalidOperationException("Login response has no access token.");
                _password = "";
                _report = "Login successful. Access token is held in memory only.";
            }
            catch (Exception e) { _report = "Login failed: " + e.Message; }
            _busy = false;
            Repaint();
        }

        private IEnumerator Sync()
        {
            _busy = true;
            var scan = Scan();
            if (scan.Errors.Count > 0)
            {
                _report = "INVALID — sync blocked\n" + string.Join("\n", scan.Errors);
                _busy = false; Repaint(); yield break;
            }

            var imageUrls = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var image in scan.Images)
            {
                yield return UploadImage(image, (url, error) =>
                {
                    if (error != null) scan.Errors.Add(error);
                    else imageUrls[image.Key] = url;
                });
                if (scan.Errors.Count > 0) break;
            }
            if (scan.Errors.Count > 0)
            {
                _report = "Image sync failed; metadata was not changed.\n" + string.Join("\n", scan.Errors);
                _busy = false; Repaint(); yield break;
            }

            foreach (var item in scan.Items)
                if (imageUrls.TryGetValue($"{(item.category == "Skill" ? "skill" : "item")}:{item.itemId}", out var url)) item.imageUrl = url;
            foreach (var enemy in scan.Enemies)
                if (imageUrls.TryGetValue($"enemy:{enemy.enemyId}", out var url)) enemy.imageUrl = url;
            foreach (var skill in scan.Skills)
                if (imageUrls.TryGetValue($"skill:{skill.skillId}", out var url)) skill.imageUrl = url;

            var payload = JsonConvert.SerializeObject(new { items = scan.Items, enemies = scan.Enemies, skills = scan.Skills });
            using (var req = AuthorizedRequest($"{_baseUrl}/admin/game-data/import", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload));
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    _report = "Import failed; retry is safe.\n" + req.downloadHandler.text;
                else
                {
                    File.WriteAllText(ManifestPath, JsonConvert.SerializeObject(scan.Manifest, Formatting.Indented));
                    AssetDatabase.ImportAsset(ManifestPath);
                    _report = "SYNC SUCCESS\n" + req.downloadHandler.text;
                }
            }
            _busy = false; Repaint();
        }

        private IEnumerator UploadImage(ImageUpload image, Action<string, string> done)
        {
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", File.ReadAllBytes(image.Path), Path.GetFileName(image.Path), Mime(image.Path)),
                new MultipartFormDataSection("sourceType", image.Type),
                new MultipartFormDataSection("sourceId", image.Id)
            };
            using var req = UnityWebRequest.Post($"{_baseUrl}/admin/assets/unity-source", form);
            req.SetRequestHeader("Authorization", "Bearer " + _token);
            req.timeout = 30;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) done(null, $"{image.Key}: {req.downloadHandler.text}");
            else
            {
                try { done((string)JObject.Parse(req.downloadHandler.text)["data"]?["filePath"], null); }
                catch (Exception e) { done(null, $"{image.Key}: invalid upload response ({e.Message})"); }
            }
        }

        private UnityWebRequest AuthorizedRequest(string url, string method)
        {
            var req = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer(), timeout = 20 };
            req.SetRequestHeader("Authorization", "Bearer " + _token);
            return req;
        }

        private static ScanResult Scan()
        {
            var result = new ScanResult();
            var items = FindAssets<ItemSO>();
            var enemies = FindAssets<EnemyStatsSO>();
            ValidateIds(items.Select(x => x.itemId), "itemId", result.Errors);
            ValidateIds(enemies.Select(x => x.enemyId), "enemyId", result.Errors);

            var databases = FindAssets<ItemDatabaseSO>();
            if (databases.Count != 1) result.Errors.Add($"Expected exactly one ItemDatabaseSO, found {databases.Count}.");
            else ValidateDatabase(databases[0], items, result);

            foreach (var item in items)
            {
                if (item == null) continue;
                var modifiers = new List<object>();
                if (item is EquipmentSO eq && eq.modifiers != null) modifiers.AddRange(eq.modifiers.Select(m => (object)new { stat = m.stat.ToString(), amount = m.amount }));
                if (item is AccessorySO acc && acc.modifiers != null) modifiers.AddRange(acc.modifiers.Select(m => (object)new { stat = m.stat.ToString(), amount = m.amount }));
                result.Items.Add(new ItemImport { itemId = item.itemId, name = item.displayName, category = item.Category.ToString(), description = item.description, maxStack = item.maxStack, isKeyItem = item.isKeyItem, modifiers = modifiers });
                AddImage(result, item is SkillSO ? "skill" : "item", item.itemId, item.icon);
                if (item is SkillSO skill) AddSkill(result, skill);
            }

            foreach (var enemy in enemies)
            {
                result.Enemies.Add(new EnemyImport { enemyId = enemy.enemyId, name = Nicify(enemy.enemyId), tier = enemy.tier.ToString(), hp = enemy.maxHP, ad = enemy.ad, ap = enemy.ap, def = enemy.def, res = enemy.res, poise = enemy.poise, poiseRecoveryTime = enemy.poiseRecoveryTime, patrolSpeed = enemy.patrolSpeed, chaseSpeed = enemy.chaseSpeed, attackSpeed = enemy.attackSpeed, expReward = enemy.expReward });
                AddImage(result, "enemy", enemy.enemyId, enemy.webImage);
            }
            ValidateBiomes(result.Errors);
            return result;
        }

        private static void ValidateDatabase(ItemDatabaseSO db, List<ItemSO> all, ScanResult result)
        {
            var registered = db.EditorItems;
            for (int i = 0; i < registered.Count; i++) if (registered[i] == null) result.Errors.Add($"ItemDatabase slot {i} is null.");
            foreach (var duplicate in registered.Where(x => x != null).GroupBy(x => x).Where(x => x.Count() > 1)) result.Errors.Add($"ItemDatabase contains duplicate asset '{duplicate.Key.name}'.");
            foreach (var item in all) if (!registered.Contains(item)) result.Errors.Add($"Item '{item.itemId}' is missing from ItemDatabase.");
            result.Manifest = registered.Select((item, index) => new ManifestEntry { index = index, itemId = item != null ? item.itemId : null }).ToList();
            if (!File.Exists(ManifestPath)) return;
            try
            {
                var previous = JsonConvert.DeserializeObject<List<ManifestEntry>>(File.ReadAllText(ManifestPath)) ?? new();
                foreach (var entry in previous)
                    if (entry.index >= result.Manifest.Count || result.Manifest[entry.index].itemId != entry.itemId)
                        result.Errors.Add($"ItemDatabase append-only violation at index {entry.index}: expected '{entry.itemId}'.");
            }
            catch (Exception e) { result.Errors.Add("Cannot read ItemDatabase manifest: " + e.Message); }
        }

        private static void ValidateBiomes(List<string> errors)
        {
            var biomes = FindAssets<EnemyBiomeDefinition>();
            ValidateIds(biomes.Select(x => x.biomeId), "biomeId", errors);
            var referencedEnemyIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var biome in biomes)
            {
                if (biome.pool == null || biome.pool.Length == 0) continue;
                foreach (var entry in biome.pool)
                {
                    if (entry == null || entry.prefab == null) continue;
                    if (entry.weight <= 0) errors.Add($"Biome '{biome.biomeId}' has a non-positive weight.");
                    var path = AssetDatabase.GetAssetPath(entry.prefab);
                    var stats = entry.prefab.GetComponent<Attrition.Gameplay.Enemy.EnemyStats>();
                    if (stats == null) errors.Add($"Enemy prefab '{path}' has no EnemyStats component.");
                    else if (stats.StatsSO == null) errors.Add($"Enemy prefab '{path}' has no EnemyStatsSO assigned.");
                    else if (referencedEnemyIds.TryGetValue(stats.EnemyId, out var previousPath) && previousPath != path) errors.Add($"Enemy ID '{stats.EnemyId}' is used by prefabs '{previousPath}' and '{path}'.");
                    else referencedEnemyIds[stats.EnemyId] = path;
                    if (!NetworkProjectConfigUtilities.TryGetPrefabId(path, out _)) errors.Add($"Enemy prefab '{path}' is missing from the Fusion Prefab Table.");
                }
            }
        }

        private static void AddSkill(ScanResult result, SkillSO s)
        {
            bool finite = IsFinite(s.castTime, s.cooldown, s.activeStartFrac, s.activeEndFrac, s.apScaling,
                s.knockbackForce, s.tickInterval, s.sweetSpotRadius, s.sweetSpotMultiplier, s.range,
                s.angle, s.rectSize.x, s.rectSize.y, s.hitboxOffset.x, s.hitboxOffset.y,
                s.projectileSpeed, s.spreadAngle, s.vfxLifetime);
            if (!finite || s.manaCost < 0 || s.baseDamage < 0 || s.castTime < 0 || s.cooldown < 0 ||
                s.activeStartFrac < 0 || s.activeEndFrac < s.activeStartFrac || s.activeEndFrac > 1 ||
                s.apScaling < 0 || s.knockbackForce < 0 || s.tickInterval < 0 || s.sweetSpotRadius < 0 ||
                s.sweetSpotMultiplier < 0 || s.range < 0 || s.angle < 0 || s.angle > 360 ||
                s.rectSize.x < 0 || s.rectSize.y < 0 || s.projectileSpeed < 0 || s.projectileCount < 1 ||
                s.spreadAngle < 0 || s.vfxLifetime < 0)
                result.Errors.Add($"Skill '{s.itemId}' has invalid numeric ranges.");
            if (s.delivery == SkillDelivery.Projectile && !s.projectilePrefab.IsValid) result.Errors.Add($"Projectile skill '{s.itemId}' has no NetworkPrefabRef.");
            result.Skills.Add(new SkillImport { skillId = s.itemId, element = s.element.ToString(), manaCost = s.manaCost, castTime = s.castTime, cooldown = s.cooldown, activeStartFrac = s.activeStartFrac, activeEndFrac = s.activeEndFrac, damageType = s.damageType.ToString(), baseDamage = s.baseDamage, apScaling = s.apScaling, knockbackForce = s.knockbackForce, tickInterval = s.tickInterval, sweetSpotRadius = s.sweetSpotRadius, sweetSpotMultiplier = s.sweetSpotMultiplier, delivery = s.delivery.ToString(), hitShape = s.hitShape.ToString(), range = s.range, angle = s.angle, rectWidth = s.rectSize.x, rectHeight = s.rectSize.y, offsetX = s.hitboxOffset.x, offsetY = s.hitboxOffset.y, projectileSpeed = s.projectileSpeed, projectileCount = s.projectileCount, spreadAngle = s.spreadAngle, vfxLifetime = s.vfxLifetime });
        }

        private static void AddImage(ScanResult result, string type, string id, Sprite sprite)
        {
            if (sprite == null) return;
            var path = AssetDatabase.GetAssetPath(sprite.texture);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!File.Exists(path) || ext is not (".png" or ".jpg" or ".jpeg" or ".webp")) result.Errors.Add($"{type}:{id} image must be a source PNG/JPG/WebP file.");
            else result.Images.Add(new ImageUpload { Type = type, Id = id, Path = path });
        }

        private static void ValidateIds(IEnumerable<string> ids, string label, List<string> errors)
        {
            foreach (var id in ids)
                if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || !StableId.IsMatch(id)) errors.Add($"Invalid {label} '{id}'; use lower_snake_case up to 64 characters.");
            foreach (var group in ids.GroupBy(x => x, StringComparer.Ordinal).Where(x => x.Count() > 1)) errors.Add($"Duplicate {label} '{group.Key}'.");
        }

        private static string RequestError(UnityWebRequest req)
        {
            var body = req.downloadHandler?.text;
            return $"HTTP {req.responseCode} {req.error}\nURL: {req.url}" +
                (string.IsNullOrWhiteSpace(body) ? "\nNo response body. Check that the gateway is running and reachable." : "\n" + body);
        }

        private static bool IsAllowedBaseUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttps ||
                (uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || uri.Host == "localhost"));
        }

        private static bool IsFinite(params float[] values) => values.All(x => !float.IsNaN(x) && !float.IsInfinity(x));
        private static List<T> FindAssets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets($"t:{typeof(T).Name}").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x => x != null).ToList();
        private static string Nicify(string id) => ObjectNames.NicifyVariableName(id ?? "Enemy");
        private static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };

        [Serializable] private class ScanResult { public readonly List<string> Errors = new(); public readonly List<ItemImport> Items = new(); public readonly List<EnemyImport> Enemies = new(); public readonly List<SkillImport> Skills = new(); public readonly List<ImageUpload> Images = new(); public List<ManifestEntry> Manifest = new(); }
        [Serializable] private class ItemImport { public string itemId, name, category, description, imageUrl; public int maxStack; public bool isKeyItem; public List<object> modifiers; }
        [Serializable] private class EnemyImport { public string enemyId, name, tier, imageUrl; public int hp, ad, ap, def, res, poise, expReward; public float poiseRecoveryTime, patrolSpeed, chaseSpeed, attackSpeed; }
        [Serializable] private class SkillImport { public string skillId, element, damageType, delivery, hitShape, imageUrl; public int manaCost, baseDamage, projectileCount; public float castTime, cooldown, activeStartFrac, activeEndFrac, apScaling, knockbackForce, tickInterval, sweetSpotRadius, sweetSpotMultiplier, range, angle, rectWidth, rectHeight, offsetX, offsetY, projectileSpeed, spreadAngle, vfxLifetime; }
        [Serializable] private class ManifestEntry { public int index; public string itemId; }
        private class ImageUpload { public string Type, Id, Path; public string Key => Type + ":" + Id; }
    }
}
