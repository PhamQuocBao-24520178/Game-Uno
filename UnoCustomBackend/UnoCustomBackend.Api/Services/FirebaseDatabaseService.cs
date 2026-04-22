using System.Text;
using Newtonsoft.Json;
using UnoCustomBackend.Api.Models.Entities;

namespace UnoCustomBackend.Api.Services
{
    public class FirebaseDatabaseService
    {
        private readonly HttpClient _httpClient;
        private readonly string _databaseUrl;

        public FirebaseDatabaseService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _databaseUrl = configuration["Firebase:DatabaseUrl"]!;
        }

        private string NormalizeEmailKey(string email)
        {
            return email.Trim().ToLower().Replace(".", ",");
        }

        private string NormalizeUsernameKey(string username)
        {
            return username.Trim().ToLower();
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            var key = NormalizeEmailKey(email);
            var response = await _httpClient.GetAsync($"{_databaseUrl}emails/{key}.json");
            var content = await response.Content.ReadAsStringAsync();
            return content != "null";
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            var key = NormalizeUsernameKey(username);
            var response = await _httpClient.GetAsync($"{_databaseUrl}usernames/{key}.json");
            var content = await response.Content.ReadAsStringAsync();
            return content != "null";
        }

        public async Task SaveUserAsync(UserEntity user)
        {
            var userJson = JsonConvert.SerializeObject(user);
            await _httpClient.PutAsync(
                $"{_databaseUrl}users/{user.Id}.json",
                new StringContent(userJson, Encoding.UTF8, "application/json"));

            var emailKey = NormalizeEmailKey(user.Email);
            var usernameKey = NormalizeUsernameKey(user.Username);

            await _httpClient.PutAsync(
                $"{_databaseUrl}emails/{emailKey}.json",
                new StringContent(JsonConvert.SerializeObject(user.Id), Encoding.UTF8, "application/json"));

            await _httpClient.PutAsync(
                $"{_databaseUrl}usernames/{usernameKey}.json",
                new StringContent(JsonConvert.SerializeObject(user.Id), Encoding.UTF8, "application/json"));
        }

        public async Task<string?> GetUserIdByEmailAsync(string email)
        {
            var key = NormalizeEmailKey(email);
            var response = await _httpClient.GetAsync($"{_databaseUrl}emails/{key}.json");
            var content = await response.Content.ReadAsStringAsync();

            if (content == "null") return null;
            return JsonConvert.DeserializeObject<string>(content);
        }

        public async Task<UserEntity?> GetUserByIdAsync(string userId)
        {
            var response = await _httpClient.GetAsync($"{_databaseUrl}users/{userId}.json");
            var content = await response.Content.ReadAsStringAsync();

            if (content == "null") return null;
            return JsonConvert.DeserializeObject<UserEntity>(content);
        }

        public async Task UpdateLastLoginAsync(string userId)
        {
            var patchObj = new
            {
                LastLoginAt = DateTime.UtcNow.ToString("o")
            };

            var json = JsonConvert.SerializeObject(patchObj);

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_databaseUrl}users/{userId}.json")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            await _httpClient.SendAsync(request);
        }

        public async Task UpdatePasswordAsync(string userId, string hash, string salt)
        {
            var patchObj = new
            {
                PasswordHash = hash,
                PasswordSalt = salt
            };

            var json = JsonConvert.SerializeObject(patchObj);

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_databaseUrl}users/{userId}.json")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            await _httpClient.SendAsync(request);
        }

        public async Task UpdateDisplayNameAsync(string userId, string displayName)
        {
            var patchObj = new
            {
                DisplayName = displayName
            };

            var json = JsonConvert.SerializeObject(patchObj);

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_databaseUrl}users/{userId}.json")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            await _httpClient.SendAsync(request);
        }

        public async Task SaveResetCodeAsync(string email, PasswordResetCodeEntity data)
        {
            var key = NormalizeEmailKey(email);
            var json = JsonConvert.SerializeObject(data);

            await _httpClient.PutAsync(
                $"{_databaseUrl}passwordResetCodes/{key}.json",
                new StringContent(json, Encoding.UTF8, "application/json"));
        }

        public async Task<PasswordResetCodeEntity?> GetResetCodeAsync(string email)
        {
            var key = NormalizeEmailKey(email);
            var response = await _httpClient.GetAsync($"{_databaseUrl}passwordResetCodes/{key}.json");
            var content = await response.Content.ReadAsStringAsync();

            if (content == "null") return null;
            return JsonConvert.DeserializeObject<PasswordResetCodeEntity>(content);
        }

        public async Task MarkResetCodeUsedAsync(string email)
        {
            var key = NormalizeEmailKey(email);
            var patchObj = new { Used = true };
            var json = JsonConvert.SerializeObject(patchObj);

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_databaseUrl}passwordResetCodes/{key}.json")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            await _httpClient.SendAsync(request);
        }

        public async Task<string?> GetUserIdByUsernameAsync(string username)
        {
            var key = NormalizeUsernameKey(username);
            var response = await _httpClient.GetAsync($"{_databaseUrl}usernames/{key}.json");
            var content = await response.Content.ReadAsStringAsync();

            if (content == "null") return null;
            return JsonConvert.DeserializeObject<string>(content);
        }
    }
}