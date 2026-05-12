using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;

public class APIManager : MonoBehaviour
{
    public static APIManager Instance;
    
    private string baseUrl = "http://localhost:5130/api";

    void Awake() => Instance = this;
    [System.Serializable]
    
    public class LoginResponse
    {
        public string userId;
        public string fullName;
    }

    public IEnumerator Login(string email, string password, System.Action<string> callback)
    {
        var loginData = new { Email = email, Password = password };
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
                
                var response = JsonConvert.DeserializeObject<LoginResponse>(request.downloadHandler.text);
                string userId = response.userId;
                callback?.Invoke(userId);
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
}
