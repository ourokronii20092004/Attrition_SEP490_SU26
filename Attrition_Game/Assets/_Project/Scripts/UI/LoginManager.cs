using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class LoginManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField emailInputField;
    public TMP_InputField passwordInputField;
    public Button loginButton;
    public Button googleLoginButton;
    public TextMeshProUGUI statusText;
    [Header("UI Panels")]
    public GameObject loginPanel;
    public GameObject lobbyPanel;
    
    private void Start()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(HandleLogin);
            
        if (googleLoginButton != null)
            googleLoginButton.onClick.AddListener(HandleGoogleLogin);

        if (LocalAuthServer.Instance != null)
            LocalAuthServer.Instance.OnTokenReceived.AddListener(HandleTokenReceived);
    }

    private void OnDestroy()
    {
        if (LocalAuthServer.Instance != null)
            LocalAuthServer.Instance.OnTokenReceived.RemoveListener(HandleTokenReceived);
    }

    private void HandleGoogleLogin()
    {
        statusText.text = "Đang chờ đăng nhập từ trình duyệt...";
        
        // Mở cổng lắng nghe
        if (LocalAuthServer.Instance != null)
            LocalAuthServer.Instance.StartListening();

        // Mở thẳng trang Login của web Attrition (Frontend) kèm tham số client=unity
        // Nếu web của bạn deploy ở link khác thì đổi localhost:3000 thành domain thực tế.
        Application.OpenURL("http://localhost:3000/login?client=unity");
    }

    private void HandleTokenReceived(string token)
    {
        statusText.text = "Đang xác thực tài khoản Google...";
        loginButton.interactable = false;
        if (googleLoginButton != null) googleLoginButton.interactable = false;

        StartCoroutine(APIManager.Instance.LoginWithToken(token, (userId) => {
            loginButton.interactable = true;
            if (googleLoginButton != null) googleLoginButton.interactable = true;

            if (!string.IsNullOrEmpty(userId))
            {
                PlayerPrefs.SetString("SavedUserId", userId);
                PlayerPrefs.Save();

                loginPanel.SetActive(false); 
                lobbyPanel.SetActive(true);  
            }
            else
            {
                statusText.text = "<color=red>Đăng nhập Google thất bại!</color>";
            }
        }));
    }

    private void HandleLogin()
    {
        string email = emailInputField.text;
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            statusText.text = "Vui lòng nhập đầy đủ thông tin!";
            return;
        }

        statusText.text = "Đang đăng nhập...";
        loginButton.interactable = false;

        StartCoroutine(APIManager.Instance.Login(email, password, (userId) => {
            loginButton.interactable = true;

            if (!string.IsNullOrEmpty(userId))
            {
                PlayerPrefs.SetString("SavedUserId", userId);
                PlayerPrefs.Save();

                loginPanel.SetActive(false); 
                lobbyPanel.SetActive(true);  
            }
            else
            {
                statusText.text = "<color=red>Sai tài khoản hoặc mật khẩu!</color>";
            }
        }));
    }
}