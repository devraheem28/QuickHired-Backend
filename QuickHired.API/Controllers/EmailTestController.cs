using Microsoft.AspNetCore.Mvc;
using QuickHired.API.Helpers;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailTestController : ControllerBase
    {
        /// <summary>
        /// Test endpoint to diagnose email sending issues
        /// </summary>
        [HttpPost("test-send")]
        public async Task<IActionResult> TestSendEmail([FromBody] TestEmailRequest request)
        {
            try
            {
                var apiKey = "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";
                var client = new SendGridClient(apiKey);
                var from = new EmailAddress("info@eventify-pro.com", "QuickHired Test");
                var to = new EmailAddress(request.Email);

                var subject = "QuickHired Email Test";
                var plainTextContent = "This is a test email from QuickHired API.";
                var htmlContent = @"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #064a60;'>QuickHired Email Test</h2>
                    <p>This is a test email to verify email functionality is working.</p>
                    <p><strong>Timestamp:</strong> " + DateTime.Now.ToString() + @"</p>
                    <p style='color: #666;'>If you received this email, the email system is working correctly.</p>
                </body>
                </html>";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
                var response = await client.SendEmailAsync(msg);

                var result = new
                {
                    StatusCode = (int)response.StatusCode,
                    StatusDescription = response.StatusCode.ToString(),
                    Success = response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted,
                    Headers = response.Headers?.ToDictionary(h => h.Key, h => h.Value.FirstOrDefault()),
                    Body = response.Body != null ? await response.Body.ReadAsStringAsync() : null
                };

                if (result.Success)
                {
                    return Ok(new { Message = "Email sent successfully!", Details = result });
                }
                else
                {
                    return BadRequest(new { Message = "Failed to send email", Details = result });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "Email sending failed with exception",
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test template loading
        /// </summary>
        [HttpGet("test-template")]
        public IActionResult TestTemplateLoading()
        {
            try
            {
                var currentDir = Directory.GetCurrentDirectory();
                var templatePath = Path.Combine(currentDir, "wwwroot", "EmailTemplates", "LoginTemplates", "LoginTemplate.html");

                var result = new
                {
                    CurrentDirectory = currentDir,
                    TemplatePath = templatePath,
                    TemplateExists = System.IO.File.Exists(templatePath),
                    WwwrootExists = Directory.Exists(Path.Combine(currentDir, "wwwroot")),
                    EmailTemplatesExists = Directory.Exists(Path.Combine(currentDir, "wwwroot", "EmailTemplates")),
                    LoginTemplatesExists = Directory.Exists(Path.Combine(currentDir, "wwwroot", "EmailTemplates", "LoginTemplates"))
                };

                if (result.TemplateExists)
                {
                    var content = System.IO.File.ReadAllText(templatePath);
                    return Ok(new
                    {
                        result.CurrentDirectory,
                        result.TemplatePath,
                        result.TemplateExists,
                        result.WwwrootExists,
                        result.EmailTemplatesExists,
                        result.LoginTemplatesExists,
                        TemplateContentLength = content.Length,
                        TemplatePreview = content.Substring(0, Math.Min(200, content.Length)) + "..."
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "Template loading test failed",
                    Error = ex.Message
                });
            }
        }
    }

    public class TestEmailRequest
    {
        public string Email { get; set; } = "";
    }
}