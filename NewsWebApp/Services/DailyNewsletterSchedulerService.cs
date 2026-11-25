using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewsWebApp.Models;
using NewsWebApp.Repositories.Interfaces;

namespace NewsWebApp.Services
{
    public class DailyNewsletterSchedulerService : BackgroundService, INewsletterSchedulerManager
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DailyNewsletterSchedulerService> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private DateTime? _lastRunAtIst;
        private DateTime? _nextRunAtIst;
        private string? _lastRunStatus;

        public DailyNewsletterSchedulerService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<DailyNewsletterSchedulerService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DailyNewsletterSchedulerService started. Monitoring schedule in Asia/Kolkata time zone...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndTriggerScheduledDispatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in newsletter scheduler background loop: {Message}", ex.Message);
                }

                // Check schedule every 60 seconds
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }

            _logger.LogInformation("DailyNewsletterSchedulerService background worker stopped.");
        }

        private async Task CheckAndTriggerScheduledDispatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var newsletterRepo = scope.ServiceProvider.GetRequiredService<INewsletterRepository>();
            var settings = await newsletterRepo.GetSettingsAsync();

            if (!settings.IsAutoSendEnabled)
            {
                return;
            }

            var istZone = GetIndiaTimeZone();
            var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var todayStr = nowIst.ToString("yyyy-MM-dd");

            // Calculate next run in IST
            var scheduledToday = new DateTime(nowIst.Year, nowIst.Month, nowIst.Day, settings.SendHour, settings.SendMinute, 0);
            _nextRunAtIst = nowIst < scheduledToday ? scheduledToday : scheduledToday.AddDays(1);

            // Trigger if current minute matches scheduled minute
            if (nowIst.Hour == settings.SendHour && nowIst.Minute == settings.SendMinute)
            {
                var existingEdition = await newsletterRepo.GetEditionByDateAsync(todayStr);
                if (existingEdition != null && existingEdition.Status == "Sent")
                {
                    // Already delivered today's edition
                    return;
                }

                _logger.LogInformation("Scheduled time reached ({Hour:D2}:{Minute:D2} IST). Executing automated dispatch for {Date}...",
                    settings.SendHour, settings.SendMinute, todayStr);

                await ExecuteFullDailyDispatchAsync(todayStr, null, cancellationToken);
            }
        }

        private async Task<(bool Success, string Message)> ExecuteFullDailyDispatchAsync(
            string editionDate, 
            string? baseUrl,
            CancellationToken cancellationToken)
        {
            if (!await _lock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
            {
                return (false, "A newsletter generation or dispatch task is already in progress.");
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var newsletterRepo = scope.ServiceProvider.GetRequiredService<INewsletterRepository>();
                var curationService = scope.ServiceProvider.GetRequiredService<INewsletterCurationService>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                baseUrl ??= _configuration["ApplicationUrl"] ?? "http://localhost:5235";
                var istZone = GetIndiaTimeZone();
                var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);

                // 1. Curate (or retrieve draft) edition
                var existing = await newsletterRepo.GetEditionByDateAsync(editionDate);
                NewsletterEdition edition;

                if (existing == null)
                {
                    var curated = await curationService.CurateDailyEditionAsync(nowIst, baseUrl, cancellationToken);
                    edition = new NewsletterEdition
                    {
                        EditionDate = editionDate,
                        Subject = curated.Subject,
                        HtmlContent = curated.HtmlTemplate,
                        PlainTextContent = curated.PlainTextTemplate,
                        CuratedJson = curated.CuratedJson,
                        Status = "Draft",
                        CreatedAt = DateTime.UtcNow
                    };
                    await newsletterRepo.SaveEditionAsync(edition);
                }
                else
                {
                    edition = existing;
                }

                if (edition.Status == "Sent")
                {
                    return (false, $"The edition for {editionDate} has already been sent to subscribers.");
                }

                // 2. Fetch Active Subscribers & Pending Deliveries for this Edition
                var allActiveSubscribers = await newsletterRepo.GetActiveSubscribersAsync();
                if (allActiveSubscribers.Count == 0)
                {
                    edition.Status = "Sent";
                    edition.TotalRecipients = 0;
                    edition.SuccessfulDeliveries = 0;
                    edition.QueuedCount = 0;
                    edition.SentAt = DateTime.UtcNow;
                    await newsletterRepo.SaveEditionAsync(edition);

                    _lastRunAtIst = nowIst;
                    _lastRunStatus = "Completed (0 active subscribers)";
                    return (true, "No active subscribers found. Edition archived.");
                }

                edition.TotalRecipients = allActiveSubscribers.Count;

                var pendingSubscribers = await newsletterRepo.GetPendingSubscribersForEditionAsync(edition.Id);
                if (pendingSubscribers.Count == 0)
                {
                    edition.Status = "Sent";
                    edition.QueuedCount = 0;
                    await newsletterRepo.SaveEditionAsync(edition);
                    return (true, "All active subscribers have already received this edition.");
                }

                // 3. Check Remaining Brevo Daily Quota
                int remainingQuota = await newsletterRepo.GetRemainingDailyQuotaAsync();
                if (remainingQuota <= 0)
                {
                    edition.Status = "PartiallySent";
                    edition.QueuedCount = pendingSubscribers.Count;
                    edition.ErrorDetails = "Brevo free daily quota (300 emails/day) reached. Remaining subscribers queued for next reset at 00:00 UTC.";
                    await newsletterRepo.SaveEditionAsync(edition);

                    _lastRunStatus = $"Quota Limit Reached: {edition.SuccessfulDeliveries}/{edition.TotalRecipients} delivered; {edition.QueuedCount} queued for 00:00 UTC reset.";
                    return (false, "Brevo free daily quota (300/day) has been reached today. Remaining recipients queued for next daily reset.");
                }

                edition.Status = "Sending";
                await newsletterRepo.SaveEditionAsync(edition);

                // 4. Batch Dispatch Capped to Remaining Brevo Daily Quota
                var recipientTuples = pendingSubscribers.Select(s => (s.Id, s.Email, s.UnsubscribeToken));
                var batchResult = await emailService.SendBatchAsync(
                    recipientTuples,
                    edition.Subject,
                    remainingQuota,
                    token => curationService.RenderPersonalizedHtml(edition.HtmlContent, token, baseUrl),
                    token => curationService.RenderPersonalizedPlainText(edition.PlainTextContent, token, baseUrl)
                );

                // 5. Record Delivered Subscribers to Prevent Duplicate Emails
                if (batchResult.DeliveredSubscriberIds.Count > 0)
                {
                    await newsletterRepo.MarkSubscribersDeliveredAsync(batchResult.DeliveredSubscriberIds, edition.Id);
                    await newsletterRepo.RecordEmailsSentAsync(batchResult.DeliveredSubscriberIds.Count);
                }

                // 6. Update Edition Status & Queued Count
                edition.SuccessfulDeliveries += batchResult.SuccessCount;
                edition.FailedDeliveries += batchResult.FailureCount;
                edition.QueuedCount = batchResult.QueuedCount;
                edition.SentAt = DateTime.UtcNow;

                if (batchResult.QueuedCount > 0)
                {
                    edition.Status = "PartiallySent";
                    edition.ErrorDetails = $"Brevo daily quota reached: {edition.SuccessfulDeliveries}/{edition.TotalRecipients} delivered; {edition.QueuedCount} queued for tomorrow.";
                }
                else
                {
                    edition.Status = (batchResult.FailureCount == 0 || edition.SuccessfulDeliveries > 0) ? "Sent" : "Failed";
                }

                if (batchResult.Errors.Count > 0)
                {
                    edition.ErrorDetails = string.Join("\n", batchResult.Errors.Take(10));
                }

                await newsletterRepo.SaveEditionAsync(edition);

                // 7. Update Runtime Settings
                var settings = await newsletterRepo.GetSettingsAsync();
                settings.LastRunAt = DateTime.UtcNow;
                settings.LastRunStatus = $"Sent {edition.SuccessfulDeliveries}/{edition.TotalRecipients} (Brevo Quota: {settings.EmailsSentToday}/{settings.DailyEmailQuota} today, {edition.QueuedCount} queued)";
                await newsletterRepo.UpdateSettingsAsync(settings);

                _lastRunAtIst = nowIst;
                _lastRunStatus = settings.LastRunStatus;

                return (true, $"Edition dispatch completed. Sent: {batchResult.SuccessCount}, Failed: {batchResult.FailureCount}, Queued: {batchResult.QueuedCount}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed executing daily dispatch for {Date}: {Message}", editionDate, ex.Message);
                _lastRunStatus = $"Failed: {ex.Message}";
                return (false, $"Dispatch error: {ex.Message}");
            }
            finally
            {
                _lock.Release();
            }
        }

        // ==========================================
        // INewsletterSchedulerManager Implementation
        // ==========================================

        public async Task<(bool Success, string Message, NewsletterEdition? Edition)> GenerateEditionAsync(
            string? editionDate = null, 
            string? baseUrl = null)
        {
            if (!await _lock.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                return (false, "Another newsletter process is currently executing. Please wait.", null);
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var newsletterRepo = scope.ServiceProvider.GetRequiredService<INewsletterRepository>();
                var curationService = scope.ServiceProvider.GetRequiredService<INewsletterCurationService>();

                baseUrl ??= _configuration["ApplicationUrl"] ?? "http://localhost:5235";
                var istZone = GetIndiaTimeZone();
                var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
                var targetDate = editionDate ?? nowIst.ToString("yyyy-MM-dd");

                var curated = await curationService.CurateDailyEditionAsync(nowIst, baseUrl);

                var existing = await newsletterRepo.GetEditionByDateAsync(targetDate);
                if (existing != null)
                {
                    existing.Subject = curated.Subject;
                    existing.HtmlContent = curated.HtmlTemplate;
                    existing.PlainTextContent = curated.PlainTextTemplate;
                    existing.CuratedJson = curated.CuratedJson;
                    if (existing.Status != "Sent")
                    {
                        existing.Status = "Draft";
                    }
                    await newsletterRepo.SaveEditionAsync(existing);
                    return (true, $"Edition for {targetDate} generated and refreshed successfully.", existing);
                }

                var newEdition = new NewsletterEdition
                {
                    EditionDate = targetDate,
                    Subject = curated.Subject,
                    HtmlContent = curated.HtmlTemplate,
                    PlainTextContent = curated.PlainTextTemplate,
                    CuratedJson = curated.CuratedJson,
                    Status = "Draft",
                    CreatedAt = DateTime.UtcNow
                };

                await newsletterRepo.SaveEditionAsync(newEdition);
                return (true, $"Edition for {targetDate} created as Draft.", newEdition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating edition manually: {Message}", ex.Message);
                return (false, $"Curation error: {ex.Message}", null);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<(bool Success, string Message)> SendEditionAsync(int editionId, string? baseUrl = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var newsletterRepo = scope.ServiceProvider.GetRequiredService<INewsletterRepository>();
            var edition = await newsletterRepo.GetEditionByIdAsync(editionId);

            if (edition == null)
            {
                return (false, "Edition not found.");
            }

            if (edition.Status == "Sent")
            {
                return (false, "This edition has already been sent to subscribers. To prevent spamming subscribers, duplicate sending is prevented.");
            }

            return await ExecuteFullDailyDispatchAsync(edition.EditionDate, baseUrl, CancellationToken.None);
        }

        public async Task<(bool Success, string Message)> SendTestEmailAsync(
            int editionId, 
            string testEmail, 
            string? baseUrl = null)
        {
            if (string.IsNullOrWhiteSpace(testEmail))
            {
                return (false, "Please provide a valid test email address.");
            }

            using var scope = _scopeFactory.CreateScope();
            var newsletterRepo = scope.ServiceProvider.GetRequiredService<INewsletterRepository>();
            var curationService = scope.ServiceProvider.GetRequiredService<INewsletterCurationService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var edition = await newsletterRepo.GetEditionByIdAsync(editionId);
            if (edition == null)
            {
                return (false, "Newsletter edition not found.");
            }

            baseUrl ??= _configuration["ApplicationUrl"] ?? "http://localhost:5235";
            var dummyToken = "test_preview_token";
            var personalizedHtml = curationService.RenderPersonalizedHtml(edition.HtmlContent, dummyToken, baseUrl);
            var personalizedText = curationService.RenderPersonalizedPlainText(edition.PlainTextContent, dummyToken, baseUrl);

            var (success, error) = await emailService.SendEmailAsync(
                testEmail.Trim(), 
                $"[PREVIEW] {edition.Subject}", 
                personalizedHtml, 
                personalizedText);

            if (success)
            {
                return (true, $"Test dispatch successfully delivered to {testEmail.Trim()} via {emailService.GetProviderName()}.");
            }

            return (false, $"Test dispatch failed: {error}");
        }

        public NewsletterSchedulerStatusDto GetStatus()
        {
            using var scope = _scopeFactory.CreateScope();
            var newsletterRepo = scope.ServiceProvider.GetRequiredService<INewsletterRepository>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var settings = newsletterRepo.GetSettingsAsync().GetAwaiter().GetResult();

            var istZone = GetIndiaTimeZone();
            var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var scheduledToday = new DateTime(nowIst.Year, nowIst.Month, nowIst.Day, settings.SendHour, settings.SendMinute, 0);
            var nextRun = nowIst < scheduledToday ? scheduledToday : scheduledToday.AddDays(1);

            return new NewsletterSchedulerStatusDto
            {
                IsRunning = true,
                IsAutoSendEnabled = settings.IsAutoSendEnabled,
                SendHour = settings.SendHour,
                SendMinute = settings.SendMinute,
                TimeZoneId = settings.TimeZoneId,
                LastRunStatus = _lastRunStatus ?? settings.LastRunStatus,
                ActiveEmailProvider = emailService.GetProviderName(),
                IsEmailProviderConfigured = emailService.IsConfigured(),
                DailyEmailQuota = settings.DailyEmailQuota > 0 ? settings.DailyEmailQuota : 300,
                EmailsSentToday = settings.EmailsSentToday,
                RemainingQuotaToday = Math.Max(0, (settings.DailyEmailQuota > 0 ? settings.DailyEmailQuota : 300) - settings.EmailsSentToday)
            };
        }

        public async Task<EmailQuotaInfo> GetEmailQuotaAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var newsletterRepo = scope.ServiceProvider.GetRequiredService<INewsletterRepository>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var settings = await newsletterRepo.GetSettingsAsync();

            return await emailService.GetDailyQuotaAsync(settings.EmailsSentToday, settings.DailyEmailQuota);
        }

        public async Task<(bool Success, string Message)> SubscribeAndSendWelcomeAsync(
            string email, 
            string? baseUrl = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var newsletterRepo = scope.ServiceProvider.GetRequiredService<INewsletterRepository>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            baseUrl ??= _configuration["ApplicationUrl"] ?? "http://localhost:5235";

            var (success, message, isNewOrReactivated, subscriber) = await newsletterRepo.SubscribeAsync(email);
            if (!success || subscriber == null)
            {
                return (success, message);
            }

            if (isNewOrReactivated)
            {
                try
                {
                    var (html, text) = BuildWelcomeEmailContent(subscriber.UnsubscribeToken, baseUrl);
                    var (emailSent, error) = await emailService.SendEmailAsync(
                        subscriber.Email,
                        "Welcome to The Morning Dispatch — Thanks for Subscribing!",
                        html,
                        text);

                    if (emailSent)
                    {
                        await newsletterRepo.RecordEmailsSentAsync(1);
                        _logger.LogInformation("Sent welcome confirmation email to subscriber {Email}.", subscriber.Email);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to dispatch welcome email to {Email}: {Error}", subscriber.Email, error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error sending welcome email to {Email}: {Message}", subscriber.Email, ex.Message);
                }
            }

            return (true, "Thank you for subscribing! A welcome confirmation has been sent to your inbox.");
        }

        private static (string Html, string Text) BuildWelcomeEmailContent(string unsubscribeToken, string baseUrl)
        {
            var unsubscribeUrl = $"{baseUrl.TrimEnd('/')}/newsletter/unsubscribe?token={System.Net.WebUtility.UrlEncode(unsubscribeToken)}";
            var exploreUrl = $"{baseUrl.TrimEnd('/')}/news";
            var homeUrl = $"{baseUrl.TrimEnd('/')}/";

            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Welcome to The Morning Dispatch</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #0f172a; -webkit-font-smoothing: antialiased;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color: #f1f5f9; padding: 32px 0;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width: 600px; width: 100%; background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 6px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.06);"">
                    <!-- Header Masthead -->
                    <tr>
                        <td style=""background-color: #0f172a; padding: 24px 32px; text-align: center;"">
                            <div style=""font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #94a3b8; font-weight: 600; margin-bottom: 6px;"">NewsBase &bull; Editorial Desk</div>
                            <h1 style=""margin: 0; font-family: Georgia, 'Times New Roman', serif; font-size: 26px; color: #ffffff; letter-spacing: 0.5px; font-weight: 700;"">The Morning Dispatch</h1>
                            <div style=""font-size: 12px; color: #cbd5e1; margin-top: 6px;"">Your Daily Morning News Digest</div>
                        </td>
                    </tr>
                    <!-- Thin Red Line -->
                    <tr>
                        <td style=""height: 3px; background-color: #991b1b;""></td>
                    </tr>
                    <!-- Content -->
                    <tr>
                        <td style=""padding: 36px 36px 28px 36px;"">
                            <h2 style=""margin: 0 0 16px 0; font-family: Georgia, 'Times New Roman', serif; font-size: 22px; color: #0f172a; line-height: 1.3;"">
                                Thanks for Subscribing to The Morning Dispatch!
                            </h2>
                            <p style=""font-size: 15px; line-height: 1.6; color: #334155; margin: 0 0 16px 0;"">
                                Hello,
                            </p>
                            <p style=""font-size: 15px; line-height: 1.6; color: #334155; margin: 0 0 20px 0;"">
                                Thank you for subscribing to <strong>The Morning Dispatch</strong>. You are now part of our morning readership receiving rigorous, independent reporting directly in your inbox.
                            </p>
                            
                            <div style=""background-color: #f8fafc; border: 1px solid #e2e8f0; border-left: 4px solid #0f172a; border-radius: 4px; padding: 18px 20px; margin: 0 0 24px 0;"">
                                <div style=""font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 1px; color: #0f172a; margin-bottom: 8px;"">What to Expect Every Morning (07:00 AM IST)</div>
                                <ul style=""margin: 0; padding-left: 18px; font-size: 13.5px; line-height: 1.6; color: #475569;"">
                                    <li style=""margin-bottom: 6px;""><strong>Top Reporting of the Day:</strong> Essential global headlines and investigative features.</li>
                                    <li style=""margin-bottom: 6px;""><strong>Categorized News:</strong> Hand-curated stories across Technology, Business, and Science.</li>
                                    <li><strong>Today's Important Events:</strong> Confirmed global summits, policy announcements, and scheduled events.</li>
                                </ul>
                            </div>

                            <p style=""font-size: 14.5px; line-height: 1.6; color: #334155; margin: 0 0 24px 0;"">
                                Your next edition will arrive tomorrow morning at 07:00 AM IST. In the meantime, you can explore live stories on our homepage.
                            </p>

                            <!-- CTA Button -->
                            <div style=""text-align: center; margin: 28px 0;"">
                                <a href=""{homeUrl}"" target=""_blank"" style=""background-color: #0f172a; color: #ffffff; text-decoration: none; padding: 12px 28px; border-radius: 4px; font-size: 14px; font-weight: 600; display: inline-block;"">Explore NewsBase &rarr;</a>
                            </div>

                            <p style=""font-size: 13px; line-height: 1.5; color: #64748b; margin: 24px 0 0 0; border-top: 1px solid #e2e8f0; padding-top: 16px;"">
                                Your privacy is protected. We will never share or sell your email address. You can unsubscribe at any time with a single click.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8fafc; padding: 20px 32px; border-top: 1px solid #e2e8f0; text-align: center; font-size: 12px; color: #64748b; line-height: 1.5;"">
                            <div>Sent by <strong>The Morning Dispatch</strong> &bull; NewsBase Publishing Group</div>
                            <div style=""margin-top: 6px;"">
                                <a href=""{unsubscribeUrl}"" target=""_blank"" style=""color: #991b1b; text-decoration: underline;"">Unsubscribe from The Morning Dispatch</a>
                                &nbsp;&bull;&nbsp;
                                <a href=""{baseUrl.TrimEnd('/')}/home/privacy"" target=""_blank"" style=""color: #64748b; text-decoration: underline;"">Privacy Policy</a>
                            </div>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            var text = $@"The Morning Dispatch — NewsBase
========================================

Thanks for Subscribing to The Morning Dispatch!

Hello,

Thank you for subscribing to The Morning Dispatch. You are now part of our morning readership receiving rigorous, independent reporting directly in your inbox.

What to Expect Every Morning (07:00 AM IST):
- Yesterday's Top Reporting across Technology, Business, and Science.
- Important Events happening today.
- Concise 3-minute morning briefing.

Your next edition will arrive tomorrow morning at 07:00 AM IST.
Explore live news: {homeUrl}

Unsubscribe at any time: {unsubscribeUrl}

© 2026 NewsBase Publishing Group. All rights reserved.";

            return (html, text);
        }

        private static TimeZoneInfo GetIndiaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "IST");
                }
            }
        }
    }
}
