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

    public string loginUrl = "https://localhost:7193/api/Auth/login";

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
            uiMessage.ClearMessage();
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
                PlayerPrefs.SetString("token", response.data.token);
                PlayerPrefs.SetString("userId", response.data.userId);
                PlayerPrefs.SetString("email", response.data.email);
                PlayerPrefs.SetString("username", response.data.username);
                PlayerPrefs.SetString("displayName", response.data.displayName ?? "");
                PlayerPrefs.Save();

                uiMessage.ShowMessage("Đăng nhập thành công", Color.green);

                yield return new WaitForSeconds(1f);

                ClearInputs();
                SceneManager.LoadScene("Home");
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
}