using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject loginPanel;
    public GameObject signUpPanel;
    public GameObject forgotPanel;
    public GameObject changePasswordPanel;

    public LoginPanelController loginPanelController;
    public SignUpPanelController signUpPanelController;
    public ForgotPanelController forgotPanelController;
    public ChangePasswordPanelController changePasswordPanelController;

    private void Start()
    {
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        mainMenu.SetActive(true);
        loginPanel.SetActive(false);
        signUpPanel.SetActive(false);
        forgotPanel.SetActive(false);
        changePasswordPanel.SetActive(false);

        ClearAllInputs();
    }

    public void ShowLoginPanel()
    {
        mainMenu.SetActive(false);
        loginPanel.SetActive(true);
        signUpPanel.SetActive(false);
        forgotPanel.SetActive(false);
        changePasswordPanel.SetActive(false);

        if (loginPanelController != null)
            loginPanelController.ClearInputs();
    }

    public void ShowSignUpPanel()
    {
        mainMenu.SetActive(false);
        loginPanel.SetActive(false);
        signUpPanel.SetActive(true);
        forgotPanel.SetActive(false);
        changePasswordPanel.SetActive(false);

        if (signUpPanelController != null)
            signUpPanelController.ClearInputs();
    }

    public void ShowForgotPanel()
    {
        mainMenu.SetActive(false);
        loginPanel.SetActive(false);
        signUpPanel.SetActive(false);
        forgotPanel.SetActive(true);
        changePasswordPanel.SetActive(false);

        if (forgotPanelController != null)
            forgotPanelController.ClearInputs();
    }

    public void ShowChangePasswordPanel()
    {
        mainMenu.SetActive(false);
        loginPanel.SetActive(false);
        signUpPanel.SetActive(false);
        forgotPanel.SetActive(false);
        changePasswordPanel.SetActive(true);

        if (changePasswordPanelController != null)
            changePasswordPanelController.ClearInputs();
    }

    private void ClearAllInputs()
    {
        if (loginPanelController != null)
            loginPanelController.ClearInputs();

        if (signUpPanelController != null)
            signUpPanelController.ClearInputs();

        if (forgotPanelController != null)
            forgotPanelController.ClearInputs();

        if (changePasswordPanelController != null)
            changePasswordPanelController.ClearInputs();
    }
}