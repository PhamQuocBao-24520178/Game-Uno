using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class GameApiService
{
    [Serializable]
    private class StartGameRequest
    {
        public string roomCode;
        public string playerId;
    }

    [Serializable]
    private class DrawCardRequest
    {
        public string roomCode;
        public string playerId;
    }

    [Serializable]
    private class PlayCardRequest
    {
        public string roomCode;
        public string playerId;
        public string cardId;
        public string chosenColor;
    }

    [Serializable]
    private class DeclareUnoRequest
    {
        public string roomCode;
        public string playerId;
    }

    public static IEnumerator StartGame(
        string roomCode,
        string playerId,
        Action<OnlineGameStateResponse> onSuccess,
        Action<string> onError)
    {
        StartGameRequest body = new StartGameRequest
        {
            roomCode = roomCode,
            playerId = playerId
        };

        yield return SendPost(
            "/api/Game/start",
            JsonUtility.ToJson(body),
            onSuccess,
            onError
        );
    }

    public static IEnumerator GetGameState(
        string roomCode,
        string playerId,
        Action<OnlineGameStateResponse> onSuccess,
        Action<string> onError)
    {
        string url =
            GetUrl("/api/Game/" + roomCode) +
            "?playerId=" + UnityWebRequest.EscapeURL(playerId);

        using UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        HandleResponse(
            request,
            onSuccess,
            onError,
            "Không đọc được GameState."
        );
    }

    public static IEnumerator DrawCard(
        string roomCode,
        string playerId,
        Action<OnlineGameStateResponse> onSuccess,
        Action<string> onError)
    {
        DrawCardRequest body = new DrawCardRequest
        {
            roomCode = roomCode,
            playerId = playerId
        };

        yield return SendPost(
            "/api/Game/draw-card",
            JsonUtility.ToJson(body),
            onSuccess,
            onError
        );
    }

    public static IEnumerator PlayCard(
        string roomCode,
        string playerId,
        string cardId,
        string chosenColor,
        Action<OnlineGameStateResponse> onSuccess,
        Action<string> onError)
    {
        PlayCardRequest body = new PlayCardRequest
        {
            roomCode = roomCode,
            playerId = playerId,
            cardId = cardId,
            chosenColor = chosenColor
        };

        yield return SendPost(
            "/api/Game/play-card",
            JsonUtility.ToJson(body),
            onSuccess,
            onError
        );
    }

    public static IEnumerator DeclareUno(
        string roomCode,
        string playerId,
        Action<OnlineGameStateResponse> onSuccess,
        Action<string> onError)
    {
        DeclareUnoRequest body = new DeclareUnoRequest
        {
            roomCode = roomCode,
            playerId = playerId
        };

        yield return SendPost(
            "/api/Game/declare-uno",
            JsonUtility.ToJson(body),
            onSuccess,
            onError
        );
    }

    private static IEnumerator SendPost(
        string route,
        string json,
        Action<OnlineGameStateResponse> onSuccess,
        Action<string> onError)
    {
        using UnityWebRequest request = new UnityWebRequest(
            GetUrl(route),
            UnityWebRequest.kHttpVerbPOST
        );

        request.uploadHandler = new UploadHandlerRaw(
            Encoding.UTF8.GetBytes(json)
        );

        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        yield return request.SendWebRequest();

        HandleResponse(
            request,
            onSuccess,
            onError,
            "Không đọc được phản hồi từ server."
        );
    }

    private static void HandleResponse(
        UnityWebRequest request,
        Action<OnlineGameStateResponse> onSuccess,
        Action<string> onError,
        string fallbackError)
    {
        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(GetErrorMessage(request));
            return;
        }

        OnlineGameStateResponse response =
            JsonUtility.FromJson<OnlineGameStateResponse>(
                request.downloadHandler.text
            );

        if (response == null || !response.success)
        {
            onError?.Invoke(
                response != null &&
                !string.IsNullOrEmpty(response.message)
                    ? response.message
                    : fallbackError
            );

            return;
        }

        onSuccess?.Invoke(response);
    }

    private static string GetUrl(string route)
    {
        string baseUrl = RoomApiService.BaseUrl;

        if (string.IsNullOrEmpty(baseUrl))
        {
            baseUrl =
                "https://detest-spindle-bonelike.ngrok-free.dev";
        }

        return baseUrl.TrimEnd('/') + route;
    }

    private static string GetErrorMessage(
        UnityWebRequest request)
    {
        string body = request.downloadHandler != null
            ? request.downloadHandler.text
            : "";

        if (!string.IsNullOrEmpty(body))
        {
            return request.error + " | " + body;
        }

        return request.error;
    }

}

[Serializable]
public class OnlineGameStateResponse
{
    public bool success;
    public string message;

    public string roomCode;
    public string status;

    public string currentTurnPlayerId;
    public string currentColor;

    public int direction;

    public int pendingDrawPenalty;
    public string pendingPenaltyType;

    public bool hasDeclaredUno;
    public string[] declaredUnoPlayerIds;

    // Timer đồng bộ từ backend.
    public long serverTimeUnixMs;
    public long turnEndsAtUnixMs;
    public int turnDurationSeconds;

    public int deckCount;

    public OnlineApiCardData topCard;
    public OnlineApiCardData[] myHand;
    public OnlineGamePlayerStateResponse[] players;

}

[Serializable]
public class OnlineApiCardData
{
    public string cardId;
    public string color;
    public string value;
}

[Serializable]
public class OnlineGamePlayerStateResponse
{
    public string playerId;
    public string displayName;
    public int avatarIndex;
    public bool isHost;
    public int cardCount;
}