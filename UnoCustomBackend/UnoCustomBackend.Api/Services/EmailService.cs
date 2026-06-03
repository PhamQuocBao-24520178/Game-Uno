using System.Net;
using System.Net.Mail;

namespace UnoCustomBackend.Api.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendResetCodeAsync(string toEmail, string code)
        {
            string fromEmail = _config["EmailSettings:Email"]
                ?? throw new Exception("Thiếu EmailSettings:Email trong appsettings.json");

            string fromPassword = _config["EmailSettings:Password"]
                ?? throw new Exception("Thiếu EmailSettings:Password trong appsettings.json");

            string host = _config["EmailSettings:Host"]
                ?? throw new Exception("Thiếu EmailSettings:Host trong appsettings.json");

            string portValue = _config["EmailSettings:Port"]
                ?? throw new Exception("Thiếu EmailSettings:Port trong appsettings.json");

            int port = int.Parse(portValue);

            var smtp = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(fromEmail, fromPassword),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail),
                Subject = "Mã xác nhận UNO Game",
                Body = $"Mã của bạn là: {code}"
            };

            message.To.Add(toEmail);

            await smtp.SendMailAsync(message);
        }
    }
}