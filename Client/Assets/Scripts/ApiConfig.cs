using UnityEngine;

public static class ApiConfig
{
    public const string BaseUrl = "https://detest-spindle-bonelike.ngrok-free.dev";

    public static string LoginUrl => BaseUrl + "/api/Auth/login";
    public static string RegisterUrl => BaseUrl + "/api/Auth/register";
    public static string ForgotPasswordUrl => BaseUrl + "/api/Auth/forgot-password";
    public static string ResetPasswordUrl => BaseUrl + "/api/Auth/reset-password";
}
