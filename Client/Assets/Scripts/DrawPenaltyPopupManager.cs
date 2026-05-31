using System.Collections;
using TMPro;
using UnityEngine;

public class DrawPenaltyPopupManager : MonoBehaviour
{
    [Header("Popup Objects")]
    [SerializeField] private GameObject playerPopup;
    [SerializeField] private GameObject bot1Popup;
    [SerializeField] private GameObject bot2Popup;
    [SerializeField] private GameObject bot3Popup;

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

    public void ShowPenaltyByTurnIndex(int turnIndex, int amount)
    {
        switch (turnIndex)
        {
            case 0:
                playerCoroutine = ShowPopup(playerPopup, playerCoroutine, amount);
                break;

            case 1:
                bot1Coroutine = ShowPopup(bot1Popup, bot1Coroutine, amount);
                break;

            case 2:
                bot2Coroutine = ShowPopup(bot2Popup, bot2Coroutine, amount);
                break;

            case 3:
                bot3Coroutine = ShowPopup(bot3Popup, bot3Coroutine, amount);
                break;
        }
    }

    private Coroutine ShowPopup(GameObject popup, Coroutine currentCoroutine, int amount)
    {
        if (popup == null)
        {
            return null;
        }

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        return StartCoroutine(ShowPopupRoutine(popup, amount));
    }

    private IEnumerator ShowPopupRoutine(GameObject popup, int amount)
    {
        TMP_Text popupText = popup.GetComponent<TMP_Text>();

        if (popupText == null)
        {
            popupText = popup.GetComponentInChildren<TMP_Text>(true);
        }

        if (popupText != null)
        {
            popupText.text = "+" + amount;
        }

        popup.SetActive(true);

        yield return new WaitForSeconds(showDuration);

        popup.SetActive(false);
    }

    public void HideAllPopups()
    {
        HidePopup(playerPopup);
        HidePopup(bot1Popup);
        HidePopup(bot2Popup);
        HidePopup(bot3Popup);
    }

    private void HidePopup(GameObject popup)
    {
        if (popup != null)
        {
            popup.SetActive(false);
        }
    }
}
