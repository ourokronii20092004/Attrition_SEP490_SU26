using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Attrition.Gameplay.Environment
{
    /// <summary>
    /// BẢN ĐỒ TỔNG (nhấn M) kiểu Hollow Knight / Afterimage.
    ///   - Vẽ TẤT CẢ map (MapRegistry) trong CÙNG 1 không gian "map-space" theo worldMapOffset →
    ///     các room/map liên kết nhau dù scene tách rời. Chỉ dùng silhouette layer Ground.
    ///   - Fog of war: phủ sương ô CHƯA đi (WorldMapState). Player dot (chấm trắng) = vị trí hiện tại.
    ///   - Mở map: hiện chấm player + ZOOM vào gần đó. Lăn chuột = thu/phóng. Kéo chuột = pan.
    ///   - Marker điểm rest đã khám phá → click chọn → Travel (cùng/khác scene). Solo + Coop.
    /// </summary>
    public class WorldMapController : MonoBehaviour
    {
        [Header("---- ICON (tùy chọn, bỏ trống = tự sinh hình tròn) ----")]
        [Tooltip("Sprite điểm Teleport (checkpoint) trên map. Bỏ trống = chấm tròn vàng tự vẽ.")]
        [SerializeField] private Sprite teleIcon;
        [Tooltip("Sprite ĐẦU player hiện trên map. Bỏ trống = chấm tròn trắng tự vẽ.")]
        [SerializeField] private Sprite playerIcon;
        [Tooltip("Icon cho player CỦA MÌNH (local) trên map. Bỏ trống = dùng playerIcon / sprite đầu / chấm xanh.")]
        [SerializeField] private Sprite localPlayerIcon;
        [Tooltip("Icon cho ĐỒNG ĐỘI (remote, coop) trên map. Bỏ trống = dùng playerIcon / sprite đầu / chấm cam.")]
        [SerializeField] private Sprite remotePlayerIcon;
        [Tooltip("Kích thước icon player trên map (px màn hình, giữ nguyên khi zoom). Tăng để icon to hơn.")]
        [SerializeField] private float playerIconSize = 26f;

        // px hiển thị cho mỗi 1 world-unit ở zoom = 1. Toàn bộ map-space nhân hệ số này.
        private const float PixelsPerUnit = 3f;
        // Dải cuộn cho phép quanh mức "15 ô" (baseZoom): thu nhỏ tối đa 0.6×, phóng to tối đa 2×.
        private const float ZoomOutLimit = 0.6f, ZoomInLimit = 2f;
        private float _baseZoom = 1f;   // mức zoom chuẩn (thấy ~15 ô/bên), tính trong CenterOnPlayer
        private static Sprite _circleSprite;  // hình tròn tự sinh (cache) dùng khi không gán icon

        private Canvas _canvas;
        private GameObject _panel;
        private RectTransform _viewport;   // khung cắt (mask)
        private RectTransform _content;    // chứa mọi map + marker + player dot; pan/zoom ở đây
        // Icon vị trí từng player (local + đồng đội coop) + PlayerController tương ứng để cập nhật mỗi frame.
        private readonly List<(RectTransform rt, PlayerController pc)> _playerDots = new List<(RectTransform, PlayerController)>();
        private MapDataSO _openMap;        // map hiện hành, cache lúc mở để reposition icon khỏi tính lại mỗi frame
        private TextMeshProUGUI _title;
        private Button _travelBtn;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<GameObject> _constantScale = new List<GameObject>();

        private MapRegistrySO _registry;
        private MapDataSO.CheckpointMarker? _selected;
        private MapDataSO _selectedMap;
        private bool _open;
        private float _zoom = 1f;

        /// <summary>True khi World Map đang mở (mọi máy tự quản). PlayerController đọc để KHÓA di chuyển local.</summary>
        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            _registry = MapRegistrySO.Load();
            EnsureEventSystem();
            BuildUI();
            SetOpen(false);
        }

        // uGUI Button cần EventSystem để nhận click + hiện trỏ chuột. Game dùng UI Toolkit nên scene
        // có thể KHÔNG có EventSystem → tự tạo.
        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        // ─────────────── BUILD UI ───────────────
        private void BuildUI()
        {
            var canvasGo = new GameObject("WorldMapCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 450;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = NewElement("Panel", canvasGo.transform, out var panelRt);
            Stretch(panelRt);
            AddImage(_panel, new Color(0.03f, 0.03f, 0.06f, 0.96f));

            // Viewport = khung cắt có RectMask2D (map chỉ hiện trong khung).
            var vpGo = NewElement("Viewport", _panel.transform, out _viewport);
            // Viewport phủ TOÀN MÀN HÌNH (chừa lề nhỏ), stretch theo canvas.
            _viewport.anchorMin = Vector2.zero; _viewport.anchorMax = Vector2.one;
            _viewport.pivot = new Vector2(0.5f, 0.5f);
            _viewport.offsetMin = new Vector2(40, 40);    // lề trái/dưới
            _viewport.offsetMax = new Vector2(-40, -40);  // lề phải/trên
            AddImage(vpGo, new Color(0.06f, 0.06f, 0.10f, 1f));
            vpGo.AddComponent<RectMask2D>();

            // Content = lớp di chuyển/zoom, chứa toàn bộ map-space.
            var contentGo = NewElement("Content", vpGo.transform, out _content);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = new Vector2(0.5f, 0.5f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(100, 100);

            // Title + Travel + close.
            var titleGo = NewElement("Title", _panel.transform, out var titleRt);
            titleRt.anchorMin = new Vector2(0.5f, 1f); titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f); titleRt.anchoredPosition = new Vector2(0, -30);
            titleRt.sizeDelta = new Vector2(1200, 70);
            _title = titleGo.AddComponent<TextMeshProUGUI>();
            _title.alignment = TextAlignmentOptions.Center; _title.fontSize = 40;
            _title.fontStyle = FontStyles.SmallCaps; _title.characterSpacing = 6f;
            _title.color = new Color(0.95f, 0.92f, 0.8f);
            _title.raycastTarget = false;

            _travelBtn = MakeButton(_panel.transform, "TRAVEL", new Vector2(0.5f, 0f), new Vector2(0, 60), DoTravel);
            MakeButton(_panel.transform, "X (M)", new Vector2(1f, 1f), new Vector2(-70, -40), () => SetOpen(false));
        }

        // ─────────────── INPUT ───────────────
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) SetOpen(!_open);
            if (!_open) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { SetOpen(false); return; }

            // ÉP cursor hiện + mở khoá MỖI FRAME khi map mở (gameplay có thể ẩn/khoá cursor liên tục).
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // Lăn chuột = zoom THEO HƯỚNG CON TRỎ (giữ điểm dưới chuột cố định).
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                float oldZoom = _zoom;
                float newZoom = Mathf.Clamp(_zoom * (1f + scroll * 1.5f),
                                            _baseZoom * ZoomOutLimit, _baseZoom * ZoomInLimit);
                if (!Mathf.Approximately(newZoom, oldZoom))
                {
                    // Vị trí chuột trong toạ độ local của viewport (gốc = tâm viewport, vì pivot 0.5,0.5).
                    Vector2 vpLocal;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _viewport, Input.mousePosition, null, out vpLocal);

                    // Điểm trên content (chưa scale) đang nằm dưới chuột.
                    Vector2 contentPt = (vpLocal - _content.anchoredPosition) / oldZoom;

                    _zoom = newZoom;
                    _content.localScale = new Vector3(_zoom, _zoom, 1f);
                    // Giữ điểm đó tiếp tục nằm dưới chuột sau khi đổi zoom.
                    _content.anchoredPosition = vpLocal - contentPt * _zoom;
                    UpdateConstantScaleElements();
                }
            }

            // Kéo chuột phải/giữa = pan.
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                Vector2 d = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                _content.anchoredPosition += d * 12f;
            }

            // Cập nhật vị trí icon player MỖI FRAME → icon di chuyển real-time theo player (local + coop).
            if (_openMap != null)
            {
                for (int i = 0; i < _playerDots.Count; i++)
                {
                    var (rt, pc) = _playerDots[i];
                    if (rt == null || pc == null) continue;
                    rt.anchoredPosition = MapToContent(_openMap, pc.transform.position);
                }
            }
        }

        private void SetOpen(bool open)
        {
            _open = open;
            IsOpen = open;   // khóa di chuyển player khi map mở
            if (_panel != null) _panel.SetActive(open);
            if (open)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                RefreshContent();
                CenterOnPlayer();
            }
        }

        private void OnDisable() { IsOpen = false; }

        // Map-space: 1 world-unit → PixelsPerUnit px. Vị trí 1 điểm world của map m trên content.
        private Vector2 MapToContent(MapDataSO m, Vector2 world)
        {
            Vector2 local = (world - (Vector2)m.worldBounds.center) * m.worldMapScale + m.worldMapOffset;
            return local * PixelsPerUnit;
        }

        private void CenterOnPlayer()
        {
            var map = CurrentMap();

            // Zoom sao cho thấy ~15 ô fog mỗi bên (ngang) tính từ player. Quy ra world units rồi ra px.
            float cell = (map != null && map.fogCellSize > 0.01f) ? map.fogCellSize : 2.5f;
            float halfViewUnits = 15f * cell;                       // 15 ô sang mỗi bên
            // Viewport stretch full-screen → dùng bề rộng tham chiếu canvas (1920) trừ lề 2 bên (40*2).
            float viewportHalfW = (1920f - 80f) * 0.5f;
            float wantZoom = viewportHalfW / (halfViewUnits * PixelsPerUnit);
            _baseZoom = wantZoom;          // mức chuẩn (15 ô/bên) — neo cho giới hạn cuộn chuột
            _zoom = wantZoom;
            _content.localScale = new Vector3(_zoom, _zoom, 1f);

            var player = FindLocalPlayer();
            if (map != null && player != null)
            {
                Vector2 pc = MapToContent(map, player.position);
                _content.anchoredPosition = -pc * _zoom;   // đưa player vào giữa viewport
            }
            else _content.anchoredPosition = Vector2.zero;

            UpdateConstantScaleElements();
        }

        // Marker + player dot phải GIỮ KÍCH THƯỚC MÀN HÌNH không đổi khi zoom → counter-scale 1/zoom.
        private void UpdateConstantScaleElements()
        {
            float inv = _zoom > 0.01f ? 1f / _zoom : 1f;
            foreach (var go in _constantScale)
                if (go != null) go.transform.localScale = new Vector3(inv, inv, 1f);
        }

        // ─────────────── DỰNG NỘI DUNG MAP ───────────────
        private void RefreshContent()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            _constantScale.Clear();
            _selected = null; _selectedMap = null;
            if (_travelBtn != null) _travelBtn.interactable = false;

            var cur = CurrentMap();
            _title.text = cur != null ? (string.IsNullOrEmpty(cur.displayName) ? cur.sceneName : cur.displayName) : "WORLD MAP";

            if (_registry == null)
            {
                Debug.LogWarning("[WorldMap] MapRegistry NULL — tạo asset 'MapRegistry' trong thư mục Resources và kéo các MapData vào.");
                return;
            }
            foreach (var map in _registry.maps)
            {
                if (map == null) continue;
                if (map.worldBounds.size.sqrMagnitude < 0.01f) continue; // chưa bake bounds → bỏ
                DrawMap(map);
            }
            BuildPlayerDot();
        }

        private void DrawMap(MapDataSO map)
        {
            // Kích thước ảnh trên content = số world-units * PixelsPerUnit * mapScale.
            Vector2 sizePx = new Vector2(map.worldBounds.size.x, map.worldBounds.size.y) * map.worldMapScale * PixelsPerUnit;
            Vector2 centerPos = MapToContent(map, map.worldBounds.center);

            // Silhouette (nếu đã bake).
            if (map.silhouette != null)
            {
                var silGo = NewElement("Sil_" + map.sceneName, _content, out var silRt);
                silRt.anchorMin = silRt.anchorMax = new Vector2(0.5f, 0.5f);
                silRt.pivot = new Vector2(0.5f, 0.5f);
                silRt.anchoredPosition = centerPos; silRt.sizeDelta = sizePx;
                var img = silGo.AddComponent<Image>();
                img.sprite = map.silhouette; img.raycastTarget = false;
                img.color = Color.white;
                _spawned.Add(silGo);
            }

            // Fog overlay (cùng vị trí + size).
            var fogGo = NewElement("Fog_" + map.sceneName, _content, out var fogRt);
            fogRt.anchorMin = fogRt.anchorMax = new Vector2(0.5f, 0.5f);
            fogRt.pivot = new Vector2(0.5f, 0.5f);
            fogRt.anchoredPosition = centerPos; fogRt.sizeDelta = sizePx;
            var raw = fogGo.AddComponent<RawImage>();
            raw.texture = BuildFogTexture(map); raw.raycastTarget = false;
            _spawned.Add(fogGo);

            // Markers điểm rest đã khám phá.
            foreach (var cp in map.checkpoints)
                DrawMarker(map, cp);
        }

        private Texture2D BuildFogTexture(MapDataSO map)
        {
            var grid = map.FogGridSize();
            // Upscale: mỗi ô fog = SUPER pixel → có chỗ để blur cho mép mượt (không còn ô vuông sắc).
            const int SUPER = 6;
            int w = grid.x * SUPER, h = grid.y * SUPER;

            // 1) Alpha thô theo ô: chưa đi = 1 (che), đã đi = 0 (rõ). Trải đều ra super-grid.
            float[] a = new float[w * h];
            for (int gy = 0; gy < grid.y; gy++)
                for (int gx = 0; gx < grid.x; gx++)
                {
                    float v = WorldMapState.IsFogVisited(map.sceneName, gx, gy) ? 0f : 1f;
                    for (int iy = 0; iy < SUPER; iy++)
                        for (int ix = 0; ix < SUPER; ix++)
                            a[(gy * SUPER + iy) * w + (gx * SUPER + ix)] = v;
                }

            // 2) Box blur vài lần → mép sương chuyển mềm thay vì cạnh vuông.
            a = BoxBlur(a, w, h, 2);
            a = BoxBlur(a, w, h, 2);

            // 3) Đổ màu xám đục, alpha theo giá trị đã blur.
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            Color fog = new Color(0.22f, 0.22f, 0.26f, 1f);
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color(fog.r, fog.g, fog.b, a[i]);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // Box blur ngang+dọc bán kính r (lấy trung bình lân cận) — làm mềm mép sương.
        private static float[] BoxBlur(float[] src, int w, int h, int r)
        {
            var dst = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f; int n = 0;
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            sum += src[ny * w + nx]; n++;
                        }
                    dst[y * w + x] = n > 0 ? sum / n : src[y * w + x];
                }
            return dst;
        }

        private void DrawMarker(MapDataSO map, MapDataSO.CheckpointMarker cp)
        {
            if (!WorldMapState.IsCheckpointDiscovered(cp.checkpointId)) return;
            var cell = map.WorldToCell(cp.worldPos);
            if (!WorldMapState.IsFogVisited(map.sceneName, cell.x, cell.y)) return;

            var mk = NewElement("CP_" + cp.checkpointId, _content, out var rt);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = MapToContent(map, cp.worldPos);
            rt.sizeDelta = new Vector2(20, 20);
            var img = mk.AddComponent<Image>();
            img.sprite = teleIcon != null ? teleIcon : CircleSprite();
            img.color = teleIcon != null ? Color.white : new Color(1f, 0.78f, 0.2f, 1f); // vàng nếu dùng chấm mặc định
            _constantScale.Add(mk);   // giữ kích thước màn hình không đổi khi zoom

            var capCp = cp; var capMap = map;
            var btn = mk.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => SelectMarker(capMap, capCp));
            _spawned.Add(mk);
        }

        // Vẽ icon vị trí cho MỌI player (local + đồng đội coop). Icon cập nhật mỗi frame trong Update.
        private void BuildPlayerDot()
        {
            _playerDots.Clear();
            var map = CurrentMap();
            _openMap = map;
            if (map == null)
            {
                Debug.LogWarning("[WorldMap] CurrentMap null — scene chưa có MapData trong MapRegistry → không vẽ được icon player.");
                return;
            }

            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc == null) continue;
                bool isLocal = pc.HasInputAuthority;

                var dotGo = NewElement("PlayerDot", _content, out var rt);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = MapToContent(map, pc.transform.position);
                rt.sizeDelta = new Vector2(playerIconSize, playerIconSize);

                var img = dotGo.AddComponent<Image>();
                // Chọn sprite: icon theo authority (Inspector) → playerIcon chung → sprite đầu player → chấm tròn.
                Sprite icon = isLocal ? localPlayerIcon : remotePlayerIcon;
                if (icon == null) icon = playerIcon;
                if (icon == null) icon = GetPlayerSprite(pc.transform);
                if (icon != null)
                {
                    img.sprite = icon;
                    img.color = Color.white;      // icon thật → giữ nguyên hình
                }
                else
                {
                    // Chưa gán icon → chấm tròn tô màu phân biệt local/remote (theo PlayerNameTag).
                    img.sprite = CircleSprite();
                    img.color = isLocal ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.95f, 0.6f, 0.25f);
                }
                img.raycastTarget = false;
                img.preserveAspect = true;

                _spawned.Add(dotGo);
                _constantScale.Add(dotGo);   // giữ kích thước màn hình khi zoom
                _playerDots.Add((rt, pc));
            }
        }

        private static Sprite GetPlayerSprite(Transform player)
        {
            var sr = player.GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.sprite : null;
        }

        private void SelectMarker(MapDataSO map, MapDataSO.CheckpointMarker cp)
        {
            _selected = cp; _selectedMap = map;
            if (_travelBtn != null) _travelBtn.interactable = true;
            _title.text = $"{(string.IsNullOrEmpty(map.displayName) ? map.sceneName : map.displayName)}  -  {cp.checkpointId}";
        }

        private MapDataSO CurrentMap()
        {
            if (_registry == null) return null;
            string scene = SceneManager.GetActiveScene().name;
            var byName = _registry.GetByScene(scene);
            if (byName != null) return byName;

            // FALLBACK: sceneName trong MapData có thể lệch tên scene thật → tìm map mà worldBounds
            // CHỨA vị trí player local. Đảm bảo dot + center vẫn hoạt động dù cấu hình tên chưa khớp.
            var player = FindLocalPlayer();
            if (player != null)
            {
                foreach (var m in _registry.maps)
                    if (m != null && m.worldBounds.size.sqrMagnitude > 0.01f
                        && m.worldBounds.Contains(new Vector3(player.position.x, player.position.y, m.worldBounds.center.z)))
                        return m;
            }
            // Cuối cùng: map đầu tiên có bounds hợp lệ (để ít nhất vẽ được gì đó).
            foreach (var m in _registry.maps)
                if (m != null && m.worldBounds.size.sqrMagnitude > 0.01f) return m;
            return null;
        }

        private Transform FindLocalPlayer()
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (pc != null && pc.HasInputAuthority) return pc.transform;
            return null;
        }

        // Hình tròn mềm tự sinh (anti-alias mép) — dùng cho marker/player khi không gán icon.
        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int S = 64; float r = S * 0.5f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01((r - d) / 1.5f); // mép mượt 1.5px
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        // ─────────────── TRAVEL (solo + coop) ───────────────
        private void DoTravel()
        {
            if (_selected == null || _selectedMap == null)
            {
                Debug.LogWarning("[WorldMap] TRAVEL: chưa chọn checkpoint nào.");
                return;
            }
            var cp = _selected.Value;
            SetOpen(false);

            // ROBUST: nếu checkpoint đích THỰC SỰ tồn tại trong scene đang chạy (khớp DisplayName) →
            // teleport CÙNG SCENE, bất kể chuỗi sceneName trong MapData có khớp hay không (tránh bug
            // tên scene lệch khiến travel cùng-scene bị hiểu nhầm là cross-scene → reload sai).
            var here = FindCheckpointInScene(cp.checkpointId);
            if (here != null)
            {
                var local = FindLocalController();
                if (local != null) local.RpcRequestFastTravelToCheckpoint(here.transform.position, cp.checkpointId);
                else Debug.LogWarning("[WorldMap] TRAVEL: không tìm thấy local PlayerController.");
                return;
            }

            // Khác scene → đặt pending + load scene đích.
            string targetScene = _selectedMap.sceneName;
            WorldMapState.PendingTravelScene = targetScene;
            WorldMapState.PendingTravelCheckpointId = cp.checkpointId;
            var launcher = FindFirstObjectByType<Attrition.Networking.NetworkLauncher>();
            if (launcher != null) launcher.BeginGameplay(targetScene);
            else Debug.LogWarning("[WorldMap] TRAVEL: không tìm thấy NetworkLauncher để load scene '" + targetScene + "'.");
        }

        private Attrition.Gameplay.World.Checkpoint FindCheckpointInScene(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var cp in FindObjectsByType<Attrition.Gameplay.World.Checkpoint>(FindObjectsSortMode.None))
                if (cp != null && cp.DisplayName == id) return cp;
            return null;
        }

        private PlayerController FindLocalController()
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (pc != null && pc.HasInputAuthority) return pc;
            return null;
        }

        // ─────────────── UI HELPERS ───────────────
        private static GameObject NewElement(string name, Transform parent, out RectTransform rt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            rt = go.AddComponent<RectTransform>();
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Image AddImage(GameObject go, Color c)
        {
            var img = go.AddComponent<Image>();
            img.color = c;
            return img;
        }

        private Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 offset, System.Action onClick)
        {
            var go = NewElement("Btn_" + label, parent, out var rt);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = offset; rt.sizeDelta = new Vector2(190, 60);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.26f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var txtGo = NewElement("Label", go.transform, out var txtRt);
            Stretch(txtRt);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = label; txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 26; txt.color = Color.white; txt.raycastTarget = false;
            return btn;
        }
    }
}
