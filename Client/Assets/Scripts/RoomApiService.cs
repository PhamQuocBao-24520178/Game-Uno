using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class RoomApiService
{
    public static string BaseUrl = "https://detest-spindle-bonelike.ngrok-free.dev";

    public static IEnumerator CreateRoom(
        CreateRoomRequest requestData,
        Action<RoomResponse> onSuccess,
        Action<string> onError)
    {
        string url = BaseUrl + "/api/rooms/create";
        string json = JsonUtility.ToJson(requestData);

        yield return SendPostRequest(url, json, onSuccess, onError);
    }

    public static IEnumerator JoinRoom(
        JoinRoomRequest requestData,
        Action<RoomResponse> onSuccess,
        Action<string> onError)
    {
        string url = BaseUrl + "/api/rooms/join";
        string json = JsonUtility.ToJson(requestData);

        yield return SendPostRequest(url, json, onSuccess, onError);
    }

    public static IEnumerator GetRoom(
        string roomCode,
        Action<RoomResponse> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            onError?.Invoke("RoomCode đang rỗng, không thể gọi GetRoom.");
            yield break;
        }

        string url = BaseUrl + "/api/rooms/" + roomCode.Trim().ToUpper();

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.certificateHandler = new DevCertificateHandler();
            request.downloadHandler = new DownloadHandlerBuffer();

            // Quan trọng khi dùng ngrok free
            request.SetRequestHeader("ngrok-skip-browser-warning", "true");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    "GET lỗi: " + request.error +
                    "\nURL: " + url +
                    "\nResponse: " + request.downloadHandler.text
                );
                yield break;
            }

            string responseText = request.downloadHandler.text;
            Debug.Log("GetRoom response: " + responseText);

            RoomResponse room = JsonUtility.FromJson<RoomResponse>(responseText);
            onSuccess?.Invoke(room);
        }
    }

    public static IEnumerator LeaveRoom(
        LeaveRoomRequest requestData,
        Action<string> onSuccess,
        Action<string> onError)
    {
        string url = BaseUrl + "/api/rooms/leave";
        string json = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.certificateHandler = new DevCertificateHandler();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("ngrok-skip-browser-warning", "true");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    "LEAVE lỗi: " + request.error +
                    "\nURL: " + url +
                    "\nBody: " + json +
                    "\nResponse: " + request.downloadHandler.text
                );
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text);
        }
    }

    private static IEnumerator SendPostRequest(
        string url,
        string json,
        Action<RoomResponse> onSuccess,
        Action<string> onError)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.certificateHandler = new DevCertificateHandler();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("ngrok-skip-browser-warning", "true");

            Debug.Log("POST URL: " + url);
            Debug.Log("POST Body: " + json);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    "POST lỗi: " + request.error +
                    "\nURL: " + url +
                    "\nBody: " + json +
                    "\nResponse: " + request.downloadHandler.text
                );
                yield break;
            }

            string responseText = request.downloadHandler.text;
            Debug.Log("POST response: " + responseText);

            RoomResponse room = JsonUtility.FromJson<RoomResponse>(responseText);
            onSuccess?.Invoke(room);
        }
    }

    private class DevCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            // Chỉ dùng cho dev/test HTTPS localhost/ngrok.
            return true;
        }
    }
}