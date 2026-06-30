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
                    <meta name='viewport' content='width=device-width, initial-scale=1'>
                    <title>Attrition — Đăng nhập thành công</title>
                    <style>
                        :root { --bg:#070b09; --surface:#0d1310; --border:#2a352e; --fg:#e7efe9; --fg-muted:#93a39a; --accent:#38e8a0; --accent-fg:#04130c; }
                        * { box-sizing: border-box; }
                        body { font-family: 'Plus Jakarta Sans', 'Segoe UI', Tahoma, sans-serif; display:flex; justify-content:center; align-items:center; min-height:100vh; margin:0; background:var(--bg); color:var(--fg); overflow:hidden; }
                        .glow { position:fixed; left:50%; top:38%; width:420px; height:420px; transform:translate(-50%,-50%); background:var(--accent); opacity:0.14; filter:blur(120px); border-radius:50%; pointer-events:none; }
                        .card { position:relative; text-align:center; background:var(--surface); padding:48px 40px; border-radius:18px; border:1px solid var(--border); box-shadow:0 0 60px -12px rgba(56,232,160,0.35); max-width:380px; animation:rise .5s cubic-bezier(.2,.8,.2,1); }
                        @keyframes rise { from { opacity:0; transform:translateY(14px); } to { opacity:1; transform:none; } }
                        .badge { width:64px; height:64px; margin:0 auto 22px; border-radius:50%; background:var(--accent); display:flex; align-items:center; justify-content:center; box-shadow:0 0 28px -4px var(--accent); }
                        .badge svg { width:32px; height:32px; stroke:var(--accent-fg); stroke-width:3; fill:none; stroke-linecap:round; stroke-linejoin:round; }
                        .badge svg path { stroke-dasharray:24; stroke-dashoffset:24; animation:draw .5s .3s forwards ease-out; }
                        @keyframes draw { to { stroke-dashoffset:0; } }
                        h1 { margin:0 0 10px; font-size:1.55rem; letter-spacing:-0.02em; }
                        .brand { color:var(--accent); font-weight:700; letter-spacing:0.18em; font-size:0.72rem; text-transform:uppercase; margin-bottom:18px; }
                        p { color:var(--fg-muted); line-height:1.6; margin:0; font-size:0.95rem; }
                        .hint { margin-top:24px; font-size:0.8rem; color:var(--fg-muted); opacity:0.7; }
                    </style>
                </head>
                <body>
                    <div class='glow'></div>
                    <div class='card'>
                        <div class='brand'>ATTRITION</div>
                        <div class='badge'><svg viewBox='0 0 24 24'><path d='M5 13l4 4L19 7'/></svg></div>
                        <h1>Đăng nhập thành công</h1>
                        <p>Trò chơi đã nhận được tín hiệu. Bạn có thể đóng tab này và quay lại Attrition để tiếp tục.</p>
                        <div class='hint'>Tab sẽ tự đóng sau giây lát…</div>
                    </div>
                    <script>
                        setTimeout(() => { window.close(); }, 2000);
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
