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
        string url = BaseUrl + "/api/rooms/" + roomCode;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.certificateHandler = new DevCertificateHandler();
            request.downloadHandler = new DownloadHandlerBuffer();

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error + "\n" + request.downloadHandler.text);
                yield break;
            }

            RoomResponse room = JsonUtility.FromJson<RoomResponse>(request.downloadHandler.text);
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

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error + "\n" + request.downloadHandler.text);
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

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error + "\n" + request.downloadHandler.text);
                yield break;
            }

            RoomResponse room = JsonUtility.FromJson<RoomResponse>(request.downloadHandler.text);
            onSuccess?.Invoke(room);
        }
    }

    private class DevCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
}