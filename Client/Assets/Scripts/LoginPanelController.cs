using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class LoginPanelController : MonoBehaviour
{
    public TMP_InputField loginInput;
    public TMP_InputField passwordInput;
    public UIMessage uiMessage;

    public string loginUrl;

    private void Awake()
    {
        loginUrl = ApiConfig.LoginUrl;
    }

    [Header("Scenes")]
    [SerializeField] private string homeSceneName = "Home";
    [SerializeField] private string profileSettingSceneName = "ProfileSetting";

    private void OnEnable()
    {
        ClearInputs();
    }

    public void OnClickLogin()
    {
        StartCoroutine(LoginCoroutine());
    }

    public void ClearInputs()
    {
        loginInput.text = "";
        passwordInput.text = "";

        loginInput.ForceLabelUpdate();
        passwordInput.ForceLabelUpdate();

        if (uiMessage != null)
        {
            uiMessage.ClearMessage();
        }
    }

    private IEnumerator LoginCoroutine()
    {
        uiMessage.ClearMessage();

        if (string.IsNullOrWhiteSpace(loginInput.text) ||
            string.IsNullOrWhiteSpace(passwordInput.text))
        {
            uiMessage.ShowMessage("Vui lòng nhập đầy đủ thông tin", Color.yellow);
            yield break;
        }

        LoginRequestData data = new LoginRequestData
        {
            login = loginInput.text.Trim(),
            password = passwordInput.text
        };

        string json = JsonUtility.ToJson(data);

        using UnityWebRequest request = new UnityWebRequest(loginUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler.text;
        Debug.Log("Login Response: " + responseText);
        Debug.Log("Response Code: " + request.responseCode);

        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            uiMessage.ShowMessage("Không kết nối được tới server", Color.red);
            yield break;
        }

        LoginApiResponse response = null;

        if (!string.IsNullOrEmpty(responseText))
        {
            response = JsonUtility.FromJson<LoginApiResponse>(responseText);
        }

        if (request.responseCode >= 200 && request.responseCode < 300)
        {
            if (response != null && response.success)
            {
                SaveLoginData(response);

                uiMessage.ShowMessage("Đăng nhập thành công", Color.green);

                yield return new WaitForSeconds(1f);

                string nextSceneName = GetNextSceneAfterLogin();

                ClearInputs();

                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                uiMessage.ShowMessage("Đăng nhập thất bại", Color.red);
            }

            yield break;
        }

        if (response != null && !string.IsNullOrEmpty(response.message))
        {
            uiMessage.ShowMessage("Tên đăng nhập hoặc mật khẩu sai", Color.red);
        }
        else
        {
            uiMessage.ShowMessage("Đăng nhập thất bại", Color.red);
        }
    }

    private void SaveLoginData(LoginApiResponse response)
    {
        string userId = response.data.userId;
        string email = response.data.email;
        string username = response.data.username;
        string displayName = response.data.displayName ?? "";

        PlayerPrefs.SetString("token", response.data.token);
        PlayerPrefs.SetString("userId", userId);
        PlayerPrefs.SetString("email", email);
        PlayerPrefs.SetString("username", username);
        PlayerPrefs.SetString("displayName", displayName);

        // Key riêng cho từng tài khoản.
        // Dùng userId là ổn nhất vì username/email có thể đổi sau này.
        PlayerPrefs.SetString("CurrentAccountKey", userId);

        PlayerPrefs.Save();
    }

    private string GetNextSceneAfterLogin()
    {
        string accountKey = PlayerPrefs.GetString("CurrentAccountKey", "");

        if (string.IsNullOrEmpty(accountKey))
        {
            return profileSettingSceneName;
        }

        string profileCompletedKey = "ProfileCompleted_" + accountKey;

        bool hasCompletedProfile = PlayerPrefs.GetInt(profileCompletedKey, 0) == 1;

        if (hasCompletedProfile)
        {
            return homeSceneName;
        }

        return profileSettingSceneName;
    }
}