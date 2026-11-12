using System.Net.Http.Json;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Settings;
using Microsoft.Extensions.Options;

namespace FW.Infrastructure.Services.Email;

/// <summary>
/// Gửi email qua HTTP API của Brevo (POST /smtp/email). Dùng HTTPS cổng 443 nên chạy được
/// trên các nền tảng chặn cổng SMTP 25/465/587.
/// </summary>
public class BrevoEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly BrevoSettings _settings;

    public BrevoEmailService(HttpClient http, IOptions<BrevoSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sender = new { name = _settings.SenderName, email = _settings.SenderEmail },
            to = new[] { new { email = toEmail } },
            subject,
            htmlContent = htmlBody,
        };

        using var response = await _http.PostAsJsonAsync("smtp/email", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Brevo từ chối gửi email ({(int)response.StatusCode}): {detail}");
        }
    }
}
