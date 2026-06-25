using System;
using UnityEngine;
using UnityEngine.UI;

public class OnlineWildColorPicker : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Color Buttons")]
    [SerializeField] private Button yellowButton;
    [SerializeField] private Button blueButton;
    [SerializeField] private Button greenButton;
    [SerializeField] private Button redButton;

    private Action<string> onColorChosen;

    private void Awake()
    {
        BindButton(yellowButton, "Yellow");
        BindButton(blueButton, "Blue");
        BindButton(greenButton, "Green");
        BindButton(redButton, "Red");

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void Open(Action<string> callback)
    {
        onColorChosen = callback;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    public void Close()
    {
        onColorChosen = null;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void BindButton(Button button, string color)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ChooseColor(color));
    }

    private void ChooseColor(string color)
    {
        Action<string> callback = onColorChosen;

        Close();

        callback?.Invoke(color);
    }

}