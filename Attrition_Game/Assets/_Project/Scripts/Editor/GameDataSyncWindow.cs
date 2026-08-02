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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Attrition.Editor
{
    public class GameDataSyncWindow : EditorWindow
    {
        private const string ManifestPath = "Assets/_Project/Data/Items/ItemDatabaseManifest.json";
        private static readonly Regex StableId = new("^[a-z0-9]+(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant);
        // Khớp với APIManager.baseUrl (gateway production). Sync chủ yếu chạy lên production nên để sẵn
        // — gõ tay dễ nhập URL trang web (https://attrition.io.vn/login) thay vì API base, rồi endpoint
        // thành /login/auth/login → 404. Test gateway local thì tự sửa thành http://localhost:8080/api.
        private string _baseUrl = "https://attrition.io.vn/api";
        private string _username = "";
        private string _password = "";
        private string _token = "";
        private string _report = "Run Validate before syncing.";
        private Vector2 _scroll;
        private bool _busy;

        [MenuItem("Tools/Attrition/Game Data Sync")]
        private static void Open() => GetWindow<GameDataSyncWindow>("Game Data Sync");

        public static void BatchExportBundle()
        {
            try
            {
                var scan = Scan();
                if (scan.Errors.Count > 0) throw new InvalidOperationException(string.Join("\n", scan.Errors));
                var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../game-data-bundle"));
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(Path.Combine(root, "files"));
                foreach (var image in scan.Images) CopyBundleFile(root, image.Key, image.Path, image.Type, image.Id, scan.BundleFiles);
                foreach (var music in scan.Music) CopyBundleFile(root, "music:" + music.SourceKey, music.Path, "music", music.SourceKey, scan.BundleFiles, music.Title, music.Usages);
                var itemById = FindAssets<ItemSO>().ToDictionary(x => x.itemId, StringComparer.Ordinal);
                File.WriteAllText(Path.Combine(root, "manifest.json"), JsonConvert.SerializeObject(new {
                    items = scan.Items, enemies = scan.Enemies, skills = BuildSkills(scan, itemById), files = scan.BundleFiles
                }, Formatting.Indented));
                Debug.Log($"[GameDataSync] BUNDLE SUCCESS: {root} ({scan.Items.Count} items, {scan.Enemies.Count} enemies, {scan.Skills.Count} skills, {scan.Music.Count} music)");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void CopyBundleFile(string root, string key, string source, string type, string id,
            List<BundleFile> output, string title = null, List<string> usages = null)
        {
            var name = $"{output.Count:D3}{Path.GetExtension(source).ToLowerInvariant()}";
            File.Copy(source, Path.Combine(root, "files", name), true);
            output.Add(new BundleFile { key = key, path = "files/" + name, type = type, id = id, title = title, usages = usages });
        }

        public static void BatchSyncProduction()
        {
            var username = Environment.GetEnvironmentVariable("ATTRITION_ADMIN_USERNAME");
            var password = Environment.GetEnvironmentVariable("ATTRITION_ADMIN_PASSWORD");
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                Debug.LogError("Set ATTRITION_ADMIN_USERNAME and ATTRITION_ADMIN_PASSWORD before batch sync.");
                EditorApplication.Exit(1);
                return;
            }
            var window = CreateInstance<GameDataSyncWindow>();
            window._username = username;
            window._password = password;
            window.StartEditorCoroutine(window.LoginAndSyncBatch());
        }

        private IEnumerator LoginAndSyncBatch()
        {
            yield return Login();
            if (string.IsNullOrWhiteSpace(_token))
            {
                Debug.LogError(_report);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return Sync();
            Debug.Log(_report);
            EditorApplication.Exit(_report.StartsWith("SYNC SUCCESS", StringComparison.Ordinal) ? 0 : 1);
        }

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
                ? $"VALID\nItems: {scan.Items.Count}\nEnemies: {scan.Enemies.Count}\nSkills: {scan.Skills.Count}\nImages: {scan.Images.Count}\nMusic: {scan.Music.Count}\nNo records will be deleted. Existing admin values/images will not be overwritten."
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
            _report = $"Uploading images 0/{scan.Images.Count}...";
            Debug.Log("[GameDataSync] " + _report);
            var imageIndex = 0;
            foreach (var image in scan.Images)
            {
                _report = $"Uploading images {++imageIndex}/{scan.Images.Count}: {image.Key}";
                Debug.Log("[GameDataSync] " + _report);
                Repaint();
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

            var itemById = FindAssets<ItemSO>().ToDictionary(x => x.itemId, StringComparer.Ordinal);
            var payload = JsonConvert.SerializeObject(new { items = scan.Items, enemies = scan.Enemies });
            using (var req = AuthorizedRequest($"{_baseUrl}/admin/game-data/import", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload));
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    _report = "Enemy/item import failed; retry is safe.\n" + req.downloadHandler.text;
                    _busy = false; Repaint(); yield break;
                }
            }

            var skills = BuildSkills(scan, itemById);
            using (var req = AuthorizedRequest($"{_baseUrl}/admin/skill-data/import", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { skills })));
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    _report = "Skill import failed; enemy/item metadata is already safe and retrying will not duplicate it.\n" + req.downloadHandler.text;
                    _busy = false; Repaint(); yield break;
                }
            }

            foreach (var music in scan.Music)
            {
                yield return UploadMusic(music, error => { if (error != null) scan.Errors.Add(error); });
                if (scan.Errors.Count > 0) break;
            }
            if (scan.Errors.Count > 0)
                _report = "Music sync failed; prior metadata is safe and retrying will not duplicate it.\n" + string.Join("\n", scan.Errors);
            else
            {
                File.WriteAllText(ManifestPath, JsonConvert.SerializeObject(scan.Manifest, Formatting.Indented));
                AssetDatabase.ImportAsset(ManifestPath);
                _report = $"SYNC SUCCESS\nItems: {scan.Items.Count}, Enemies: {scan.Enemies.Count}, Skills: {scan.Skills.Count}, Music: {scan.Music.Count}";
            }
            _busy = false; Repaint();
        }

        private IEnumerator UploadMusic(MusicUpload music, Action<string> done)
        {
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", File.ReadAllBytes(music.Path), Path.GetFileName(music.Path), MusicMime(music.Path)),
                new MultipartFormDataSection("sourceKey", music.SourceKey),
                new MultipartFormDataSection("title", music.Title)
            };
            foreach (var usage in music.Usages) form.Add(new MultipartFormDataSection("gameUsages", usage));
            using var req = UnityWebRequest.Post($"{_baseUrl}/music/tracks/unity-source", form);
            req.SetRequestHeader("Authorization", "Bearer " + _token);
            req.timeout = 120;
            yield return req.SendWebRequest();
            done(req.result == UnityWebRequest.Result.Success ? null : $"music:{music.Title}: {req.downloadHandler.text}");
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
                if (item is SkillSO skill)
                {
                    AddImage(result, "skill", item.itemId, item.icon);
                    AddSkill(result, skill);
                    continue;
                }
                result.Items.Add(new ItemImport { itemId = item.itemId, name = item.displayName, category = item.Category.ToString(), description = item.description, maxStack = item.maxStack, isKeyItem = item.isKeyItem, modifiers = modifiers });
                AddImage(result, "item", item.itemId, item.icon);
            }

            var itemIds = new HashSet<string>(items.Where(x => x != null).Select(x => x.itemId), StringComparer.Ordinal);
            var enemyControllers = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemy" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Select(x => x.GetComponent<Attrition.Controllers.EnemyController>())
                .Where(x => x != null)
                .Select(x => new { Controller = x, Stats = x.GetComponent<Attrition.Gameplay.Enemy.EnemyStats>()?.StatsSO })
                .Where(x => x.Stats != null)
                .GroupBy(x => x.Stats.enemyId, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First().Controller, StringComparer.Ordinal);

            foreach (var enemy in enemies)
            {
                var loot = new List<LootImport>();
                if (enemyControllers.TryGetValue(enemy.enemyId, out var controller))
                {
                    foreach (var itemId in controller.EditorLootItemIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
                    {
                        if (!itemIds.Contains(itemId)) result.Errors.Add($"Enemy '{enemy.enemyId}' references unknown loot item '{itemId}'.");
                        else
                        {
                            var chance = enemy.tier == EnemyTier.Normal ? controller.EditorNormalDropChance : 1f;
                            if (!IsFinite(chance) || chance < 0f || chance > 1f) result.Errors.Add($"Enemy '{enemy.enemyId}' has invalid drop chance {chance}.");
                            else if (chance > 0f) loot.Add(new LootImport { itemId = itemId, dropChance = chance, minQty = 1, maxQty = 1 });
                        }
                    }
                }
                result.Enemies.Add(new EnemyImport { enemyId = enemy.enemyId, name = Nicify(enemy.enemyId), tier = enemy.tier.ToString(), hp = enemy.maxHP, ad = enemy.ad, ap = enemy.ap, def = enemy.def, res = enemy.res, poise = enemy.poise, poiseRecoveryTime = enemy.poiseRecoveryTime, patrolSpeed = enemy.patrolSpeed, chaseSpeed = enemy.chaseSpeed, attackSpeed = enemy.attackSpeed, expReward = enemy.expReward, lootTable = loot });
                AddImage(result, "enemy", enemy.enemyId, enemy.webImage);
            }
            ValidateBiomes(result.Errors);
            ScanMusic(result);
            return result;
        }

        private static List<SkillImport> BuildSkills(ScanResult scan, IReadOnlyDictionary<string, ItemSO> itemById) => scan.Skills.Select(skill =>
        {
            var item = itemById[skill.skillId];
            return new SkillImport
            {
                skillId = skill.skillId, name = item.displayName, description = item.description,
                iconKey = item.itemId, rarity = "Common", element = skill.element,
                manaCost = skill.manaCost, castTime = skill.castTime, cooldown = skill.cooldown,
                activeStartFrac = skill.activeStartFrac, activeEndFrac = skill.activeEndFrac,
                damageType = skill.damageType, baseDamage = skill.baseDamage, apScaling = skill.apScaling,
                knockbackForce = skill.knockbackForce, tickInterval = skill.tickInterval,
                sweetSpotRadius = skill.sweetSpotRadius, sweetSpotMultiplier = skill.sweetSpotMultiplier,
                delivery = skill.delivery, hitShape = skill.hitShape, range = skill.range, angle = skill.angle,
                rectWidth = skill.rectWidth, rectHeight = skill.rectHeight, offsetX = skill.offsetX,
                offsetY = skill.offsetY, projectileSpeed = skill.projectileSpeed,
                projectileCount = skill.projectileCount, spreadAngle = skill.spreadAngle,
                vfxLifetime = skill.vfxLifetime, imageUrl = skill.imageUrl
            };
        }).ToList();

        private static void ScanMusic(ScanResult result)
        {
            var clips = new Dictionary<string, MusicUpload>(StringComparer.Ordinal);
            void Add(AudioClip clip, string usage)
            {
                if (clip == null) return;
                var path = AssetDatabase.GetAssetPath(clip);
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (!File.Exists(path) || ext is not (".mp3" or ".flac" or ".ogg" or ".m4a" or ".wav"))
                {
                    result.Errors.Add($"Music '{clip.name}' must be a source MP3/FLAC/OGG/M4A/WAV file.");
                    return;
                }
                var key = AssetDatabase.AssetPathToGUID(path);
                if (!clips.TryGetValue(key, out var music)) clips[key] = music = new MusicUpload { SourceKey = key, Title = clip.name, Path = path };
                if (!music.Usages.Contains(usage)) music.Usages.Add(usage);
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var scenePath in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    foreach (var component in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Component>(true)).Where(x => x != null))
                    {
                        var serialized = new SerializedObject(component);
                        Add(serialized.FindProperty("sceneBgmClip")?.objectReferenceValue as AudioClip, $"Scene: {scene.name}");
                        Add(serialized.FindProperty("bossBgmClip")?.objectReferenceValue as AudioClip, $"Boss in scene: {scene.name}");
                        Add(serialized.FindProperty("menuBgmClip")?.objectReferenceValue as AudioClip, "Main menu");
                    }
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }

            foreach (var prefabPath in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                foreach (var component in prefab.GetComponentsInChildren<Component>(true).Where(x => x != null))
                {
                    var clip = new SerializedObject(component).FindProperty("bossMusic")?.objectReferenceValue as AudioClip;
                    Add(clip, $"Boss: {prefab.name}");
                }
            }
            result.Music.AddRange(clips.Values.OrderBy(x => x.Title));
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
        private static string MusicMime(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".mp3" => "audio/mpeg", ".flac" => "audio/flac", ".ogg" => "audio/ogg", ".m4a" => "audio/mp4", _ => "audio/wav" };

        [Serializable] private class ScanResult { public readonly List<string> Errors = new(); public readonly List<ItemImport> Items = new(); public readonly List<EnemyImport> Enemies = new(); public readonly List<SkillImport> Skills = new(); public readonly List<ImageUpload> Images = new(); public readonly List<MusicUpload> Music = new(); public readonly List<BundleFile> BundleFiles = new(); public List<ManifestEntry> Manifest = new(); }
        [Serializable] private class ItemImport { public string itemId, name, category, description, imageUrl; public int maxStack; public bool isKeyItem; public List<object> modifiers; }
        [Serializable] private class EnemyImport { public string enemyId, name, tier, imageUrl; public int hp, ad, ap, def, res, poise, expReward; public float poiseRecoveryTime, patrolSpeed, chaseSpeed, attackSpeed; public List<LootImport> lootTable; }
        [Serializable] private class LootImport { public string itemId; public float dropChance; public short minQty, maxQty; }
        [Serializable] private class SkillImport { public string skillId, name, description, iconKey, rarity, element, damageType, delivery, hitShape, imageUrl; public int manaCost, baseDamage, projectileCount; public float castTime, cooldown, activeStartFrac, activeEndFrac, apScaling, knockbackForce, tickInterval, sweetSpotRadius, sweetSpotMultiplier, range, angle, rectWidth, rectHeight, offsetX, offsetY, projectileSpeed, spreadAngle, vfxLifetime; }
        [Serializable] private class ManifestEntry { public int index; public string itemId; }
        private class ImageUpload { public string Type, Id, Path; public string Key => Type + ":" + Id; }
        private class MusicUpload { public string SourceKey, Title, Path; public readonly List<string> Usages = new(); }
        [Serializable] private class BundleFile { public string key, path, type, id, title; public List<string> usages; }
    }
}
