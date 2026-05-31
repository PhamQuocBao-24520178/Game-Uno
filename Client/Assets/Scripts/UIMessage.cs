using System.Collections;
using TMPro;
using UnityEngine;

public class UIMessage : MonoBehaviour
{
    public TMP_Text messageText;
    public float showTime = 2.5f;

    private Coroutine currentRoutine;

    private void Start()
    {
        ClearMessage();
    }

    public void ShowMessage(string message, Color color)
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
        }

        currentRoutine = StartCoroutine(ShowMessageRoutine(message, color));
    }

    private IEnumerator ShowMessageRoutine(string message, Color color)
    {
        messageText.gameObject.SetActive(true);
        messageText.text = message;
        messageText.color = color;

        yield return new WaitForSeconds(showTime);

        ClearMessage();
        currentRoutine = null;
    }

    public void ClearMessage()
    {
        if (messageText != null)
        {
            messageText.text = "";
            messageText.gameObject.SetActive(false);
        }
    }
}