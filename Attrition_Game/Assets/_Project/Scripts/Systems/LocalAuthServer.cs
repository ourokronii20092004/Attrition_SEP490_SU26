using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public class LocalAuthServer : MonoBehaviour
{
    public static LocalAuthServer Instance { get; private set; }

    [Header("Events")]
    [Tooltip("Sự kiện được gọi khi nhận được Token từ trình duyệt (access token, refresh token)")]
    public TokenReceivedEvent OnTokenReceived;

    /// <summary>access token + refresh token (refresh có thể rỗng nếu web không gửi).</summary>
    [System.Serializable]
    public class TokenReceivedEvent : UnityEngine.Events.UnityEvent<string, string> { }

    private HttpListener _listener;
    private const string ServerUrl = "http://localhost:52000/";
    private bool _isListening = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        StopListening();
    }

    private void OnApplicationQuit()
    {
        StopListening();
    }

    /// <summary>
    /// Bắt đầu mở mini server trên máy để chờ trình duyệt gửi Token về
    /// </summary>
    public async void StartListening()
    {
        if (_isListening) return;

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(ServerUrl);
            _listener.Start();
            _isListening = true;
            Debug.Log($"[LocalAuthServer] Đã mở cổng {ServerUrl} để chờ đăng nhập...");

            // Lắng nghe Request ở một luồng khác để không làm treo Game
            await ListenForRequestsAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalAuthServer] Lỗi khi mở server: {ex.Message}");
            StopListening();
        }
    }

    public void StopListening()
    {
        if (!_isListening) return;

        _isListening = false;
        if (_listener != null)
        {
            if (_listener.IsListening)
                _listener.Stop();
            _listener.Close();
            _listener = null;
        }
        Debug.Log("[LocalAuthServer] Đã đóng cổng lắng nghe.");
    }

    private async Task ListenForRequestsAsync()
    {
        while (_isListening && _listener != null && _listener.IsListening)
        {
            try
            {
                // Task này sẽ chặn cho tới khi có một request HTTP bay vào
                HttpListenerContext context = await _listener.GetContextAsync();

                // Lấy URL mà trình duyệt gửi đến (VD: http://localhost:52000/?token=abc&refresh=xyz)
                string token = context.Request.QueryString["token"];
                string refresh = context.Request.QueryString["refresh"];

                // Gửi phản hồi lại cho trình duyệt báo thành công
                SendSuccessResponse(context.Response);

                if (!string.IsNullOrEmpty(token))
                {
                    Debug.Log($"[LocalAuthServer] Đã bắt được token (refresh: {(string.IsNullOrEmpty(refresh) ? "không" : "có")}).");

                    // Đẩy sự kiện về Main Thread qua biến cờ để Update() gọi OnTokenReceived.
                    _pendingToken = token;
                    _pendingRefresh = refresh;
                }
                else
                {
                    Debug.LogWarning("[LocalAuthServer] Có request tới nhưng không có token!");
                }

                // Nhận được token rồi thì tự động đóng server luôn, vì xong việc rồi
                StopListening();
                break;
            }
            catch (HttpListenerException)
            {
                // Bị ném ra khi Listener bị ép đóng (Stop), ta chỉ cần thoát vòng lặp
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalAuthServer] Lỗi khi xử lý Request: {ex.Message}");
            }
        }
    }

    private string _pendingToken = null;
    private string _pendingRefresh = null;

    private void Update()
    {
        if (_pendingToken != null)
        {
            string tokenToPass = _pendingToken;
            string refreshToPass = _pendingRefresh;
            _pendingToken = null; // Reset để không gọi 2 lần
            _pendingRefresh = null;
            OnTokenReceived?.Invoke(tokenToPass, refreshToPass);
        }
    }

    private void SendSuccessResponse(HttpListenerResponse response)
    {
        try
        {
            string html = @"
                <!DOCTYPE html>
                <html lang='vi'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Đăng nhập thành công</title>
                    <style>
                        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background-color: #1a1a2e; color: #e94560; }
                        .container { text-align: center; background: rgba(255, 255, 255, 0.1); padding: 40px; border-radius: 12px; box-shadow: 0 4px 30px rgba(0, 0, 0, 0.1); backdrop-filter: blur(5px); border: 1px solid rgba(255, 255, 255, 0.3); }
                        h1 { margin-top: 0; }
                        p { color: #f5f5f5; }
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h1>Đăng Nhập Thành Công!</h1>
                        <p>Trò chơi đã nhận được tín hiệu. Bạn có thể đóng tab này lại và quay lại trò chơi.</p>
                    </div>
                    <script>
                        // Cố gắng tự động đóng tab sau 2 giây
                        setTimeout(() => {
                            window.close();
                        }, 2000);
                    </script>
                </body>
                </html>";

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "text/html; charset=UTF-8";
            
            using (var output = response.OutputStream)
            {
                output.Write(buffer, 0, buffer.Length);
            }
            response.Close();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalAuthServer] Lỗi khi gửi response: {ex.Message}");
        }
    }
}
