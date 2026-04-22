using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class ChangePasswordPanelController : MonoBehaviour
{
    public TMP_InputField oldPasswordInput;
    public TMP_InputField emailInput;
    public TMP_InputField codeInput;
    public TMP_InputField newPasswordInput;
    public UIMessage uiMessage;

    public string sendCodeUrl = "https://localhost:7193/api/Auth/send-change-password-code";
    public string changePasswordUrl = "https://localhost:7193/api/Auth/change-password";

    private void OnEnable()
    {
        ClearInputs();
    }

    public void OnClickSendCode()
    {
        StartCoroutine(SendCodeCoroutine());
    }

    public void OnClickChangePassword()
    {
        StartCoroutine(ChangePasswordCoroutine());
    }

    public void ClearInputs()
    {
        oldPasswordInput.text = "";
        emailInput.text = "";
        codeInput.text = "";
        newPasswordInput.text = "";

        oldPasswordInput.ForceLabelUpdate();
        emailInput.ForceLabelUpdate();
        codeInput.ForceLabelUpdate();
        newPasswordInput.ForceLabelUpdate();

        if (uiMessage != null)
            uiMessage.ClearMessage();
    }

    private IEnumerator SendCodeCoroutine()
    {
        uiMessage.ClearMessage();

        if (string.IsNullOrWhiteSpace(emailInput.text))
        {
            uiMessage.ShowMessage("Vui lòng nhập email", Color.yellow);
            yield break;
        }

        SendChangePasswordCodeRequestData data = new SendChangePasswordCodeRequestData
        {
            email = emailInput.text.Trim()
        };

        string json = JsonUtility.ToJson(data);

        using UnityWebRequest request = new UnityWebRequest(sendCodeUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler.text;
        Debug.Log("Send Change Code Response: " + responseText);
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
                uiMessage.ShowMessage("Đã gửi mã về email", Color.green);
            else
                uiMessage.ShowMessage("Gửi mã thất bại", Color.red);

            yield break;
        }

        if (response != null && !string.IsNullOrEmpty(response.message))
            uiMessage.ShowMessage(response.message, Color.red);
        else
            uiMessage.ShowMessage("Gửi mã thất bại", Color.red);
    }

    private IEnumerator ChangePasswordCoroutine()
    {
        uiMessage.ClearMessage();

        if (string.IsNullOrWhiteSpace(oldPasswordInput.text) ||
            string.IsNullOrWhiteSpace(emailInput.text) ||
            string.IsNullOrWhiteSpace(codeInput.text) ||
            string.IsNullOrWhiteSpace(newPasswordInput.text))
        {
            uiMessage.ShowMessage("Vui lòng nhập đầy đủ thông tin", Color.yellow);
            yield break;
        }

        ChangePasswordRequestData data = new ChangePasswordRequestData
        {
            oldPassword = oldPasswordInput.text,
            email = emailInput.text.Trim(),
            code = codeInput.text.Trim(),
            newPassword = newPasswordInput.text
        };

        string json = JsonUtility.ToJson(data);

        using UnityWebRequest request = new UnityWebRequest(changePasswordUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler.text;
        Debug.Log("Change Password Response: " + responseText);
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
                uiMessage.ShowMessage("Đổi mật khẩu thành công", Color.green);
            else
                uiMessage.ShowMessage("Đổi mật khẩu thất bại", Color.red);

            yield break;
        }

        if (response != null && !string.IsNullOrEmpty(response.message))
        {
            string msg = response.message;

            if (msg.Contains("Mật khẩu cũ"))
                msg = "Mật khẩu cũ không đúng";
            else if (msg.Contains("không đúng"))
                msg = "Mã xác nhận không đúng";
            else if (msg.Contains("hết hạn"))
                msg = "Mã đã hết hạn";

            uiMessage.ShowMessage(msg, Color.red);
        }
        else
        {
            uiMessage.ShowMessage("Đổi mật khẩu thất bại", Color.red);
        }
    }
}