using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class GlobalChatApiService
{
    public static string BaseUrl
    {
        get
        {
            return ApiConfig.BaseUrl;
        }
    }

    public static IEnumerator SendMessage(
        GlobalChatSendRequest requestData,
        Action<GlobalChatSendResponse> onSuccess,
        Action<string> onError)
    {
        string url = BaseUrl + "/api/GlobalChat/send";
        string json = JsonUtility.ToJson(requestData);

        using UnityWebRequest request = new UnityWebRequest(url, "POST");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("ngrok-skip-browser-warning", "true");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler.text;

        Debug.Log("GlobalChat Send URL: " + url);
        Debug.Log("GlobalChat Send Response: " + responseText);

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError ||
            request.result == UnityWebRequest.Result.DataProcessingError)
        {
            onError?.Invoke(request.error + " | " + responseText);
            yield break;
        }

        GlobalChatSendResponse response = JsonUtility.FromJson<GlobalChatSendResponse>(responseText);

        if (response != null && response.success)
        {
            onSuccess?.Invoke(response);
        }
        else
        {
            onError?.Invoke(response != null ? response.message : "Gửi tin nhắn thất bại.");
        }
    }

    public static IEnumerator GetMessages(
        Action<GlobalChatListResponse> onSuccess,
        Action<string> onError)
    {
        string url = BaseUrl + "/api/GlobalChat/messages";

        using UnityWebRequest request = UnityWebRequest.Get(url);

        request.SetRequestHeader("ngrok-skip-browser-warning", "true");

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler.text;

        Debug.Log("GlobalChat Get URL: " + url);
        Debug.Log("GlobalChat Get Response: " + responseText);

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError ||
            request.result == UnityWebRequest.Result.DataProcessingError)
        {
            onError?.Invoke(request.error + " | " + responseText);
            yield break;
        }

        GlobalChatListResponse response = JsonUtility.FromJson<GlobalChatListResponse>(responseText);

        if (response != null && response.success)
        {
            onSuccess?.Invoke(response);
        }
        else
        {
            onError?.Invoke(response != null ? response.message : "Không lấy được chat.");
        }
    }
}