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
    public string AccessToken { get; private set; }

    void Awake() => Instance = this;
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
    }

    [Tooltip("Khóa nội bộ khớp với web (X-Internal-Key). Đặt qua config, không hardcode khi build thật.")]
    public string InternalKey = "dev-internal-key";

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
}
