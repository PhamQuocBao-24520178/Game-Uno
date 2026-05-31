[System.Serializable]
public class SimpleApiResponse
{
    public bool success;
    public string message;
}

[System.Serializable]
public class RegisterRequestData
{
    public string username;
    public string email;
    public string password;
    public string confirmPassword;
}

[System.Serializable]
public class LoginRequestData
{
    public string login;
    public string password;
}

[System.Serializable]
public class LoginApiResponse
{
    public bool success;
    public string message;
    public LoginResponseData data;
}

[System.Serializable]
public class LoginResponseData
{
    public string token;
    public string userId;
    public string email;
    public string username;
    public string displayName;
    public bool needCreateDisplayName;
    public string expiredAt;
}

[System.Serializable]
public class ForgotPasswordRequestData
{
    public string email;
}

[System.Serializable]
public class ResetPasswordRequestData
{
    public string email;
    public string code;
    public string newPassword;
}

[System.Serializable]
public class SendChangePasswordCodeRequestData
{
    public string email;
}

[System.Serializable]
public class ChangePasswordRequestData
{
    public string oldPassword;
    public string email;
    public string code;
    public string newPassword;
}