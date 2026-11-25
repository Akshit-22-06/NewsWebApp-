using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsWebApp.Services
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        private readonly string? _brevoApiKey;
        private readonly string? _resendApiKey;
        private readonly string _senderEmail;
        private readonly string _senderName;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public EmailService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<EmailService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _brevoApiKey = configuration["Brevo:ApiKey"] 
                ?? configuration["BREVO_API_KEY"] 
                ?? Environment.GetEnvironmentVariable("BREVO_API_KEY");

            _resendApiKey = configuration["Resend:ApiKey"] 
                ?? configuration["RESEND_API_KEY"] 
                ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");

            _senderEmail = configuration["Newsletter:SenderEmail"] ?? "newsletter@newsbase.org";
            _senderName = configuration["Newsletter:SenderName"] ?? "The Morning Dispatch | NewsBase";
        }

        public string GetProviderName()
        {
            if (!string.IsNullOrWhiteSpace(_brevoApiKey)) return "Brevo (Free Tier: 300 emails/day)";
            if (!string.IsNullOrWhiteSpace(_resendApiKey)) return "Resend API";
            return "Simulated / Developer Delivery (Brevo Free Quota: 300/day)";
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_brevoApiKey) || !string.IsNullOrWhiteSpace(_resendApiKey);
        }

        public async Task<EmailQuotaInfo> GetDailyQuotaAsync(int localUsedToday = 0, int localDailyLimit = 300)
        {
            var quota = new EmailQuotaInfo
            {
                DailyLimit = localDailyLimit > 0 ? localDailyLimit : 300,
                UsedToday = Math.Max(0, localUsedToday),
                Provider = GetProviderName(),
                ResetTimeDescription = "Resets daily at 00:00 UTC (05:30 AM IST)"
            };

            if (string.IsNullOrWhiteSpace(_brevoApiKey))
            {
                return quota;
            }

            // Query live Brevo account endpoint to check verified credits
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.brevo.com/v3/account");
                request.Headers.Add("api-key", _brevoApiKey.Trim());
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("email", out var emailProp))
                    {
                        quota.AccountEmail = emailProp.GetString();
                    }

                    if (root.TryGetProperty("plan", out var planArray) && planArray.GetArrayLength() > 0)
                    {
                        var firstPlan = planArray[0];
                        if (firstPlan.TryGetProperty("type", out var typeProp))
                        {
                            quota.PlanType = typeProp.GetString();
                        }
                        if (firstPlan.TryGetProperty("credits", out var creditsProp))
                        {
                            quota.LiveCreditsRemaining = creditsProp.GetDouble();
                        }
                    }

                    quota.IsLiveAccountChecked = true;
                    _logger.LogInformation("Verified live Brevo account: {Email}, Plan: {Plan}, Credits: {Credits}",
                        quota.AccountEmail, quota.PlanType, quota.LiveCreditsRemaining);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch live Brevo account details: {Message}. Using local tracking.", ex.Message);
            }

            return quota;
        }

        public async Task<(bool Success, string? ErrorMessage)> SendEmailAsync(
            string toEmail, 
            string subject, 
            string htmlContent, 
            string plainTextContent)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return (false, "Recipient email address cannot be empty.");
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(_brevoApiKey))
                {
                    return await SendViaBrevoAsync(toEmail, subject, htmlContent, plainTextContent);
                }

                if (!string.IsNullOrWhiteSpace(_resendApiKey))
                {
                    return await SendViaResendAsync(toEmail, subject, htmlContent, plainTextContent);
                }

                return await SimulateDeliveryAsync(toEmail, subject, htmlContent, plainTextContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception delivering email to {Email}: {Message}", toEmail, ex.Message);
                return (false, $"Delivery failed: {ex.Message}");
            }
        }

        public async Task<BatchDeliveryResult> SendBatchAsync(
            IEnumerable<(int Id, string Email, string UnsubscribeToken)> recipients,
            string subject,
            int maxDailyQuotaRemaining,
            Func<string, string> htmlBodyFactory,
            Func<string, string> plainTextBodyFactory)
        {
            var recipientList = recipients.ToList();
            var result = new BatchDeliveryResult();

            _logger.LogInformation("Beginning batch delivery for '{Subject}'. Total candidates: {Count}, Daily Quota Remaining: {Quota}...",
                subject, recipientList.Count, maxDailyQuotaRemaining);

            if (maxDailyQuotaRemaining <= 0)
            {
                result.HitQuotaLimit = true;
                result.QueuedCount = recipientList.Count;
                result.Errors.Add("Brevo free daily quota (300 emails/day) reached. Remaining subscribers queued for next daily reset at 00:00 UTC.");
                _logger.LogWarning("Brevo daily email quota reached. 0 emails sent; {Queued} queued.", result.QueuedCount);
                return result;
            }

            int allowedToSend = Math.Min(recipientList.Count, maxDailyQuotaRemaining);
            var toSend = recipientList.Take(allowedToSend).ToList();
            var queued = recipientList.Skip(allowedToSend).ToList();

            if (queued.Count > 0)
            {
                result.HitQuotaLimit = true;
                result.QueuedCount = queued.Count;
                result.Errors.Add($"Brevo free daily quota cap applied: sending {allowedToSend} today; {queued.Count} subscribers queued for tomorrow's reset.");
            }

            const int batchSize = 10;
            for (int i = 0; i < toSend.Count; i += batchSize)
            {
                var currentBatch = toSend.Skip(i).Take(batchSize).ToList();

                foreach (var (id, email, token) in currentBatch)
                {
                    var html = htmlBodyFactory(token);
                    var text = plainTextBodyFactory(token);

                    var (success, error) = await SendEmailAsync(email, subject, html, text);
                    if (success)
                    {
                        result.SuccessCount++;
                        result.DeliveredSubscriberIds.Add(id);
                    }
                    else
                    {
                        result.FailureCount++;
                        if (error != null && !result.Errors.Contains(error))
                        {
                            result.Errors.Add($"{email}: {error}");
                        }
                    }
                }

                // Throttle pacing between micro-batches to strictly adhere to Brevo rate limits
                if (i + batchSize < toSend.Count)
                {
                    await Task.Delay(250);
                }
            }

            _logger.LogInformation("Batch delivery completed: {Success} sent, {Failed} failed, {Queued} queued (Quota hit: {QuotaHit}).",
                result.SuccessCount, result.FailureCount, result.QueuedCount, result.HitQuotaLimit);

            return result;
        }

        private async Task<(bool Success, string? ErrorMessage)> SendViaBrevoAsync(
            string toEmail, 
            string subject, 
            string htmlContent, 
            string plainTextContent)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", _brevoApiKey!.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                sender = new { name = _senderName, email = _senderEmail },
                to = new[] { new { email = toEmail } },
                subject = subject,
                htmlContent = htmlContent,
                textContent = plainTextContent
            };

            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Dispatched email to {Email} via Brevo.", toEmail);
                return (true, null);
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Brevo API returned HTTP {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
            return (false, $"Brevo API HTTP {(int)response.StatusCode}: {errorBody}");
        }

        private async Task<(bool Success, string? ErrorMessage)> SendViaResendAsync(
            string toEmail, 
            string subject, 
            string htmlContent, 
            string plainTextContent)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _resendApiKey!.Trim());

            var payload = new
            {
                from = $"{_senderName} <{_senderEmail}>",
                to = new[] { toEmail },
                subject = subject,
                html = htmlContent,
                text = plainTextContent
            };

            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Dispatched email to {Email} via Resend.", toEmail);
                return (true, null);
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Resend API returned HTTP {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
            return (false, $"Resend API HTTP {(int)response.StatusCode}: {errorBody}");
        }

        private Task<(bool Success, string? ErrorMessage)> SimulateDeliveryAsync(
            string toEmail, 
            string subject, 
            string htmlContent, 
            string plainTextContent)
        {
            _logger.LogInformation("[SIMULATED EMAIL DISPATCH] Recipient: {Email} | Subject: '{Subject}' | Brevo Quota Tracked",
                toEmail, subject);

            return Task.FromResult<(bool Success, string? ErrorMessage)>((true, null));
        }
    }
}
