using System.Security.Cryptography;

namespace UnoCustomBackend.Api.Services
{
    public class PasswordService
    {
        public (string Hash, string Salt) HashPassword(string password)
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(16);

            byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                100000,
                HashAlgorithmName.SHA256,
                32);

            return (
                Convert.ToBase64String(hashBytes),
                Convert.ToBase64String(saltBytes)
            );
        }

        public bool VerifyPassword(string password, string savedHash, string savedSalt)
        {
            byte[] saltBytes = Convert.FromBase64String(savedSalt);

            byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                100000,
                HashAlgorithmName.SHA256,
                32);

            string computedHash = Convert.ToBase64String(hashBytes);

            return computedHash == savedHash;
        }
    }
}