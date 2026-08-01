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
        statusText.text = "Waiting for browser sign-in...";
        
        // Mở cổng lắng nghe
        if (LocalAuthServer.Instance != null)
            LocalAuthServer.Instance.StartListening();

        // Mở trang Login của web Attrition (frontend) kèm client=unity. URL lấy từ APIManager
        // (override qua ATTRITION_WEB_URL / StreamingAssets/web_base_url.txt) để test từ xa khỏi build lại.
        Application.OpenURL(APIManager.Instance.WebLoginUrl);
    }

    private void HandleTokenReceived(string token, string refreshToken)
    {
        statusText.text = "Verifying Google account...";
        loginButton.interactable = false;
        if (googleLoginButton != null) googleLoginButton.interactable = false;

        StartCoroutine(APIManager.Instance.LoginWithToken(token, refreshToken, (userId) => {
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
                statusText.text = "<color=red>Google sign-in failed.</color>";
            }
        }));
    }

    private void HandleLogin()
    {
        string email = emailInputField.text;
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            statusText.text = "Please enter both email and password.";
            return;
        }

        statusText.text = "Signing in...";
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
                statusText.text = "<color=red>Incorrect email or password.</color>";
            }
        }));
    }
}