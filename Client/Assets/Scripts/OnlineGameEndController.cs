using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnlineGameEndController : MonoBehaviour
{
    [Header("Game State Source")]
    [SerializeField]
    private OnlineGameSetupController onlineGameSetupController;

    [Header("Scene")]
    [SerializeField]
    private string resultSceneName = "ResultGame";

    private bool hasMovedToResultScene;

    private void Update()
    {
        if (hasMovedToResultScene ||
            onlineGameSetupController == null)
        {
            return;
        }

        OnlineGameStateResponse state =
            onlineGameSetupController.CurrentGameState;

        if (state == null ||
            state.status != "Finished")
        {
            return;
        }

        SaveOnlineResult(state);

        hasMovedToResultScene = true;

        PlayerPrefs.SetInt("IsOnlineResult", 1);
        PlayerPrefs.Save();

        SceneManager.LoadScene(resultSceneName);
    }

    private void SaveOnlineResult(
        OnlineGameStateResponse state)
    {
        List<OnlineResultPlayerData> ranking =
            BuildRanking(state);

        OnlineResultDataStore.SetRanking(ranking);
    }

    private List<OnlineResultPlayerData> BuildRanking(
        OnlineGameStateResponse state)
    {
        List<OnlineResultPlayerData> ranking =
            new List<OnlineResultPlayerData>();

        if (state == null || state.players == null)
        {
            return ranking;
        }

        for (int i = 0; i < state.players.Length; i++)
        {
            OnlineGamePlayerStateResponse player =
                state.players[i];

            if (player == null)
            {
                continue;
            }

            ranking.Add(new OnlineResultPlayerData
            {
                playerId = player.playerId,
                displayName = player.displayName,
                avatarIndex = player.avatarIndex,
                cardCount = player.cardCount,
                place = 0
            });
        }

        // Người hết bài sẽ đứng hạng 1.
        // Các người còn lại: ít bài hơn sẽ xếp cao hơn.
        ranking = ranking
            .OrderBy(player => player.cardCount)
            .ThenBy(player => player.displayName)
            .ToList();

        for (int i = 0; i < ranking.Count; i++)
        {
            ranking[i].place = i + 1;
        }

        return ranking;
    }
}