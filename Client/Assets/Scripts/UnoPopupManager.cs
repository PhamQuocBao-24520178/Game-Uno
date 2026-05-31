using System.Collections;
using UnityEngine;

public class UnoPopupManager : MonoBehaviour
{
    [Header("UNO Popups")]
    [SerializeField] private GameObject playerUnoPopup;
    [SerializeField] private GameObject bot1UnoPopup;
    [SerializeField] private GameObject bot2UnoPopup;
    [SerializeField] private GameObject bot3UnoPopup;

    [Header("Settings")]
    [SerializeField] private float showDuration = 1f;

    private Coroutine playerCoroutine;
    private Coroutine bot1Coroutine;
    private Coroutine bot2Coroutine;
    private Coroutine bot3Coroutine;

    private void Awake()
    {
        HideAllPopups();
    }

    public void ShowPlayerUno()
    {
        playerCoroutine = ShowPopup(playerUnoPopup, playerCoroutine);
    }

    public void ShowBot1Uno()
    {
        bot1Coroutine = ShowPopup(bot1UnoPopup, bot1Coroutine);
    }

    public void ShowBot2Uno()
    {
        bot2Coroutine = ShowPopup(bot2UnoPopup, bot2Coroutine);
    }

    public void ShowBot3Uno()
    {
        bot3Coroutine = ShowPopup(bot3UnoPopup, bot3Coroutine);
    }

    public void ShowUnoByTurnIndex(int turnIndex)
    {
        switch (turnIndex)
        {
            case 0:
                ShowPlayerUno();
                break;

            case 1:
                ShowBot1Uno();
                break;

            case 2:
                ShowBot2Uno();
                break;

            case 3:
                ShowBot3Uno();
                break;
        }
    }

    private Coroutine ShowPopup(GameObject popup, Coroutine currentCoroutine)
    {
        if (popup == null)
        {
            return null;
        }

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        return StartCoroutine(ShowPopupRoutine(popup));
    }

    private IEnumerator ShowPopupRoutine(GameObject popup)
    {
        popup.SetActive(true);

        yield return new WaitForSeconds(showDuration);

        popup.SetActive(false);
    }

    private void HideAllPopups()
    {
        if (playerUnoPopup != null)
        {
            playerUnoPopup.SetActive(false);
        }

        if (bot1UnoPopup != null)
        {
            bot1UnoPopup.SetActive(false);
        }

        if (bot2UnoPopup != null)
        {
            bot2UnoPopup.SetActive(false);
        }

        if (bot3UnoPopup != null)
        {
            bot3UnoPopup.SetActive(false);
        }
    }
}
