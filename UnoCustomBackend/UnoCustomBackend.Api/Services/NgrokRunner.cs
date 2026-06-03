using System.Diagnostics;

namespace UnoCustomBackend.Api.Services
{
    public class NgrokRunner : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private Process? _ngrokProcess;

        public NgrokRunner(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            bool enabled = _configuration.GetValue<bool>("Ngrok:Enabled");

            if (!enabled)
            {
                return Task.CompletedTask;
            }

            // Chỉ tự bật ngrok khi đang chạy Development.
            // Lên server thật thì không tự bật.
            if (!_environment.IsDevelopment())
            {
                return Task.CompletedTask;
            }

            string domain = _configuration["Ngrok:Domain"] ?? "";
            string targetUrl = _configuration["Ngrok:TargetUrl"] ?? "";

            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(targetUrl))
            {
                Console.WriteLine("[ngrok] Thiếu Domain hoặc TargetUrl trong appsettings.json.");
                return Task.CompletedTask;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "ngrok",
                    Arguments = $"http --domain={domain} {targetUrl}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _ngrokProcess = Process.Start(startInfo);

                Console.WriteLine("[ngrok] Đã bật tunnel:");
                Console.WriteLine("[ngrok] https://" + domain + " -> " + targetUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ngrok] Không bật được ngrok.");
                Console.WriteLine("[ngrok] " + ex.Message);
                Console.WriteLine("[ngrok] Kiểm tra ngrok đã cài và chạy được lệnh: ngrok version");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_ngrokProcess != null && !_ngrokProcess.HasExited)
                {
                    _ngrokProcess.Kill(true);
                    _ngrokProcess.Dispose();

                    Console.WriteLine("[ngrok] Đã tắt tunnel.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ngrok] Lỗi khi tắt tunnel: " + ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
