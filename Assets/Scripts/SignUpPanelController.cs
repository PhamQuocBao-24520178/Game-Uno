using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class SignUpPanelController : MonoBehaviour
{
    public TMP_InputField usernameInput;
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;
    public UIMessage uiMessage;

    public string registerUrl = "https://localhost:7193/api/Auth/register";

    private void OnEnable()
    {
        ClearInputs();
    }

    public void OnClickRegister()
    {
        StartCoroutine(RegisterCoroutine());
    }

    public void ClearInputs()
    {
        usernameInput.text = "";
        emailInput.text = "";
        passwordInput.text = "";
        confirmPasswordInput.text = "";

        usernameInput.ForceLabelUpdate();
        emailInput.ForceLabelUpdate();
        passwordInput.ForceLabelUpdate();
        confirmPasswordInput.ForceLabelUpdate();

        if (uiMessage != null)
            uiMessage.ClearMessage();
    }

    private IEnumerator RegisterCoroutine()
    {
        uiMessage.ClearMessage();

        if (string.IsNullOrWhiteSpace(usernameInput.text) ||
            string.IsNullOrWhiteSpace(emailInput.text) ||
            string.IsNullOrWhiteSpace(passwordInput.text) ||
            string.IsNullOrWhiteSpace(confirmPasswordInput.text))
        {
            uiMessage.ShowMessage("Vui lòng nhập đầy đủ thông tin", Color.yellow);
            yield break;
        }

        RegisterRequestData data = new RegisterRequestData
        {
            username = usernameInput.text.Trim(),
            email = emailInput.text.Trim(),
            password = passwordInput.text,
            confirmPassword = confirmPasswordInput.text
        };

        string json = JsonUtility.ToJson(data);

        using UnityWebRequest request = new UnityWebRequest(registerUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler.text;
        Debug.Log("Register Response: " + responseText);
        Debug.Log("Response Code: " + request.responseCode);

        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            uiMessage.ShowMessage("Không kết nối được tới server", Color.red);
            yield break;
        }

        SimpleApiResponse response = null;

        if (!string.IsNullOrEmpty(responseText))
        {
            response = JsonUtility.FromJson<SimpleApiResponse>(responseText);
        }

        if (request.responseCode >= 200 && request.responseCode < 300)
        {
            if (response != null && response.success)
            {
                uiMessage.ShowMessage("Đăng ký thành công", Color.green);
            }
            else
            {
                uiMessage.ShowMessage("Đăng ký thất bại", Color.red);
            }

            yield break;
        }

        if (response != null && !string.IsNullOrEmpty(response.message))
        {
            string msg = ConvertRegisterMessage(response.message);
            uiMessage.ShowMessage(msg, Color.red);
        }
        else
        {
            uiMessage.ShowMessage("Đăng ký thất bại", Color.red);
        }
    }

    private string ConvertRegisterMessage(string backendMessage)
    {
        if (backendMessage.Contains("Email"))
            return "Email đã được sử dụng";

        if (backendMessage.Contains("Username"))
            return "Tên đăng nhập đã được sử dụng";

        if (backendMessage.Contains("Confirm password"))
            return "Mật khẩu nhập lại không khớp";

        return backendMessage;
    }
}