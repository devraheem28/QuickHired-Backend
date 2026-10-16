using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickHired.API.DTOs;
using QuickHired.API.Helpers;
using QuickHired.API.Interfaces.Services;
using QuickHired.API.Models;
using QuickHired.BOL;
using QuickHired.DAL.Data;
using SendGrid.Helpers.Mail;
using System.Net;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using PnP.Framework;
using Google.Apis.Auth;
using static Google.Apis.Auth.GoogleJsonWebSignature;
using Newtonsoft.Json;
using QuickHired.API.DTOs;


namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SecurityMasterController : ControllerHandler
    {
        private readonly UserManager<AppUsers> _userManager;
        private readonly SignInManager<AppUsers> _signInManager;
        private readonly AppDbContext _identityDbContext;
        private readonly ITokenService _tokenService;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string SessionCode = "EmailConfirmationCode";
        private const string SessionDate = "SessionDate";
        private readonly AppDbContext _dbContext;
        private readonly IConfiguration _config;
        private readonly ILogger<SecurityMasterController> _logger;
        public SecurityMasterController(IConfiguration config, UserManager<AppUsers> userManager,
                                         SignInManager<AppUsers> signInManager,
                                         AppDbContext identityDbContext,
                                         ITokenService tokenService,
                                         IMapper mapper,
                                         AppDbContext dbContext,
                                         IHttpContextAccessor httpContextAccessor,
                                         ILogger<SecurityMasterController> logger
                                         )
        {
            _config = config;
            _userManager = userManager;
            _signInManager = signInManager;
            _identityDbContext = identityDbContext;
            _tokenService = tokenService;
            _mapper = mapper;
            _logger = logger;
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;

        }

        [HttpPost("support-submit")]
        [AllowAnonymous]
        public async Task<IActionResult> SupportSubmit([FromBody] SupportRequestDTO req)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email)
                    || string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Message))
                {
                    return BadRequest(new { message = "Invalid support request payload." });
                }

                var adminEmail = _config["AdminEmail"] ?? "mohsinrana09@yahoo.com";
                var sendGridApiKey = _config["SendGrid:ApiKey"] ?? Environment.GetEnvironmentVariable("SENDGRID_API_KEY");

                if (string.IsNullOrWhiteSpace(sendGridApiKey))
                {
                    LoggingHelper.LogError("Authentication_User_Management", "SUPPORT_SUBMIT_CONFIG",
                        "SendGrid API key is not configured for support form submission", null, _logger);
                    return StatusCode(500, new { message = "Email service not configured." });
                }

                await EmailHelper.SendSupportEmailToAdmin(sendGridApiKey, adminEmail, req.Name, req.Email, req.Subject, req.Message);
                return Ok(new { message = "Support request submitted." });
            }
            catch (Exception ex)
            {
                LoggingHelper.LogError("Authentication_User_Management", "SUPPORT_SUBMIT",
                    $"Failed to process support request from {req?.Email}", ex, _logger);
                return StatusCode(500, new { message = "Failed to submit support request." });
            }
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO loginDto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(loginDto.Email);
                if (user is null)
                {
                    LoggingHelper.LogError("Authentication_User_Management", "USER_LOGIN",
                        $"Login attempt failed - User does not exist: {loginDto.Email}", null, _logger);
                    return Unauthorized(new { isError = true, status = 401, message = "User does not exist." });
                }

                // Check if the user is verified
                bool isVerified = await _dbContext.VerificationCodes
                    .AnyAsync(v => v.UserId == loginDto.Email && v.IsVerified);

                if (!isVerified)
                {
                    LoggingHelper.LogError("Authentication_User_Management", "USER_LOGIN",
                        $"Login attempt failed - Email not verified: {loginDto.Email}", null, _logger);
                    return Unauthorized(new { isError = true, status = 401, message = "Email not verified. Please verify your email." });
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();

                if (!result.Succeeded)
                {
                    LoggingHelper.LogError("Authentication_User_Management", "USER_LOGIN",
                        $"Login attempt failed - Invalid credentials for: {loginDto.Email}", null, _logger);
                    return Unauthorized(new { isError = true, status = 401, message = "Email/Password is incorrect." });
                }

                // Send email notification on successful login
                try
                {
                    await EmailHelper.SendLoginEmail(loginDto.Email, user.FirstName);
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogError("Authentication_User_Management", "USER_LOGIN_EMAIL",
                        $"Login email failed for: {loginDto.Email}", ex, _logger);
                }

                // Return success response with token
                return Ok(new
                {
                    isError = false,
                    status = 200,
                    message = "Login successful",
                    token = _tokenService.CreateToken(user),
                    displayName = user.FirstName,
                    email = user.Email,
                    role = role
                });
            }
            catch (Exception ex)
            {
                LoggingHelper.LogError("Authentication_User_Management", "USER_LOGIN",
                    $"Unexpected error during login for: {loginDto.Email}", ex, _logger);
                return StatusCode(500, new { isError = true, status = 500, message = "Internal Server Error", error = ex.Message });
            }
        }


        [Authorize]
        [HttpGet]
        public async Task<ApiResponse<UserDTO>> GetCurrentUser()
        {
            return await DoTryAsync(async () =>
            {
                var email = User.FindFirstValue(ClaimTypes.Email)?.ToString();
                var user = await _userManager.FindByEmailAsync(email);
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault();
                return new UserDTO
                {
                    Email = user.Email,
                    Token = _tokenService.CreateToken(user),
                    DisplayName = user.FirstName,
                    LastName = user.LastName,
                    UserId = user.Id,
                    Role = role

                };
            }, nameof(SecurityMasterController), nameof(GetCurrentUser), _logger);
        }
        [HttpGet("emailexists")]
        public async Task<bool> CheckEmailExitsAsync([FromQuery] string email)
        {
            return await IsEmailAlreadyExistAsync(email);
        }
        private async Task<bool> IsEmailAlreadyExistAsync(string email) =>
            await _userManager.FindByEmailAsync(email) != null;

        [HttpPost("super-admin-register")]
        public async Task<ApiResponse<UserDTO>> SuperAdminRegister(RegisterDTO registerDTO)
        {
            return await DoTryAsync(async () =>
            {
                // Check if email already exists
                bool isEmailExist = await IsEmailAlreadyExistAsync(registerDTO.Email);
                if (isEmailExist)
                    throw new BadHttpRequestException("Email address is already in use.");

                var user = new AppUsers
                {
                    FirstName = registerDTO.FirstName,
                    LastName = registerDTO.LastName,
                    Email = registerDTO.Email,
                    UserName = registerDTO.Email,
                    JobTitle = registerDTO.JobTitle,
                    YearsOfExperience = registerDTO.YearsOfExperience,
                    ExperienceLevel = registerDTO.ExperienceLevel,
                    Country = registerDTO.Country,
                    City = registerDTO.City,
                    Address = registerDTO.Address,
                    State = registerDTO.State,
                    ZipCode = registerDTO.ZipCode,
                    Education = registerDTO.Education,
                    LinkedInProfile = registerDTO.LinkedInProfile,
                    ResumeUrl = registerDTO.ResumeUrl,
                    Skills = registerDTO.Skills,
                };

                var result = await _userManager.CreateAsync(user, registerDTO.Password);
                if (!result.Succeeded)
                    throw new BadHttpRequestException("User creation failed.");

                // Assign "Super Admin" role to the user
                var roleAssignResult = await _userManager.AddToRoleAsync(user, "Super Admin");
                if (!roleAssignResult.Succeeded)
                    throw new BadHttpRequestException("Role assignment failed.");

                var code = await GenerateAndStoreEmailConfirmationCodeAsync(6, registerDTO.Email);
                try
                {
                    await EmailHelper.SendRegistrationEmail(user.Email, user.FirstName, code);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Registration email failed: {ex.Message}");
                }


                await _dbContext.SaveChangesAsync();

                return new UserDTO
                {
                    Email = user.Email,
                    Token = _tokenService.CreateToken(user),
                    DisplayName = user.FirstName
                };
            }, nameof(SecurityMasterController), nameof(Register), _logger);
        }
        [HttpPost("generate-email-code")]
        public async Task<string> GenerateAndStoreEmailConfirmationCodeAsync([FromBody] int codeLength, string email)
        {
            var userId = email;


            var code = GenerateRandomCode(codeLength);
            var expiryTime = DateTime.UtcNow.AddMinutes(5); // Code expires in 5 minutes

            var verificationCode = new VerificationCodes
            {
                UserId = userId,
                Code = code,
                ExpiryTime = expiryTime,
                IsVerified = false
            };

            _dbContext.VerificationCodes.Add(verificationCode);
            await _dbContext.SaveChangesAsync();

            //Console.WriteLine($"Generated Code: {code}");
            //Console.WriteLine($"Stored Expiry Time: {expiryTime:o}");

            return code;
        }

        public class VerifyCodeRequest
        {
            public string Code { get; set; }
            public string Email { get; set; }
        }

        [HttpPost("verify-email-code")]
        public async Task<bool> VerifyEmailConfirmationCodeAsync([FromBody] VerifyCodeRequest request)
        {
            if (string.IsNullOrEmpty(request.Email)) return false;

            var userId = request.Email;

            var verificationCode = await _dbContext.VerificationCodes
                .Where(vc => vc.UserId == userId && vc.IsVerified == false)
                .OrderByDescending(vc => vc.ExpiryTime)
                .FirstOrDefaultAsync();

            if (verificationCode == null || verificationCode.Code != request.Code)
            {
                Console.WriteLine("Invalid Code.");
                return false;
            }

            if (DateTime.UtcNow > verificationCode.ExpiryTime)
            {
                Console.WriteLine("Code Expired.");
                return false;
            }

            verificationCode.IsVerified = true;
            _dbContext.Entry(verificationCode).State = EntityState.Modified;

            int rowsAffected = await _dbContext.SaveChangesAsync();

            Console.WriteLine("Code Verified Successfully!");
            return true;
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(req.Credential, new ValidationSettings
                {
                    Audience = new List<string> { _config["Authentication:Google:ClientId"] }
                });
                var user = await _userManager.FindByEmailAsync(payload.Email);
                if (user == null)
                {
                    user = new AppUsers
                    {
                        Email = payload.Email,
                        UserName = payload.Email,
                        FirstName = payload.GivenName,
                        LastName = payload.FamilyName,
                        EmailConfirmed = true,
                        JobTitle = "Google User",
                        YearsOfExperience = 0,
                        ExperienceLevel = "Beginner",
                        Country = "N/A",
                        City = "N/A",
                        Address = "N/A",
                        State = "N/A",
                        ZipCode = "00000",
                        Education = "Not Specified",
                        LinkedInProfile = "",
                        ResumeUrl = "",
                        Skills = ""
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                        return BadRequest(new { message = "Google registration failed" });

                    var subscription = new UserSubscriptions
                    {
                        UserId = user.Id,
                        PlanName = "Free",
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.UserSubscriptions.Add(subscription);
                    await _dbContext.SaveChangesAsync();
                    try
                    {
                        await EmailHelper.SendRegistrationEmailSocial(user.Email, user.FirstName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Social registration email failed: {ex.Message}");
                    }
                }

                var token = _tokenService.CreateToken(user);
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "";

                return Ok(new
                {
                    isError = false,
                    token,
                    role
                });
            }
            catch
            {
                return Unauthorized(new { isError = true, message = "Invalid Google token" });
            }
        }
        public class GoogleLoginRequest
        {
            public string Credential { get; set; } = default!;
        }
        private string GenerateRandomCode(int length)
        {
            const string validChars = "0123456789";
            var randomBytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            var sb = new StringBuilder(length);
            foreach (var b in randomBytes)
            {
                sb.Append(validChars[b % validChars.Length]);
            }
            return sb.ToString();
        }

        public class FacebookLoginRequest
        {
            public string AccessToken { get; set; }
        }

        [HttpPost("facebook-login")]
        public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginRequest req)
        {
            try
            {
                var httpClient = new HttpClient();
                var result = await httpClient.GetStringAsync(
                    $"https://graph.facebook.com/me?access_token={req.AccessToken}&fields=id,email,first_name,last_name");

                dynamic profile = JsonConvert.DeserializeObject(result);
                string email = profile.email;

                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new AppUsers
                    {
                        Email = email,
                        UserName = email,
                        FirstName = profile.first_name,
                        LastName = profile.last_name,
                        EmailConfirmed = true,

                        // Default values for required fields
                        JobTitle = "Facebook User",
                        YearsOfExperience = 0,
                        ExperienceLevel = "Beginner",
                        Country = "N/A",
                        City = "N/A",
                        Address = "N/A",
                        State = "N/A",
                        ZipCode = "00000",
                        Education = "Not Specified",
                        LinkedInProfile = "",
                        ResumeUrl = "",
                        Skills = ""
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                        return BadRequest(new { message = "Facebook registration failed" });

                    var subscription = new UserSubscriptions
                    {
                        UserId = user.Id,
                        PlanName = "Free",
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.UserSubscriptions.Add(subscription);
                    await _dbContext.SaveChangesAsync();
                    try
                    {
                        await EmailHelper.SendRegistrationEmailSocial(user.Email, user.FirstName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Social registration email failed: {ex.Message}");
                    }
                }

                var token = _tokenService.CreateToken(user);
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "";

                return Ok(new
                {
                    isError = false,
                    token,
                    role
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { isError = true, message = "Invalid Facebook token", error = ex.Message });
            }
        }

        [HttpPost("register")]
        public async Task<ApiResponse<UserDTO>> Register(RegisterDTO registerDTO)
        {
            return await DoTryAsync(async () =>
            {
                // Check if email already exists
                bool isEmailExist = await IsEmailAlreadyExistAsync(registerDTO.Email);
                if (isEmailExist)
                    throw new BadHttpRequestException("Email address is already in use.");

                var user = new AppUsers
                {
                    FirstName = registerDTO.FirstName,
                    LastName = registerDTO.LastName,
                    Email = registerDTO.Email,
                    UserName = registerDTO.Email,
                    JobTitle = registerDTO.JobTitle,
                    YearsOfExperience = registerDTO.YearsOfExperience,
                    ExperienceLevel = registerDTO.ExperienceLevel,
                    Country = registerDTO.Country,
                    City = registerDTO.City,
                    Address = registerDTO.Address,
                    State = registerDTO.State,
                    ZipCode = registerDTO.ZipCode,
                    Education = registerDTO.Education,
                    LinkedInProfile = registerDTO.LinkedInProfile,
                    ResumeUrl = registerDTO.ResumeUrl,
                    Skills = registerDTO.Skills,
                };

                var result = await _userManager.CreateAsync(user, registerDTO.Password);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    LoggingHelper.LogError("Authentication_User_Management", "USER_REGISTRATION",
                        $"User registration failed for {registerDTO.Email}: {errors}", null, _logger);
                    throw new BadHttpRequestException("User creation failed.");
                }

                var code = await GenerateAndStoreEmailConfirmationCodeAsync(6, registerDTO.Email);

                try
                {
                    await EmailHelper.SendRegistrationEmail(user.Email, user.FirstName, code);
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogError("Authentication_User_Management", "REGISTRATION_EMAIL",
                        $"Registration email failed for: {user.Email}", ex, _logger);
                }

                var subscription = new UserSubscriptions
                {
                    UserId = user.Id,
                    PlanName = "Free",
                    CreatedAt = DateTime.UtcNow
                };

                try
                {
                    _dbContext.UserSubscriptions.Add(subscription);
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogError("Authentication_User_Management", "SUBSCRIPTION_CREATION",
                        $"Failed to create subscription for user: {user.Id}", ex, _logger);
                    throw;
                }
                return new UserDTO
                {
                    Email = user.Email,
                    Token = _tokenService.CreateToken(user),
                    DisplayName = user.FirstName
                };
            }, nameof(SecurityMasterController), nameof(Register), _logger);
        }
        public class ResendCodeRequest
        {
            public string Email { get; set; }
        }

        [HttpPost("quote-submit")]
        public IActionResult SendQuoteRequest([FromForm] string userEmail, [FromForm] string userRequirement)
        {
            try
            {
                EmailHelper.SendQuoteRequestEmail(userEmail, userRequirement);
                return Ok(new { success = true, message = "Your quote request has been submitted successfully!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Server error occurred." });
            }
        }

        [HttpPost("resend-email-code")]
        public async Task<ApiResponse<string>> ResendEmailCode([FromBody] ResendCodeRequest request)
        {
            return await DoTryAsync(async () =>
            {
                // Validate if email exists in the system
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                    throw new BadHttpRequestException("User not found.");

                var code = await GenerateAndStoreEmailConfirmationCodeAsync(6, request.Email);

                // Send email with new verification code
                try
                {
                    await EmailHelper.SendRegistrationEmail(user.Email, user.FirstName, code);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Resend email failed: {ex.Message}");
                }

                return "Verification code has been resent successfully.";
            }, nameof(SecurityMasterController), nameof(ResendEmailCode), _logger);
        }


        [HttpPost("save-subscription")]
        public async Task<IActionResult> SaveSubscription([FromBody] SubscriptionPlanDto model)
        {
            if (model == null)
            {
                return BadRequest("Invalid subscription data.");
            }

            // Check if a subscription already exists for the user
            var existingSubscription = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == model.Email);

            if (existingSubscription != null)
            {
                // Update the existing subscription
                existingSubscription.PlanName = model.PlanName;
                existingSubscription.CreatedAt = DateTime.UtcNow;
                existingSubscription.Status = true;
                _dbContext.Entry(existingSubscription).State = EntityState.Modified;
            }
            else
            {
                // Create a new subscription
                var subscription = new UserSubscriptions
                {
                    UserId = model.Email,
                    PlanName = model.PlanName,
                    CreatedAt = DateTime.UtcNow,
                    Status = true
                };

                _dbContext.UserSubscriptions.Add(subscription);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Subscription saved successfully!" });
        }

        [HttpGet("get-user-plan/{email}")]
        public async Task<IActionResult> GetUserPlan(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest("Email is required.");
            }
            var userSubscription = await _dbContext.UserSubscriptions
                .Where(s => s.UserId == email)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (userSubscription == null)
            {
                return NotFound(new { message = "No subscription found for this user." });
            }

            return Ok(new { planName = userSubscription.PlanName });
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("UploadProfilePhoto")]
        public async Task<IActionResult> UploadProfilePhoto([FromForm] IFormFile ProfilePhoto, [FromForm] string UserId)
        {
            if (ProfilePhoto == null || string.IsNullOrEmpty(UserId))
                return BadRequest("Invalid upload.");

            // Validate image type
            var extension = Path.GetExtension(ProfilePhoto.FileName);
            if (extension != ".jpg" && extension != ".png")
                return BadRequest("Only .jpg or .png allowed.");

            // Save path
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "logos");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await ProfilePhoto.CopyToAsync(stream);
            }

            var user = await _dbContext.AppUsers.FindAsync(UserId);
            if (user == null)
                return NotFound("User not found.");

            // Update only the ProfileImagePath field
            user.ProfileImagePath = $"/assets/logos/{uniqueFileName}";
            _dbContext.Entry(user).Property(u => u.ProfileImagePath).IsModified = true;
            await _dbContext.SaveChangesAsync();


            return Ok(new { message = "Profile photo uploaded successfully." });
        }
        [HttpGet("GetUserProfileImage/{id}")]
        public async Task<IActionResult> GetUserProfileImage(string id)
        {
            var user = await _dbContext.AppUsers.FindAsync(id);
            if (user == null)
                return NotFound();

            return Ok(new { imagePath = user.ProfileImagePath });
        }

        public class SubscriptionPlanDto
        {
            public string PlanName { get; set; }
            public string Email { get; set; }

        }
        public class SubscriptionDto
        {
            public string PlanName { get; set; }
            public string Email { get; set; }
            public DateTime? CreatedAt { get; set; }
            public string? CustomerId { get; set; }
            public string? SubscriptionId { get; set; }
        }

        //[HttpPost("forgotpassword")]
        //public async Task<ApiResponse<string>> ForgotPassword(ForgotPasswordDTO forgotPasswordDto)
        //{
        //    return await DoTryAsync(async () =>
        //    {
        //        var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
        //        if (user == null) throw new BadHttpRequestException("User not found");

        //        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        //        // Save the token to the database
        //        var passwordResetToken = new PasswordResetToken
        //        {
        //            UserId = user.Id,
        //            Token = token,
        //            ExpirationDate = DateTime.UtcNow.AddHours(1)
        //        };
        //        _identityDbContext.PasswordResetToken.Add(passwordResetToken);
        //        await _identityDbContext.SaveChangesAsync();

        //        var resetLink = Url.Action("ResetPassword", "SecurityMasterMaster",
        //                           new { token, email = user.Email }, Request.Scheme);

        //        // Send the email
        //        EmailHelper.SendForgetPasswordEmail(user.Email, user.FirstName, resetLink);
        //        return token;

        //    }, nameof(SecurityMasterController), nameof(ForgotPassword), _logger);
        //}

        [HttpPost("forgotpassword")]
        public async Task<ApiResponse<string>> ForgotPassword(ForgotPasswordDTO forgotPasswordDto)
        {
            return await DoTryAsync(async () =>
            {
                var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
                if (user == null)
                {
                    LoggingHelper.LogError("Authentication_User_Management", "FORGOT_PASSWORD",
                        $"Password reset requested for non-existent user: {forgotPasswordDto.Email}", null, _logger);
                    throw new BadHttpRequestException("User not found");
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                var passwordResetToken = new PasswordResetToken
                {
                    UserId = user.Id,
                    Token = token,
                    ExpirationDate = DateTime.UtcNow.AddHours(1)
                };

                try
                {
                    _identityDbContext.PasswordResetToken.Add(passwordResetToken);
                    await _identityDbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogError("Authentication_User_Management", "FORGOT_PASSWORD_DB",
                        $"Failed to save password reset token for user: {user.Id}", ex, _logger);
                    throw;
                }

                var requestHost = HttpContext.Request.Host.Host;

                // Default frontend URL
                string frontendUrl = "https://quickhired.com";

                if (requestHost.Contains("localhost"))
                {
                    frontendUrl = "http://localhost:4200";
                }

                // Prepare reset URL
                var resetLink = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email)}";

                try
                {
                    EmailHelper.SendForgetPasswordEmail(user.Email, user.FirstName, resetLink);
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogError("Authentication_User_Management", "FORGOT_PASSWORD_EMAIL",
                        $"Failed to send password reset email to: {user.Email}", ex, _logger);
                    // Continue execution as token is already saved
                }

                return token;

            }, nameof(SecurityMasterController), nameof(ForgotPassword), _logger);
        }


        public class AdminChangePasswordDTO
        {
            public string Email { get; set; }
            public string NewPassword { get; set; }
        }

        [HttpPost("changepassword")]
        public async Task<ApiResponse<string>> ChangePasswordByAdmin(AdminChangePasswordDTO dto)
        {
            return await DoTryAsync(async () =>
            {
                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user == null)
                    throw new BadHttpRequestException("User not found");

                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, resetToken, dto.NewPassword);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new BadHttpRequestException($"Password change failed: {errors}");
                }

                EmailHelper.SendResetPasswordEmail(user.Email, user.FirstName);
                return "Password changed successfully";

            }, nameof(SecurityMasterController), nameof(ChangePasswordByAdmin), _logger);
        }

        [HttpPost("resetpassword")]
        public async Task<ApiResponse<string>> ResetPassword(ResetPasswordDTO resetPasswordDto)
        {
            return await DoTryAsync(async () =>
            {
                var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email);
                if (user == null) throw new BadHttpRequestException("Invalid token or email");

                // Check if the token is valid and not expired
                var passwordResetToken = await _identityDbContext.PasswordResetToken
                    .FirstOrDefaultAsync(t => t.UserId == user.Id && t.Token == resetPasswordDto.Token && t.ExpirationDate > DateTime.UtcNow);

                if (passwordResetToken == null)
                {
                    throw new BadHttpRequestException("Invalid or expired token");
                }

                var result = await _userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.NewPassword);
                if (result.Succeeded)
                {
                    // Remove the used token from the database
                    _identityDbContext.PasswordResetToken.Remove(passwordResetToken);
                    await _identityDbContext.SaveChangesAsync();
                    EmailHelper.SendResetPasswordEmail(user.Email, user.FirstName);

                }
                else
                {
                    throw new BadHttpRequestException("Error resetting password");
                }
                return "Password has been reset successfully";

            }, nameof(SecurityMasterController), nameof(ResetPassword), _logger);
        }

        [HttpPost("changeemail")]
        public async Task<ApiResponse<string>> ChangeEmail(ChangeEmailDTO changeEmailDto)
        {
            return await DoTryAsync(async () =>
            {
                var user = await _userManager.FindByEmailAsync(changeEmailDto.CurrentEmail);
                if (user == null) throw new BadHttpRequestException("User not found");

                // Verify the password before changing the email
                //var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, changeEmailDto.Password, false);
                //if (!passwordCheck.Succeeded) throw new UnauthorizedAccessException("Incorrect password");

                // Check if the new email is already in use
                if (await _userManager.FindByEmailAsync(changeEmailDto.NewEmail) != null)
                    throw new BadHttpRequestException("The new email is already in use");

                // Change email
                user.Email = changeEmailDto.NewEmail;
                user.UserName = changeEmailDto.NewEmail;

                var result = await _userManager.UpdateAsync(user);
                var code = await GenerateAndStoreEmailConfirmationCodeAsync(6, changeEmailDto.NewEmail);
                EmailHelper.SendChangeEmail(changeEmailDto.NewEmail, user.FirstName, code);
                if (!result.Succeeded) throw new BadHttpRequestException("Failed to change email");

                return "Email changed successfully";

            }, nameof(SecurityMasterController), nameof(ChangeEmail), _logger);
        }
        [HttpPut("update-name")]
        public async Task<IActionResult> UpdateName([FromBody] UpdateNameDTO dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(dto.UserId);
                if (user == null)
                    return BadRequest("User not found");

                user.FirstName = dto.FirstName;
                user.LastName = dto.LastName;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return BadRequest($"Failed to update name: {errors}");
                }

                return Ok(new { message = "Name updated successfully" });
            }
            catch (Exception ex)
            {

                return StatusCode(500, new { message = "An unexpected error occurred.", details = ex.Message });
            }
        }


    }
}
