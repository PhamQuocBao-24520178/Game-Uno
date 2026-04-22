namespace UnoCustomBackend.Api.Helpers
{
    public static class RandomCodeGenerator
    {
        public static string Generate6Digits()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}
