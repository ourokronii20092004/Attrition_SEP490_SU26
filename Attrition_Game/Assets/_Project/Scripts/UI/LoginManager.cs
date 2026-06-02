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
    public TextMeshProUGUI statusText;
    [Header("UI Panels")]
    public GameObject loginPanel;
    public GameObject lobbyPanel;
    private void Start()
    {
        loginButton.onClick.AddListener(HandleLogin);
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