using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;

public class APIManager : MonoBehaviour
{
    public static APIManager Instance;
    
    private string baseUrl = "http://localhost:8080/api";
    /// <summary>Base URL gateway (đọc-only) để các provider khác (EnemyStatProvider) dùng chung, tránh lệch port.</summary>
    public string BaseUrl => baseUrl;
    public string AccessToken { get; private set; }
    /// <summary>Username tài khoản đã đăng nhập (từ Postgres). Dùng làm tên player hiển thị.</summary>
    public string Username { get; private set; }

    void Awake()
    {
        // Singleton bền: sống xuyên Menu → scene gameplay. Thiếu DontDestroyOnLoad thì khi Fusion
        // load scene gameplay, APIManager (ở scene Menu) bị huỷ → save online per-room mất chỗ gọi.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadInternalKey();
    }

    /// <summary>
    /// Nạp X-Internal-Key lúc chạy (KHÔNG hardcode trong source / không commit lên git).
    /// Thứ tự ưu tiên:
    ///   1. Biến môi trường ATTRITION_INTERNAL_KEY — tiện cho Editor/dev.
    ///   2. File StreamingAssets/internal_key.txt — ship theo build máy host, đã gitignore.
    ///   3. Giá trị field Inspector (fallback; để trống khi build thật).
    /// Key phải KHỚP INTERNAL_API_KEY của backend (.env) thì các call internal mới qua (không 401).
    /// </summary>
    private void LoadInternalKey()
    {
        var env = System.Environment.GetEnvironmentVariable("ATTRITION_INTERNAL_KEY");
        if (!string.IsNullOrWhiteSpace(env)) { InternalKey = env.Trim(); return; }

        try
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "internal_key.txt");
            if (System.IO.File.Exists(path))
            {
                string fromFile = System.IO.File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(fromFile)) { InternalKey = fromFile; return; }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[APIManager] Đọc internal_key.txt lỗi: {e.Message}");
        }

        if (string.IsNullOrWhiteSpace(InternalKey))
            Debug.LogWarning("[APIManager] InternalKey trống — set biến môi trường ATTRITION_INTERNAL_KEY " +
                             "hoặc tạo Assets/StreamingAssets/internal_key.txt. Call internal sẽ bị 401.");
    }
    [System.Serializable]
    
    public class UserDto
    {
        public string id;
        public string username;
        public string displayName;
    }

    public class AuthResponseData
    {
        public string accessToken;
        public string refreshToken;
        public UserDto user;
    }

    public class ApiResponse<T>
    {
        public bool success;
        public T data;
        public string error;
    }

    public IEnumerator Login(string email, string password, System.Action<string> callback)
    {
        var loginData = new { Username = email, Password = password };
        string json = JsonConvert.SerializeObject(loginData);

        using (UnityWebRequest request = new UnityWebRequest($"{baseUrl}/Auth/login", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<ApiResponse<AuthResponseData>>(request.downloadHandler.text);
                
                if (response.success && response.data != null && response.data.user != null)
                {
                    string userId = response.data.user.id;
                    AccessToken = response.data.accessToken;
                    Username = response.data.user.username;
                    callback?.Invoke(userId);
                }
                else
                {
                    Debug.LogError("Login Fail: " + response.error);
                    callback?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError("Login Fail: " + request.error);
                callback?.Invoke(null);
            }
        }
    }

   
    public async Task<Player> GetCharacterData(string userId)
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/Character/{userId}"))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return JsonConvert.DeserializeObject<Player>(request.downloadHandler.text);
            }
            return null;
        }
    }
    public class CharacterSummaryDto
    {
        public string id;
        public string ownerId;
        public string name;
        public string archetype;
        public SnapshotDto latestSnapshot;
    }

    public class SnapshotDto
    {
        public int level;
        public int hp;
        public int maxHp;
        public int gold;
        public int playtimeSeconds;
    }

    // Khớp CharacterDetailDto của web — dùng để đọc lại inventory/equipment khi vào game coop.
    public class CharacterDetailDto
    {
        public string id;
        public string ownerId;
        public string name;
        public string archetype;
        public string inventoryJson;
        public string equipmentJson;
        public string questsJson;
    }

    /// <summary>Đọc chi tiết 1 nhân vật (gồm inventoryJson) theo characterId. JWT.</summary>
    public IEnumerator GetCharacterDetail(string characterId, System.Action<CharacterDetailDto> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/characters/{characterId}"))
        {
            if (!string.IsNullOrEmpty(AccessToken))
                request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<ApiResponse<CharacterDetailDto>>(request.downloadHandler.text);
                callback?.Invoke(response?.data);
            }
            else
            {
                Debug.LogError("GetCharacterDetail Fail: " + request.error);
                callback?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// HOST đọc character detail của BẤT KỲ player nào (kể cả client) qua X-Internal-Key — bỏ qua
    /// ownership guard của endpoint JWT. Dùng để host nạp đồ cho nhân vật client trong coop.
    /// </summary>
    public IEnumerator GetCharacterDetailInternal(string characterId, System.Action<CharacterDetailDto> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/internal/characters/{characterId}"))
        {
            request.SetRequestHeader("X-Internal-Key", InternalKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<ApiResponse<CharacterDetailDto>>(request.downloadHandler.text);
                callback?.Invoke(response?.data);
            }
            else
            {
                Debug.LogError("GetCharacterDetailInternal Fail: " + request.error + " | " + request.downloadHandler.text);
                callback?.Invoke(null);
            }
        }
    }

    public IEnumerator GetCharacters(System.Action<System.Collections.Generic.List<CharacterSummaryDto>> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/characters"))
        {
            if (!string.IsNullOrEmpty(AccessToken))
                request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<ApiResponse<System.Collections.Generic.List<CharacterSummaryDto>>>(request.downloadHandler.text);
                callback?.Invoke(response?.data);
            }
            else
            {
                Debug.LogError("GetCharacters Fail: " + request.error);
                callback?.Invoke(null);
            }
        }
    }

    public IEnumerator DeleteCharacter(string characterId, System.Action<bool> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Delete($"{baseUrl}/characters/{characterId}"))
        {
            if (!string.IsNullOrEmpty(AccessToken))
                request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(true);
            }
            else
            {
                Debug.LogError("DeleteCharacter Fail: " + request.error);
                callback?.Invoke(false);
            }
        }
    }

    // ─── SAVE ONLINE: post snapshot tiến trình lên server (Postgres) ───
    // Khớp SnapshotIngestRequest của web. Guard bằng X-Internal-Key (game server tin cậy).
    [System.Serializable]
    public class SnapshotIngestRequest
    {
        public string ownerId;
        public string characterId;   // null/empty → server resolve theo (owner, name)
        public string name;
        public string archetype;
        public int level;
        public int hp;
        public int maxHp;
        public int gold;
        public bool isAlive;
        public string roomCode;
        public string eventType;     // "rest" | "quit" | "death" | "levelup"
        public int playtimeSeconds;
        public string inventoryJson; // JSON inventory (null = không đổi)
        public string equipmentJson; // JSON trang bị đang mặc
        public string questsJson;    // JSON tiến trình quest world-state (host gom, null = không đổi)
    }

    [Tooltip("Khóa nội bộ khớp với web (X-Internal-Key). ĐỂ TRỐNG khi build thật — nạp qua " +
             "ATTRITION_INTERNAL_KEY (env) hoặc StreamingAssets/internal_key.txt. Chỉ điền tạm khi test Editor.")]
    public string InternalKey = "";

    public IEnumerator PostSnapshot(SnapshotIngestRequest req, System.Action<bool> callback)
    {
        string json = JsonConvert.SerializeObject(req);
        using (UnityWebRequest request = new UnityWebRequest($"{baseUrl}/internal/characters/snapshot", "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Internal-Key", InternalKey);

            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success;
            if (!ok) Debug.LogError("PostSnapshot Fail: " + request.error + " | " + request.downloadHandler.text);
            callback?.Invoke(ok);
        }
    }

    // ─── SESSIONS (room bền) — internal API, guard X-Internal-Key ───
    // Khớp DTO của Character.Service. ASP.NET serialize PascalCase → camelCase nên field ở đây
    // để camelCase. Model-binding của ASP.NET case-insensitive nên request gửi camelCase vẫn khớp.

    [System.Serializable]
    public class CharacterSessionDto
    {
        public string characterId;
        public string sessionId;
        public short playerRole;
        public int currentLevel;
        public int currentExp;
        public string allocatedPointsJson;
        public int maxHp;
        public int currentHp;
        public int maxMana;
        public int currentMana;
        public int maxStamina;
        public int potionMaxFlasks;
        public float attackSpeed;
        public float posX;
        public float posY;
        public string lastRestPointId;
        public string inventoryJson;
        public string equipmentJson;
    }

    [System.Serializable]
    public class WorldStateDto
    {
        public string eventId;
        public short stateValue;
        public int progress;
    }

    [System.Serializable]
    public class SessionDetailDto
    {
        public string id;
        public string ownerId;
        public string roomCode;
        public string name;
        public bool isMultiplayer;
        public int playTimeSeconds;
        public string currentScene;
        public System.Collections.Generic.List<CharacterSessionDto> characters;
        public System.Collections.Generic.List<WorldStateDto> worldStates;
    }

    [System.Serializable]
    public class CreateSessionRequest
    {
        public string ownerId;
        public string name;
        public string roomCode;     // null/empty → server sinh mã cố định mới
        public string currentScene;
    }

    [System.Serializable]
    public class UpdateSessionRequest
    {
        public string sessionId;
        public int playTimeSeconds;
        public string currentScene;
    }

    [System.Serializable]
    public class SaveCharacterSessionRequest
    {
        public string characterId;
        public string sessionId;
        public short playerRole;
        public int currentLevel;
        public int currentExp;
        public string allocatedPointsJson;
        public int maxHp;
        public int currentHp;
        public int maxMana;
        public int currentMana;
        public int maxStamina;
        public int potionMaxFlasks;
        public float attackSpeed;
        public float posX;
        public float posY;
        public string lastRestPointId;
        public string inventoryJson; // null = giữ nguyên (không xoá đồ khi save không kèm)
        public string equipmentJson;
    }

    [System.Serializable]
    public class SaveWorldStateRequest
    {
        public string sessionId;
        public string eventId;
        public short stateValue;
        public int progress;
    }

    /// <summary>Host tạo phòng mới (hoặc reopen nếu gửi roomCode đã có của mình). Trả room đầy đủ.</summary>
    public IEnumerator CreateOrReopenSession(CreateSessionRequest req, System.Action<SessionDetailDto> callback)
        => PostSession("internal/sessions", req, callback);

    /// <summary>Host cập nhật playtime/scene của phòng khi save/quit.</summary>
    public IEnumerator UpdateSessionMeta(UpdateSessionRequest req, System.Action<SessionDetailDto> callback)
        => PostSession("internal/sessions/meta", req, callback);

    /// <summary>Host lưu tiến trình 1 player trong phòng (upsert theo characterId+sessionId).</summary>
    public IEnumerator SaveCharacterSession(SaveCharacterSessionRequest req, System.Action<bool> callback)
        => PostSessionOk("internal/sessions/character", req, callback);

    /// <summary>Host lưu tiến trình quest của phòng (upsert theo sessionId+eventId).</summary>
    public IEnumerator SaveWorldState(SaveWorldStateRequest req, System.Action<bool> callback)
        => PostSessionOk("internal/sessions/world-state", req, callback);

    /// <summary>Host đọc full room theo id (gồm tiến trình mọi player + quest) để load hành trình đã lưu.</summary>
    public IEnumerator GetSession(string sessionId, System.Action<SessionDetailDto> callback)
        => GetSessionInternal($"internal/sessions/{sessionId}", callback);

    /// <summary>Client tra phòng theo mã host chia sẻ, để join.</summary>
    public IEnumerator GetSessionByCode(string roomCode, System.Action<SessionDetailDto> callback)
        => GetSessionInternal($"internal/sessions/by-code/{UnityWebRequest.EscapeURL(roomCode)}", callback);

    // ─── helpers nội bộ cho session ───
    private IEnumerator PostSession(string path, object req, System.Action<SessionDetailDto> callback)
    {
        string json = JsonConvert.SerializeObject(req);
        using (UnityWebRequest request = new UnityWebRequest($"{baseUrl}/{path}", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Internal-Key", InternalKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonConvert.DeserializeObject<ApiResponse<SessionDetailDto>>(request.downloadHandler.text);
                callback?.Invoke(resp != null && resp.success ? resp.data : null);
            }
            else
            {
                Debug.LogError($"[Session] POST {path} Fail: {request.error} | {request.downloadHandler.text}");
                callback?.Invoke(null);
            }
        }
    }

    private IEnumerator PostSessionOk(string path, object req, System.Action<bool> callback)
    {
        string json = JsonConvert.SerializeObject(req);
        using (UnityWebRequest request = new UnityWebRequest($"{baseUrl}/{path}", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Internal-Key", InternalKey);

            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success;
            if (!ok) Debug.LogError($"[Session] POST {path} Fail: {request.error} | {request.downloadHandler.text}");
            callback?.Invoke(ok);
        }
    }

    private IEnumerator GetSessionInternal(string path, System.Action<SessionDetailDto> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/{path}"))
        {
            request.SetRequestHeader("X-Internal-Key", InternalKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonConvert.DeserializeObject<ApiResponse<SessionDetailDto>>(request.downloadHandler.text);
                callback?.Invoke(resp != null && resp.success ? resp.data : null);
            }
            else
            {
                Debug.LogError($"[Session] GET {path} Fail: {request.error} | {request.downloadHandler.text}");
                callback?.Invoke(null);
            }
        }
    }

    // ─── SESSION SUMMARY (player-facing, JWT) ───

    [System.Serializable]
    public class SessionSummaryDto
    {
        public string id;
        public string ownerId;
        public string roomCode;
        public string name;
        public bool isMultiplayer;
        public int playTimeSeconds;
        public string currentScene;
        public string createdAt;
        public string updatedAt;
        public string lastPlayedAt;
        public int characterCount;
    }

    /// <summary>Host lấy danh sách phòng mình sở hữu (JWT). Dùng cho màn chọn session.</summary>
    public IEnumerator GetMySessions(System.Action<System.Collections.Generic.List<SessionSummaryDto>> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/sessions"))
        {
            if (!string.IsNullOrEmpty(AccessToken))
                request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonConvert.DeserializeObject<ApiResponse<System.Collections.Generic.List<SessionSummaryDto>>>(request.downloadHandler.text);
                callback?.Invoke(resp != null && resp.success ? resp.data : null);
            }
            else
            {
                Debug.LogError($"[Session] GetMySessions Fail: {request.error} | {request.downloadHandler.text}");
                callback?.Invoke(null);
            }
        }
    }

    // ─── DELETE SESSION (internal, X-Internal-Key) ───

    /// <summary>Host deletes a room entirely.</summary>
    public IEnumerator DeleteSession(string sessionId, System.Action<bool> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Delete($"{baseUrl}/internal/sessions/{sessionId}"))
        {
            request.SetRequestHeader("X-Internal-Key", InternalKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback?.Invoke(true);
            }
            else
            {
                Debug.LogError($"[Session] DELETE session {sessionId} Fail: {request.error} | {request.downloadHandler.text}");
                callback?.Invoke(false);
            }
        }
    }
}
