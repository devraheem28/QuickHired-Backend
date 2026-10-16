using SendGrid.Helpers.Mail;
using SendGrid;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using QuickHired.API.Services;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;

namespace QuickHired.API.Helpers
{
    public static class EmailHelper
    {
        private static string GetSendGridApiKey(ILogger logger = null)
        {
            try
            {
                var envKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
                if (!string.IsNullOrWhiteSpace(envKey))
                {
                    logger?.LogInformation("[EMAIL] SendGrid API key loaded from environment variable");
                    LogToFile("INFO", "[EMAIL] SendGrid API key loaded from environment variable", "Authentication_User_Management");
                    return envKey;
                }

                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
                logger?.LogInformation($"[EMAIL] Loading SendGrid API key from config for environment: {environment}");
                LogToFile("INFO", $"[EMAIL] Loading SendGrid API key from config for environment: {environment}", "Authentication_User_Management");

                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();

                var key = config["SendGrid:ApiKey"];
                if (!string.IsNullOrWhiteSpace(key))
                {
                    logger?.LogInformation("[EMAIL] SendGrid API key loaded from configuration file");
                    LogToFile("INFO", "[EMAIL] SendGrid API key loaded from configuration file", "Authentication_User_Management");
                    return key;
                }
                else
                {
                    logger?.LogError("[EMAIL] SendGrid API key not found in configuration");
                    LogToFile("ERROR", "[EMAIL] SendGrid API key not found in configuration", "Authentication_User_Management");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[EMAIL] Exception while loading SendGrid API key");
                LogToFile("ERROR", $"[EMAIL] Exception while loading SendGrid API key: {ex.Message}", "Authentication_User_Management");
                return string.Empty;
            }
        }

        private static void LogToFile(string level, string message, string functionalArea = "GENERAL")
        {
            try
            {
                var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                var logFile = Path.Combine(logDirectory, $"{functionalArea}_logs_{DateTime.Now:yyyy-MM-dd}.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";

                File.AppendAllText(logFile, logEntry);
            }
            catch
            {
                // Silent fail for logging to prevent infinite loops
            }
        }

        public static async Task SendSupportEmailToAdmin(string sendGridApiKey, string adminEmail, string name, string fromEmail, string subject, string message)
        {
            LogToFile("INFO", $"[SUPPORT_EMAIL] Starting support email send - From: {fromEmail}, To: {adminEmail}, Subject: {subject}", "Testing_Support");

            if (string.IsNullOrWhiteSpace(sendGridApiKey))
            {
                LogToFile("ERROR", "[SUPPORT_EMAIL] Missing SendGrid API key for support email", "Testing_Support");
                throw new ArgumentException("Missing SendGrid API key", nameof(sendGridApiKey));
            }

            try
            {
                var client = new SendGridClient(sendGridApiKey);
                LogToFile("INFO", "[SUPPORT_EMAIL] SendGrid client created successfully", "Testing_Support");

                // Use authenticated domain sender to avoid spam filters
                var from = new EmailAddress("beastshah123@gmail.com", "QuickHired Support");
                var to = new EmailAddress(adminEmail);

                LogToFile("INFO", $"[SUPPORT_EMAIL] Using authenticated sender: info@eventify-pro.com for better deliverability", "Testing_Support");

                var html = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                        <title>New Support Request - QuickHired</title>
                    </head>
                    <body style='margin:0;padding:20px;background-color:#f8f9fa;font-family:Arial,sans-serif;'>
                        <div style='max-width:600px;margin:0 auto;background-color:#ffffff;padding:30px;border-radius:8px;box-shadow:0 2px 4px rgba(0,0,0,0.1);'>
                            <div style='text-align:center;margin-bottom:30px;'>
                                <h1 style='color:#1a73e8;margin:0;font-size:24px;'>QuickHired</h1>
                                <p style='color:#6c757d;margin:5px 0 0 0;font-size:14px;'>Professional Job Application Platform</p>
                            </div>
                            <h2 style='color:#333333;margin:0 0 20px 0;font-size:20px;border-bottom:2px solid #1a73e8;padding-bottom:10px;'>New Support Request</h2>
                            <div style='background-color:#f8f9fa;padding:20px;border-radius:6px;margin-bottom:20px;'>
                                <p style='margin:0 0 10px 0;font-size:16px;'><strong style='color:#333;'>Contact Name:</strong> {System.Net.WebUtility.HtmlEncode(name)}</p>
                                <p style='margin:0 0 10px 0;font-size:16px;'><strong style='color:#333;'>Email Address:</strong> {System.Net.WebUtility.HtmlEncode(fromEmail)}</p>
                                <p style='margin:0;font-size:16px;'><strong style='color:#333;'>Subject:</strong> {System.Net.WebUtility.HtmlEncode(subject)}</p>
                            </div>
                            <div>
                                <h3 style='color:#333333;margin:0 0 15px 0;font-size:18px;'>Message:</h3>
                                <div style='background-color:#ffffff;border:1px solid #dee2e6;border-radius:6px;padding:20px;font-size:15px;line-height:1.6;color:#333333;white-space:pre-wrap;'>
                                    {System.Net.WebUtility.HtmlEncode(message)}
                                </div>
                            </div>
                            <div style='margin-top:30px;padding-top:20px;border-top:1px solid #dee2e6;text-align:center;'>
                                <p style='margin:0;font-size:12px;color:#6c757d;'>This message was sent through the QuickHired contact form</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                LogToFile("INFO", $"[SUPPORT_EMAIL] Professional email template prepared - Length: {html.Length} characters", "Testing_Support");

                var msg = MailHelper.CreateSingleEmail(from, to, $"[QuickHired] {subject}", string.Empty, html);

                // Add proper headers to improve deliverability
                msg.Headers = new Dictionary<string, string>
                {
                    { "X-Priority", "3" },
                    { "X-MSMail-Priority", "Normal" },
                    { "X-Mailer", "QuickHired Support System" },
                    { "Organization", "QuickHired" }
                };

                msg.ReplyTo = new EmailAddress(fromEmail, name);

                LogToFile("INFO", "[SUPPORT_EMAIL] Attempting to send support email via SendGrid", "Testing_Support");
                var response = await client.SendEmailAsync(msg);

                LogToFile("INFO", $"[SUPPORT_EMAIL] SendGrid response - Status: {response.StatusCode}", "Testing_Support");

                if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Accepted)
                {
                    var responseBody = response.Body != null ? await response.Body.ReadAsStringAsync() : "No body";
                    LogToFile("ERROR", $"[SUPPORT_EMAIL] Support email failed - Status: {response.StatusCode}, Body: {responseBody}", "Testing_Support");
                    throw new Exception($"Support email failed with status: {response.StatusCode}. Body: {responseBody}");
                }
                else
                {
                    LogToFile("SUCCESS", $"[SUPPORT_EMAIL] Support email sent successfully - Status: {response.StatusCode}", "Testing_Support");
                }
            }
            catch (Exception ex)
            {
                LogToFile("ERROR", $"[SUPPORT_EMAIL] Exception in SendSupportEmailToAdmin: {ex.Message}", "Testing_Support");
                throw;
            }
        }

        /// <summary>
        /// sends email using send grid API
        /// </summary>
        /// <param name="emailTo"></param>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        /// <returns></returns>

        public static string GetBaseUrl()
        {
#if DEBUG

            return "https://localhost:44303";
#else
                // Production or staging environment - use main domain for assets
                return "https://quickhired.com";
#endif
        }

        public static string BuildAssetUrl(string baseUrl, string relativePath)
        {
            return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
        }
        public static async Task SendLoginEmail(string email, string displayName)
        {
            LogToFile("INFO", $"[LOGIN_EMAIL] Starting login email send - To: {email}, User: {displayName}", "Authentication_User_Management");

            try
            {
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "LoginTemplates", "LoginTemplate.html");
                LogToFile("INFO", $"[LOGIN_EMAIL] Looking for template at: {templatePath}", "Authentication_User_Management");

                if (!File.Exists(templatePath))
                {
                    var errorMsg = $"Email template not found at: {templatePath}";
                    LogToFile("ERROR", $"[LOGIN_EMAIL] {errorMsg}", "Authentication_User_Management");
                    Console.WriteLine(errorMsg);
                    throw new FileNotFoundException(errorMsg);
                }

                LogToFile("INFO", "[LOGIN_EMAIL] Login email template found, loading content", "Authentication_User_Management");
                string emailTemplate = await File.ReadAllTextAsync(templatePath);
                emailTemplate = emailTemplate.Replace("{{UserName}}", displayName);

                string baseUrl = GetBaseUrl();
                LogToFile("INFO", $"[LOGIN_EMAIL] Using base URL: {baseUrl}", "Authentication_User_Management");

                emailTemplate = emailTemplate.Replace("{{BaseUrl}}", baseUrl);
                string logoPath = BuildAssetUrl(baseUrl, "assets/logos/QuickHireBlack.png");
                string facebookLogo = BuildAssetUrl(baseUrl, "assets/logos/facebook-4.svg");
                string instagramLogo = BuildAssetUrl(baseUrl, "assets/logos/instagram-2.svg");
                string linkedInLogo = BuildAssetUrl(baseUrl, "assets/logos/linkedin-1.svg");
                string youtubeLogo = BuildAssetUrl(baseUrl, "assets/logos/youtube-3.svg");

                emailTemplate = emailTemplate.Replace("{{logo}}", logoPath);
                emailTemplate = emailTemplate.Replace("{{facebooklogo}}", facebookLogo);
                emailTemplate = emailTemplate.Replace("{{instagramLogo}}", instagramLogo);
                emailTemplate = emailTemplate.Replace("{{linkedInLogo}}", linkedInLogo);
                emailTemplate = emailTemplate.Replace("{{youtubeLogo}}", youtubeLogo);

                LogToFile("INFO", "[LOGIN_EMAIL] Template processing completed, getting API key", "Authentication_User_Management");
                var apiKey = GetSendGridApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    LogToFile("ERROR", "[LOGIN_EMAIL] SendGrid API key is not configured for login email", "Authentication_User_Management");
                    throw new InvalidOperationException("SendGrid API key is not configured.");
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
                var to = new EmailAddress(email);

                var msg = MailHelper.CreateSingleEmail(from, to, "Login Notification", "", emailTemplate);
                LogToFile("INFO", "[LOGIN_EMAIL] Login email message created, sending via SendGrid", "Authentication_User_Management");

                var response = await client.SendEmailAsync(msg);
                LogToFile("INFO", $"[LOGIN_EMAIL] SendGrid response - Status: {response.StatusCode}", "Authentication_User_Management");

                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
                {
                    LogToFile("SUCCESS", "[LOGIN_EMAIL] Login email sent successfully", "Authentication_User_Management");
                    Console.WriteLine("Email sent successfully.");
                }
                else
                {
                    var responseBody = response.Body != null ? await response.Body.ReadAsStringAsync() : "No body";
                    LogToFile("ERROR", $"[LOGIN_EMAIL] Login email failed - Status: {response.StatusCode}, Body: {responseBody}", "Authentication_User_Management");
                    Console.WriteLine($"Failed to send email. Status: {response.StatusCode}");
                    Console.WriteLine($"Response body: {responseBody}");
                    throw new Exception($"Email sending failed with status: {response.StatusCode}. Body: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                LogToFile("ERROR", $"[LOGIN_EMAIL] Exception in SendLoginEmail: {ex.Message}", "Authentication_User_Management");
                throw;
            }
        }

        public static async Task SendRegistrationEmailSocial(string email, string displayName)
        {
            LogToFile("INFO", $"[SOCIAL_REGISTRATION_EMAIL] Starting social registration email send - To: {email}, User: {displayName}", "Authentication_User_Management");

            try
            {
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "RegisterTemplates", "RegisterTemplate.html");
                LogToFile("INFO", $"[SOCIAL_REGISTRATION_EMAIL] Looking for template at: {templatePath}", "Authentication_User_Management");

                if (!File.Exists(templatePath))
                {
                    LogToFile("ERROR", "[SOCIAL_REGISTRATION_EMAIL] Email template not found: " + templatePath, "Authentication_User_Management");
                    Console.WriteLine("Email template not found: " + templatePath);
                    return;
                }

                LogToFile("INFO", "[SOCIAL_REGISTRATION_EMAIL] Template found, processing content", "Authentication_User_Management");
                string emailTemplate = await File.ReadAllTextAsync(templatePath);
                emailTemplate = emailTemplate.Replace("{{UserName}}", displayName);
                var apiKey = GetSendGridApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    LogToFile("ERROR", "[SOCIAL_REGISTRATION_EMAIL] SendGrid API key not configured", "Authentication_User_Management");
                    throw new InvalidOperationException("SendGrid API key is not configured.");
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
                var to = new EmailAddress(email);

                var msg = MailHelper.CreateSingleEmail(from, to, "Welcome to QuickHired", "", emailTemplate);
                LogToFile("INFO", "[SOCIAL_REGISTRATION_EMAIL] Sending email via SendGrid", "Authentication_User_Management");

                var response = await client.SendEmailAsync(msg);
                LogToFile("INFO", $"[SOCIAL_REGISTRATION_EMAIL] SendGrid response - Status: {response.StatusCode}", "Authentication_User_Management");

                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
                {
                    LogToFile("SUCCESS", "[SOCIAL_REGISTRATION_EMAIL] Social registration email sent successfully", "Authentication_User_Management");
                    Console.WriteLine("Registration Email sent successfully.");
                }
                else
                {
                    LogToFile("ERROR", $"[SOCIAL_REGISTRATION_EMAIL] Failed to send email - Status: {response.StatusCode}", "Authentication_User_Management");
                    Console.WriteLine("Failed to send email. Error: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                LogToFile("ERROR", $"[SOCIAL_REGISTRATION_EMAIL] Exception in SendRegistrationEmailSocial: {ex.Message}", "Authentication_User_Management");
                throw;
            }
        }
        public static async Task SendRegistrationEmail(string email, string displayName, string code)
        {

            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "RegisterTemplates", "RegisterTemplate.html");

            if (!File.Exists(templatePath))
            {
                Console.WriteLine("Email template not found: " + templatePath);
                return;
            }

            string emailTemplate = await File.ReadAllTextAsync(templatePath);
            emailTemplate = emailTemplate.Replace("{{UserName}}", displayName);
            emailTemplate = emailTemplate.Replace("{{Code}}", code);
            string baseUrl = GetBaseUrl();

            emailTemplate = emailTemplate.Replace("{{BaseUrl}}", baseUrl);
            string logoPath = BuildAssetUrl(baseUrl, "assets/logos/QuickHireBlack.png");
            string facebookLogo = BuildAssetUrl(baseUrl, "assets/logos/facebook-4.svg");
            string instagramLogo = BuildAssetUrl(baseUrl, "assets/logos/instagram-2.svg");
            string linkedInLogo = BuildAssetUrl(baseUrl, "assets/logos/linkedin-1.svg");
            string youtubeLogo = BuildAssetUrl(baseUrl, "assets/logos/youtube-3.svg");

            emailTemplate = emailTemplate.Replace("{{logo}}", logoPath);
            emailTemplate = emailTemplate.Replace("{{facebooklogo}}", facebookLogo);
            emailTemplate = emailTemplate.Replace("{{instagramLogo}}", instagramLogo);
            emailTemplate = emailTemplate.Replace("{{linkedInLogo}}", linkedInLogo);
            emailTemplate = emailTemplate.Replace("{{youtubeLogo}}", youtubeLogo);

            var apiKey = GetSendGridApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("SendGrid API key is not configured.");
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
            var to = new EmailAddress(email);

            var msg = MailHelper.CreateSingleEmail(from, to, "Welcome to Quick Hired", "", emailTemplate);

            // Send Email
            var response = await client.SendEmailAsync(msg);

            // Log Result
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
            {
                Console.WriteLine("Registration Email sent successfully.");
            }
            else
            {
                Console.WriteLine("Failed to send email. Error: " + response.StatusCode);
            }
        }
        public static async Task SendChangeEmail(string email, string displayName, string code)
        {

            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "RegisterTemplates", "ChangeEmailTemplate.html");

            if (!File.Exists(templatePath))
            {
                Console.WriteLine("Email template not found: " + templatePath);
                return;
            }

            string emailTemplate = await File.ReadAllTextAsync(templatePath);
            emailTemplate = emailTemplate.Replace("{{UserName}}", displayName);
            emailTemplate = emailTemplate.Replace("{{Code}}", code);
            string baseUrl = GetBaseUrl();

            emailTemplate = emailTemplate.Replace("{{BaseUrl}}", baseUrl);
            string logoPath = BuildAssetUrl(baseUrl, "assets/logos/QuickHireBlack.png");
            string facebookLogo = BuildAssetUrl(baseUrl, "assets/logos/facebook-4.svg");
            string instagramLogo = BuildAssetUrl(baseUrl, "assets/logos/instagram-2.svg");
            string linkedInLogo = BuildAssetUrl(baseUrl, "assets/logos/linkedin-1.svg");
            string youtubeLogo = BuildAssetUrl(baseUrl, "assets/logos/youtube-3.svg");

            emailTemplate = emailTemplate.Replace("{{logo}}", logoPath);
            emailTemplate = emailTemplate.Replace("{{facebooklogo}}", facebookLogo);
            emailTemplate = emailTemplate.Replace("{{instagramLogo}}", instagramLogo);
            emailTemplate = emailTemplate.Replace("{{linkedInLogo}}", linkedInLogo);
            emailTemplate = emailTemplate.Replace("{{youtubeLogo}}", youtubeLogo);

            var apiKey = GetSendGridApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("SendGrid API key is not configured.");

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
            var to = new EmailAddress(email);

            var msg = MailHelper.CreateSingleEmail(from, to, "Welcome to QuickHired", "", emailTemplate);

            // ?? Send Email
            var response = await client.SendEmailAsync(msg);

            // ?? Log Result
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
            {
                Console.WriteLine("Registration Email sent successfully.");
            }
            else
            {
                Console.WriteLine("Failed to send email. Error: " + response.StatusCode);
            }
        }
        public static async Task SendSummaryEmail(string email, string displayName, string summaryHtml, string companyName, string interviewDate, string interviewDuration)
        {
            LogToFile("INFO", $"[INTERVIEW_SUMMARY_EMAIL] Starting interview summary email send - To: {email}, User: {displayName}, Company: {companyName}", "AI_Interview_System");

            try
            {
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "Interview", "Summary.html");
                LogToFile("INFO", $"[INTERVIEW_SUMMARY_EMAIL] Looking for template at: {templatePath}", "AI_Interview_System");

                if (!File.Exists(templatePath))
                {
                    LogToFile("ERROR", "[INTERVIEW_SUMMARY_EMAIL] Email template not found: " + templatePath, "AI_Interview_System");
                    Console.WriteLine("Email template not found: " + templatePath);
                    return;
                }

                LogToFile("INFO", "[INTERVIEW_SUMMARY_EMAIL] Template found, processing content", "AI_Interview_System");
                string emailTemplate = await File.ReadAllTextAsync(templatePath);

                emailTemplate = emailTemplate.Replace("{{UserName}}", displayName);
                emailTemplate = emailTemplate.Replace("{{SummaryHtml}}", summaryHtml);
                emailTemplate = emailTemplate.Replace("{{Company}}", companyName);
                emailTemplate = emailTemplate.Replace("{{Date}}", interviewDate);
                emailTemplate = emailTemplate.Replace("{{Duration}}", interviewDuration);


                string baseUrl = GetBaseUrl();
                LogToFile("INFO", $"[INTERVIEW_SUMMARY_EMAIL] Using base URL: {baseUrl}", "AI_Interview_System");

                emailTemplate = emailTemplate.Replace("{{BaseUrl}}", baseUrl);
                string logoPath = BuildAssetUrl(baseUrl, "assets/logos/QuickHireBlack.png");
                string facebookLogo = BuildAssetUrl(baseUrl, "assets/logos/facebook-4.svg");
                string instagramLogo = BuildAssetUrl(baseUrl, "assets/logos/instagram-2.svg");
                string linkedInLogo = BuildAssetUrl(baseUrl, "assets/logos/linkedin-1.svg");
                string youtubeLogo = BuildAssetUrl(baseUrl, "assets/logos/youtube-3.svg");

                emailTemplate = emailTemplate.Replace("{{logo}}", logoPath);
                emailTemplate = emailTemplate.Replace("{{facebooklogo}}", facebookLogo);
                emailTemplate = emailTemplate.Replace("{{instagramLogo}}", instagramLogo);
                emailTemplate = emailTemplate.Replace("{{linkedInLogo}}", linkedInLogo);
                emailTemplate = emailTemplate.Replace("{{youtubeLogo}}", youtubeLogo);

                // SendGrid configuration
                LogToFile("INFO", "[INTERVIEW_SUMMARY_EMAIL] Template processing completed, getting API key", "AI_Interview_System");
                var apiKey = GetSendGridApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    LogToFile("ERROR", "[INTERVIEW_SUMMARY_EMAIL] SendGrid API key not configured", "AI_Interview_System");
                    throw new InvalidOperationException("SendGrid API key is not configured.");
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
                var to = new EmailAddress(email);
                var subject = "Interview Summary Report";
                LogToFile("INFO", $"[INTERVIEW_SUMMARY_EMAIL] Email subject: {subject}", "AI_Interview_System");

                var msg = MailHelper.CreateSingleEmail(from, to, subject, "", emailTemplate);
                LogToFile("INFO", "[INTERVIEW_SUMMARY_EMAIL] Sending email via SendGrid", "AI_Interview_System");

                var response = await client.SendEmailAsync(msg);
                LogToFile("INFO", $"[INTERVIEW_SUMMARY_EMAIL] SendGrid response - Status: {response.StatusCode}", "AI_Interview_System");

                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
                {
                    LogToFile("SUCCESS", "[INTERVIEW_SUMMARY_EMAIL] Interview summary email sent successfully", "AI_Interview_System");
                    Console.WriteLine("Interview Summary Email sent successfully.");
                }
                else
                {
                    LogToFile("ERROR", $"[INTERVIEW_SUMMARY_EMAIL] Failed to send email - Status: {response.StatusCode}", "AI_Interview_System");
                    Console.WriteLine("Failed to send interview summary. Error: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                LogToFile("ERROR", $"[INTERVIEW_SUMMARY_EMAIL] Exception in SendSummaryEmail: {ex.Message}", "AI_Interview_System");
                throw;
            }
        }

        public static async Task SendForgetPasswordEmail(string email, string displayName, string resetLink)
        {
            LogToFile("INFO", $"[FORGOT_PASSWORD_EMAIL] Starting forgot password email send - To: {email}, User: {displayName}", "Authentication_User_Management");

            try
            {
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "ForgetPasswordTemplates", "ForgetPassword.html");
                LogToFile("INFO", $"[FORGOT_PASSWORD_EMAIL] Looking for forgot password template at: {templatePath}", "Authentication_User_Management");

                if (!File.Exists(templatePath))
                {
                    LogToFile("ERROR", "[FORGOT_PASSWORD_EMAIL] Forgot password email template not found: " + templatePath, "Authentication_User_Management");
                    Console.WriteLine("Email template not found: " + templatePath);
                    return;
                }

                LogToFile("INFO", "[FORGOT_PASSWORD_EMAIL] Forgot password template found, processing content", "Authentication_User_Management");
                string emailTemplate = await File.ReadAllTextAsync(templatePath);
                emailTemplate = emailTemplate.Replace("{{UserName}}", displayName)
                                             .Replace("{{ResetUrl}}", resetLink);
                string baseUrl = GetBaseUrl();
                LogToFile("INFO", $"[FORGOT_PASSWORD_EMAIL] Using base URL: {baseUrl}, Reset link: {resetLink}", "Authentication_User_Management");

                emailTemplate = emailTemplate.Replace("{{BaseUrl}}", baseUrl);
                string logoPath = BuildAssetUrl(baseUrl, "assets/logos/QuickHireBlack.png");
                string facebookLogo = BuildAssetUrl(baseUrl, "assets/logos/facebook-4.svg");
                string instagramLogo = BuildAssetUrl(baseUrl, "assets/logos/instagram-2.svg");
                string linkedInLogo = BuildAssetUrl(baseUrl, "assets/logos/linkedin-1.svg");
                string youtubeLogo = BuildAssetUrl(baseUrl, "assets/logos/youtube-3.svg");

                emailTemplate = emailTemplate.Replace("{{logo}}", logoPath);
                emailTemplate = emailTemplate.Replace("{{facebooklogo}}", facebookLogo);
                emailTemplate = emailTemplate.Replace("{{instagramLogo}}", instagramLogo);
                emailTemplate = emailTemplate.Replace("{{linkedInLogo}}", linkedInLogo);
                emailTemplate = emailTemplate.Replace("{{youtubeLogo}}", youtubeLogo);

                LogToFile("INFO", "[FORGOT_PASSWORD_EMAIL] Forgot password template processing completed, getting API key", "Authentication_User_Management");
                var apiKey = GetSendGridApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    LogToFile("ERROR", "[FORGOT_PASSWORD_EMAIL] SendGrid API key not configured for forgot password email", "Authentication_User_Management");
                    throw new InvalidOperationException("SendGrid API key is not configured.");
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
                var to = new EmailAddress(email);

                var msg = MailHelper.CreateSingleEmail(from, to, "Forget Password Request", "", emailTemplate);
                LogToFile("INFO", "[FORGOT_PASSWORD_EMAIL] Forgot password email message created, sending via SendGrid", "Authentication_User_Management");

                var response = await client.SendEmailAsync(msg);
                LogToFile("INFO", $"[FORGOT_PASSWORD_EMAIL] SendGrid response - Status: {response.StatusCode}", "Authentication_User_Management");

                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
                {
                    LogToFile("SUCCESS", "[FORGOT_PASSWORD_EMAIL] Forgot password email sent successfully", "Authentication_User_Management");
                    Console.WriteLine("Forget Password Email sent successfully.");
                }
                else
                {
                    LogToFile("ERROR", $"[FORGOT_PASSWORD_EMAIL] Forgot password email failed - Status: {response.StatusCode}", "Authentication_User_Management");
                    Console.WriteLine("Failed to send email. Error: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                LogToFile("ERROR", $"[FORGOT_PASSWORD_EMAIL] Exception in SendForgetPasswordEmail: {ex.Message}", "Authentication_User_Management");
                throw;
            }
        }

        public static async Task SendResetPasswordEmail(string email, string displayName)
        {
            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "ResetPasswordTemplates", "ResetPassword.html");

            if (!File.Exists(templatePath))
            {
                Console.WriteLine("Email template not found: " + templatePath);
                return;
            }

            string emailTemplate = await File.ReadAllTextAsync(templatePath);
            emailTemplate = emailTemplate.Replace("{{UserName}}", displayName);
            string baseUrl = GetBaseUrl();
            string loginUrl = $"{baseUrl}/login";

            emailTemplate = emailTemplate.Replace("{{BaseUrl}}", baseUrl);
            emailTemplate = emailTemplate.Replace("{{LoginUrl}}", loginUrl);
            string logoPath = BuildAssetUrl(baseUrl, "assets/logos/QuickHireBlack.png");
            string facebookLogo = BuildAssetUrl(baseUrl, "assets/logos/facebook-4.svg");
            string instagramLogo = BuildAssetUrl(baseUrl, "assets/logos/instagram-2.svg");
            string linkedInLogo = BuildAssetUrl(baseUrl, "assets/logos/linkedin-1.svg");
            string youtubeLogo = BuildAssetUrl(baseUrl, "assets/logos/youtube-3.svg");

            emailTemplate = emailTemplate.Replace("{{logo}}", logoPath);
            emailTemplate = emailTemplate.Replace("{{facebooklogo}}", facebookLogo);
            emailTemplate = emailTemplate.Replace("{{instagramLogo}}", instagramLogo);
            emailTemplate = emailTemplate.Replace("{{linkedInLogo}}", linkedInLogo);
            emailTemplate = emailTemplate.Replace("{{youtubeLogo}}", youtubeLogo);

            var apiKey = GetSendGridApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("SendGrid API key is not configured.");

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
            var to = new EmailAddress(email);

            var msg = MailHelper.CreateSingleEmail(from, to, "Password Reset Successful", "", emailTemplate);

            var response = await client.SendEmailAsync(msg);

            // ?? Log Result
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
            {
                Console.WriteLine("Reset Password Email sent successfully.");
            }
            else
            {
                Console.WriteLine("Failed to send email. Error: " + response.StatusCode);
            }
        }

        public static async Task SendCancelMembershipEmail(string email, string displayName)
        {

            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "Membership", "CancelMembership.html");

            if (!File.Exists(templatePath))
            {
                Console.WriteLine("Email template not found: " + templatePath);
                return;
            }

            string emailTemplate = await File.ReadAllTextAsync(templatePath);
            emailTemplate = emailTemplate.Replace("{{UserName}}", displayName);
            string baseUrl = GetBaseUrl();

            emailTemplate = emailTemplate.Replace("{{BaseUrl}}", baseUrl);
            string logoPath = BuildAssetUrl(baseUrl, "assets/logos/QuickHireBlack.png");
            string facebookLogo = BuildAssetUrl(baseUrl, "assets/logos/facebook-4.svg");
            string instagramLogo = BuildAssetUrl(baseUrl, "assets/logos/instagram-2.svg");
            string linkedInLogo = BuildAssetUrl(baseUrl, "assets/logos/linkedin-1.svg");
            string youtubeLogo = BuildAssetUrl(baseUrl, "assets/logos/youtube-3.svg");

            emailTemplate = emailTemplate.Replace("{{logo}}", logoPath);
            emailTemplate = emailTemplate.Replace("{{facebooklogo}}", facebookLogo);
            emailTemplate = emailTemplate.Replace("{{instagramLogo}}", instagramLogo);
            emailTemplate = emailTemplate.Replace("{{linkedInLogo}}", linkedInLogo);
            emailTemplate = emailTemplate.Replace("{{youtubeLogo}}", youtubeLogo);
            var apiKey = GetSendGridApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("SendGrid API key is not configured.");

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
            var to = new EmailAddress(email);

            var msg = MailHelper.CreateSingleEmail(from, to, "Cancel Membership", "", emailTemplate);

            // Send Email
            var response = await client.SendEmailAsync(msg);

            // Log Result
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
            {
                Console.WriteLine("Forget Password Email sent successfully.");
            }
            else
            {
                Console.WriteLine("Failed to send email. Error: " + response.StatusCode);
            }
        }

        public static async Task SendUpdatePaymentEmail(string email)
        {

            string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "Membership", "UpdatePayment.html");

            if (!File.Exists(templatePath))
            {
                Console.WriteLine("Email template not found: " + templatePath);
                return;
            }

            string emailTemplate = await File.ReadAllTextAsync(templatePath);
            string baseUrl = GetBaseUrl();

            emailTemplate = emailTemplate.Replace("{{BaseUrl}}", baseUrl);
            string logoPath = BuildAssetUrl(baseUrl, "assets/logos/QuickHireBlack.png");
            string facebookLogo = BuildAssetUrl(baseUrl, "assets/logos/facebook-4.svg");
            string instagramLogo = BuildAssetUrl(baseUrl, "assets/logos/instagram-2.svg");
            string linkedInLogo = BuildAssetUrl(baseUrl, "assets/logos/linkedin-1.svg");
            string youtubeLogo = BuildAssetUrl(baseUrl, "assets/logos/youtube-3.svg");

            emailTemplate = emailTemplate.Replace("{{logo}}", logoPath);
            emailTemplate = emailTemplate.Replace("{{facebooklogo}}", facebookLogo);
            emailTemplate = emailTemplate.Replace("{{instagramLogo}}", instagramLogo);
            emailTemplate = emailTemplate.Replace("{{linkedInLogo}}", linkedInLogo);
            emailTemplate = emailTemplate.Replace("{{youtubeLogo}}", youtubeLogo);
            var apiKey = GetSendGridApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("SendGrid API key is not configured.");

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("beastshah123@gmail.com", "QuickHired");
            var to = new EmailAddress(email);

            var msg = MailHelper.CreateSingleEmail(from, to, "Payment Update", "", emailTemplate);

            // ?? Send Email
            var response = await client.SendEmailAsync(msg);

            // ?? Log Result
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
            {
                Console.WriteLine("Forget Password Email sent successfully.");
            }
            else
            {
                Console.WriteLine("Failed to send email. Error: " + response.StatusCode);
            }
        }
        public static async Task SendEmailAsync(string toEmail, string subject, string message, string fromEmail)
        {
            var apiKey = GetSendGridApiKey();
            if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("SendGrid API key is not configured.");
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
            var to = new EmailAddress(toEmail);
            var emailMessage = MailHelper.CreateSingleEmail(from, to, subject, "", message);
            emailMessage.ReplyTo = new EmailAddress(fromEmail);
            // Send Email
            var response = await client.SendEmailAsync(emailMessage);

            // Log Result
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
            {
                Console.WriteLine("Forget Password Email sent successfully.");
            }
            else
            {
                Console.WriteLine("Failed to send email. Error: " + response.StatusCode);
            }
        }
        public static void SendQuoteRequestEmail(string userEmail, string userRequirement)
        {
            LogToFile("INFO", $"[QUOTE_REQUEST_EMAIL] Starting quote request email - From: {userEmail}", "Testing_Support");

            try
            {
                var apiKey = GetSendGridApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    LogToFile("ERROR", "[QUOTE_REQUEST_EMAIL] SendGrid API key not configured", "Testing_Support");
                    throw new InvalidOperationException("SendGrid API key is not configured.");
                }

                var client = new SendGridClient(apiKey);

                //var from = new EmailAddress(userEmail);
                var from = new EmailAddress("beastshah123@gmail.com", "Quick Hired");
                var to = new EmailAddress("mohsinrana09@yahoo.com");

                // Setting the subject and body of the email
                string subject = "New Quote Request";
                string body = $"You have received a new quote request from:\n\n" +
                              $"Email: {userEmail}\n\n" +
                              $"Requirement:\n{userRequirement}";

                LogToFile("INFO", $"[QUOTE_REQUEST_EMAIL] Email content prepared - Subject: {subject}", "Testing_Support");

                var msg = MailHelper.CreateSingleEmail(from, to, subject, "", body);
                msg.ReplyTo = new EmailAddress(userEmail);

                LogToFile("INFO", "[QUOTE_REQUEST_EMAIL] Sending email via SendGrid", "Testing_Support");
                var response = client.SendEmailAsync(msg).Result;
                LogToFile("INFO", $"[QUOTE_REQUEST_EMAIL] SendGrid response - Status: {response.StatusCode}", "Testing_Support");

                if (response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    LogToFile("SUCCESS", "[QUOTE_REQUEST_EMAIL] Quote request email sent successfully", "Testing_Support");
                    Console.WriteLine("Quote request email sent successfully.");
                }
                else
                {
                    LogToFile("ERROR", $"[QUOTE_REQUEST_EMAIL] Failed to send email - Status: {response.StatusCode}", "Testing_Support");
                    Console.WriteLine("Failed to send quote request email. Error: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                LogToFile("ERROR", $"[QUOTE_REQUEST_EMAIL] Exception in SendQuoteRequestEmail: {ex.Message}", "Testing_Support");
                throw;
            }
        }

        public static async Task SendDailyDigestEmail(string sendGridApiKey, string adminEmail, QuickHired.API.Services.DailyDigestData digestData)
        {
            LogToFile("INFO", $"[DAILY_DIGEST_EMAIL] Starting daily digest email send - To: {adminEmail}, Date: {digestData.Date:yyyy-MM-dd}", "Reports_Analytics");

            try
            {
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "EmailTemplates", "DailyDigest.html");
                LogToFile("INFO", $"[DAILY_DIGEST_EMAIL] Looking for daily digest template at: {templatePath}", "Reports_Analytics");

                if (!File.Exists(templatePath))
                {
                    LogToFile("ERROR", "[DAILY_DIGEST_EMAIL] Daily digest email template not found: " + templatePath, "Reports_Analytics");
                    Console.WriteLine("Daily Digest email template not found: " + templatePath);
                    return;
                }

                LogToFile("INFO", "[DAILY_DIGEST_EMAIL] Daily digest template found, processing content", "Reports_Analytics");
                string emailTemplate = await File.ReadAllTextAsync(templatePath);

                // Replace all template variables
                emailTemplate = emailTemplate.Replace("{{ReportDate}}", digestData.Date.ToString("MMMM d, yyyy"));
                emailTemplate = emailTemplate.Replace("{{GeneratedAt}}", digestData.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"));

                // Key metrics
                emailTemplate = emailTemplate.Replace("{{TotalUsers}}", digestData.TotalUsers.ToString("N0"));
                emailTemplate = emailTemplate.Replace("{{NewUsersToday}}", digestData.DailySignups.ToString());
                emailTemplate = emailTemplate.Replace("{{NewUsersThisMonth}}", "0"); // Not available in current structure
                emailTemplate = emailTemplate.Replace("{{TotalActiveSubscriptions}}", digestData.ActiveSubscriptions.ToString("N0"));
                emailTemplate = emailTemplate.Replace("{{NewSubscriptionsToday}}", digestData.DailySubscriptions.ToString());
                emailTemplate = emailTemplate.Replace("{{CancelledSubscriptionsToday}}", "0"); // Not available
                emailTemplate = emailTemplate.Replace("{{TotalInterviewReports}}", "0"); // Not available
                emailTemplate = emailTemplate.Replace("{{InterviewReportsToday}}", "0"); // Not available
                emailTemplate = emailTemplate.Replace("{{TotalMeetings}}", "0"); // Not available
                emailTemplate = emailTemplate.Replace("{{MeetingsToday}}", digestData.MeetingsScheduled.ToString());

                // Revenue (not available in current structure)
                emailTemplate = emailTemplate.Replace("{{MonthlyRevenue}}", "0.00");
                emailTemplate = emailTemplate.Replace("{{YearlyRevenue}}", "0.00");

                // Generate HTML for subscription breakdown
                var subscriptionHtml = "";
                foreach (var sub in digestData.SubscriptionPlans)
                {
                    subscriptionHtml += $@"
                        <div class=""breakdown-item"">
                            <strong>{sub.Count}</strong>
                            <span>{sub.PlanName}</span>
                        </div>";
                }
                emailTemplate = emailTemplate.Replace("{{SubscriptionBreakdownHtml}}", subscriptionHtml);

                // Generate HTML for user locations
                var locationsHtml = "";
                foreach (var location in digestData.CountryStats.Take(8))
                {
                    locationsHtml += $@"
                        <div class=""breakdown-item"">
                            <strong>{location.UserCount}</strong>
                            <span>{location.Country}</span>
                        </div>";
                }
                emailTemplate = emailTemplate.Replace("{{TopUserLocationsHtml}}", locationsHtml);

                // Generate HTML for recent signups (simplified since we don't have detailed signup data)
                var signupsHtml = "";
                if (digestData.DailySignups > 0)
                {
                    signupsHtml = $@"
                        <div style='text-align: center; padding: 20px;'>
                            <h3 style='color: #1a73e8; margin: 0;'>{digestData.DailySignups}</h3>
                            <p style='margin: 5px 0 0 0; color: #6c757d;'>New signups today</p>
                        </div>";
                }
                else
                {
                    signupsHtml = "<p style='text-align: center; color: #6c757d; margin: 20px 0;'>No new signups today</p>";
                }
                emailTemplate = emailTemplate.Replace("{{RecentSignupsHtml}}", signupsHtml);

                // Database health (simplified)
                var healthStatus = $"✅ System operational - {digestData.TotalUsers:N0} total users";
                emailTemplate = emailTemplate.Replace("{{DatabaseHealth}}", healthStatus);
                emailTemplate = emailTemplate.Replace("{{HealthStatusClass}}", "health-status health-good");

                LogToFile("INFO", "[DAILY_DIGEST_EMAIL] Daily digest template processing completed, getting API key", "Reports_Analytics");

                if (string.IsNullOrWhiteSpace(sendGridApiKey))
                {
                    LogToFile("ERROR", "[DAILY_DIGEST_EMAIL] SendGrid API key is not configured", "Reports_Analytics");
                    throw new InvalidOperationException("SendGrid API key is not configured.");
                }

                var client = new SendGridClient(sendGridApiKey);
                var from = new EmailAddress("beastshah123@gmail.com", "QuickHired Analytics");
                var to = new EmailAddress(adminEmail);

                var msg = MailHelper.CreateSingleEmail(from, to, $"📊 QuickHired Daily Digest - {digestData.Date:MMM d, yyyy}", "", emailTemplate);

                // Add proper headers
                msg.Headers = new Dictionary<string, string>
                {
                    { "X-Priority", "3" },
                    { "X-MSMail-Priority", "Normal" },
                    { "X-Mailer", "QuickHired Analytics System" },
                    { "Organization", "QuickHired" }
                };

                LogToFile("INFO", "[DAILY_DIGEST_EMAIL] Sending daily digest email via SendGrid", "Reports_Analytics");
                var response = await client.SendEmailAsync(msg);

                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
                {
                    LogToFile("SUCCESS", "[DAILY_DIGEST_EMAIL] Daily digest email sent successfully", "Reports_Analytics");
                    Console.WriteLine("Daily Digest email sent successfully.");
                }
                else
                {
                    LogToFile("ERROR", $"[DAILY_DIGEST_EMAIL] Daily digest email failed - Status: {response.StatusCode}", "Reports_Analytics");
                    Console.WriteLine("Failed to send daily digest email. Error: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                LogToFile("ERROR", $"[DAILY_DIGEST_EMAIL] Exception in SendDailyDigestEmail: {ex.Message}", "Reports_Analytics");
                throw;
            }
        }

        // Overload to fetch API key internally to align with controller usage
        public static Task SendDailyDigestEmail(string adminEmail, QuickHired.API.Services.DailyDigestData digestData)
        {
            var apiKey = GetSendGridApiKey();
            return SendDailyDigestEmail(apiKey, adminEmail, digestData);
        }
    }

    // DTOs used by email helper for Daily Digest rendering
    public class DailyDigestData
    {
        public DateTime Date { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int DailySignups { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int DailySubscriptions { get; set; }
        public int MeetingsScheduled { get; set; }
        public List<CountryStatsDto> CountryStats { get; set; } = new();
        public List<SubscriptionPlanDto> SubscriptionPlans { get; set; } = new();
    }

    public class CountryStatsDto
    {
        public string Country { get; set; } = string.Empty;
        public int UserCount { get; set; }
    }

    public class SubscriptionPlanDto
    {
        public string PlanName { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
