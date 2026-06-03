using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LogoutController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button logoutButton;

    [Header("Scene")]
    [SerializeField] private string loginSceneName = "StartMenu";

    private void Awake()
    {
        if (logoutButton != null)
        {
            logoutButton.onClick.RemoveAllListeners();
            logoutButton.onClick.AddListener(Logout);
        }
        else
        {
            Debug.LogError("LogoutController: Chưa kéo Logout Button.");
        }
    }

    private void Logout()
    {
        // Xóa thông tin phiên đăng nhập hiện tại
        PlayerPrefs.DeleteKey("token");
        PlayerPrefs.DeleteKey("userId");
        PlayerPrefs.DeleteKey("email");
        PlayerPrefs.DeleteKey("username");
        PlayerPrefs.DeleteKey("displayName");
        PlayerPrefs.DeleteKey("CurrentAccountKey");

        // Không xóa PlayerName_... / PlayerAvatarIndex_... / ProfileCompleted_...
        // để lần sau tài khoản đó đăng nhập vẫn nhớ profile đã tạo.

        PlayerPrefs.Save();

        Time.timeScale = 1f;

        SceneManager.LoadScene(loginSceneName);
    }
}