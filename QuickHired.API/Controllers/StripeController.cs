using Google.Apis.Calendar.v3.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickHired.API.Helpers;
using QuickHired.API.Services;
using QuickHired.BOL;
using QuickHired.DAL.Data;
using Stripe;
using Stripe.Checkout;
using Microsoft.Extensions.Logging;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StripeController : ControllerBase
    {
        private readonly StripeService _stripeService;
        private readonly AppDbContext _dbContext;
        private readonly UserManager<AppUsers> _userManager;
        private readonly ILogger<StripeController> _logger;

        public StripeController(StripeService stripeService, UserManager<AppUsers> userManager, ILogger<StripeController> logger, AppDbContext dbContext)
        {
            _stripeService = stripeService;
            _userManager = userManager;
            _logger = logger;
            _dbContext = dbContext;
        }
        [HttpPost("create-payment-intent")]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
        {
            try
            {
                var paymentIntent = await _stripeService.CreatePaymentIntentAsync(request.Amount, request.PaymentMethodId);
                return Ok(new
                {
                    PaymentIntentId = paymentIntent.Id,
                    ClientSecret = paymentIntent.ClientSecret
                });
            }
            catch (StripeException stripeEx)
            {
                LoggingHelper.LogError("Payment_Subscriptions", "CREATE_PAYMENT_INTENT",
                    $"Stripe error creating payment intent for amount: {request.Amount}, PaymentMethod: {request.PaymentMethodId} - {stripeEx.StripeError?.Message}",
                    stripeEx, _logger);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Payment service error", details = stripeEx.StripeError?.Message });
            }
            catch (Exception ex)
            {
                LoggingHelper.LogError("Payment_Subscriptions", "CREATE_PAYMENT_INTENT",
                    $"Unexpected error creating payment intent for amount: {request.Amount}", ex, _logger);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }
        [HttpPost("confirm-payment-intent")]
        public async Task<IActionResult> ConfirmPaymentIntent([FromBody] ConfirmPaymentIntentRequest request)
        {
            try
            {
                var paymentIntent = await _stripeService.ConfirmPaymentIntentAsync(request.PaymentIntentId, request.PaymentMethodId);
                return Ok(new
                {
                    PaymentIntentId = paymentIntent.Id,
                    Status = paymentIntent.Status
                });
            }
            catch (StripeException stripeEx)
            {
                LoggingHelper.LogError("Payment_Subscriptions", "CONFIRM_PAYMENT_INTENT",
                    $"Stripe error confirming payment intent: {request.PaymentIntentId} - {stripeEx.StripeError?.Message}",
                    stripeEx, _logger);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Payment confirmation failed", details = stripeEx.StripeError?.Message });
            }
            catch (Exception ex)
            {
                LoggingHelper.LogError("Payment_Subscriptions", "CONFIRM_PAYMENT_INTENT",
                    $"Unexpected error confirming payment intent: {request.PaymentIntentId}", ex, _logger);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpPost("stripe-webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    "whsec_FprqJixDRAYAHNUhgtla0fywn2jJYlB9"
                );

                if (stripeEvent.Type == "checkout.session.completed")
                {
                    try
                    {
                        var session = stripeEvent.Data.Object as Session;

                        var sessionService = new SessionService();
                        var fullSession = await sessionService.GetAsync(session.Id, new SessionGetOptions
                        {
                            Expand = new List<string> { "customer" }
                        });

                        var customerId = fullSession.CustomerId;
                        var userEmail = fullSession.CustomerEmail;
                        var subscriptionId = fullSession.SubscriptionId;
                        var planName = fullSession.Metadata.ContainsKey("PlanName") ? fullSession.Metadata["PlanName"] : null;

                        // Save or update subscription
                        var userSubscription = await _dbContext.UserSubscriptions
                            .FirstOrDefaultAsync(us => us.UserId == userEmail);

                        if (userSubscription != null)
                        {
                            userSubscription.SubscriptionKey = subscriptionId;
                            userSubscription.CustomerId = customerId;
                            _dbContext.Entry(userSubscription).State = EntityState.Modified;

                            await _dbContext.SaveChangesAsync();
                            Console.WriteLine($"Subscription updated: {subscriptionId} for {userEmail}");
                        }
                        else
                        {
                            var newSubscription = new UserSubscriptions
                            {
                                UserId = userEmail,
                                SubscriptionKey = subscriptionId,
                                CustomerId = customerId,
                                PlanName = planName
                            };

                            _dbContext.UserSubscriptions.Add(newSubscription);
                            await _dbContext.SaveChangesAsync();
                            Console.WriteLine($"Subscription created: {subscriptionId} for {userEmail}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingHelper.LogError("Payment_Subscriptions", "STRIPE_WEBHOOK_SESSION",
                            $"Error processing checkout session completion", ex, _logger);
                        return StatusCode(500, "Webhook processing failed");
                    }
                }

                return Ok();
            }
            catch (StripeException stripeEx)
            {
                LoggingHelper.LogError("Payment_Subscriptions", "STRIPE_WEBHOOK",
                    $"Stripe webhook validation failed: {stripeEx.StripeError?.Message}", stripeEx, _logger);
                return BadRequest("Invalid webhook");
            }
            catch (Exception ex)
            {
                LoggingHelper.LogError("Payment_Subscriptions", "STRIPE_WEBHOOK",
                    $"Unexpected error in Stripe webhook processing", ex, _logger);
                return StatusCode(500, "Webhook processing error");
            }
        }

        [HttpGet("get-payment-details/{customerId}")]
        public async Task<IActionResult> GetPaymentDetails(string customerId)
        {
            try
            {
                StripeConfiguration.ApiKey = "sk_test_51R2AHHEI7JiDsJpZ8KjwWbM6RlDVhMuLedVY2FgO8ExWDRFRVkomPJDpE3fjAfYCsjuIJBbMjpFzNvACPVXFF60700G8VtiiW4";

                var customerService = new CustomerService();
                var customer = await customerService.GetAsync(customerId);

                var paymentMethodService = new PaymentMethodService();
                var paymentMethods = await paymentMethodService.ListAsync(new PaymentMethodListOptions
                {
                    Customer = customerId,
                    Type = "card"
                });

                var card = paymentMethods.Data.FirstOrDefault()?.Card;

                var result = new
                {
                    Email = customer.Email,
                    CardBrand = card?.Brand,
                    Last4 = card?.Last4,
                    ExpMonth = card?.ExpMonth,
                    ExpYear = card?.ExpYear
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error: {ex.Message}");
                return StatusCode(500, "Something went wrong.");
            }
        }

        [HttpPost("update-payment-details")]
        public async Task<IActionResult> UpdatePaymentDetails([FromBody] UpdatePaymentDetailsRequest request)
        {
            try
            {
                StripeConfiguration.ApiKey = "sk_test_51R2AHHEI7JiDsJpZ8KjwWbM6RlDVhMuLedVY2FgO8ExWDRFRVkomPJDpE3fjAfYCsjuIJBbMjpFzNvACPVXFF60700G8VtiiW4";

                var customerService = new CustomerService();

                // 1. Update email if changed
                var updateOptions = new CustomerUpdateOptions
                {
                    Email = request.Email
                };
                await customerService.UpdateAsync(request.CustomerId, updateOptions);

                // 2. Attach new payment method (card)
                if (!string.IsNullOrEmpty(request.NewPaymentMethodId))
                {
                    var paymentMethodService = new PaymentMethodService();

                    // Attach to customer
                    await paymentMethodService.AttachAsync(
                        request.NewPaymentMethodId,
                        new PaymentMethodAttachOptions
                        {
                            Customer = request.CustomerId
                        });

                    // Set it as default
                    var updateCustomer = new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = request.NewPaymentMethodId
                        }
                    };

                    await customerService.UpdateAsync(request.CustomerId, updateCustomer);
                    EmailHelper.SendUpdatePaymentEmail(request.Email);
                }

                return Ok(new { success = true, message = "Payment details updated successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error: {ex.Message}");
                return StatusCode(500, "Failed to update payment details.");
            }
        }

        public class UpdatePaymentDetailsRequest
        {
            public string CustomerId { get; set; }
            public string Email { get; set; }
            public string NewPaymentMethodId { get; set; }
        }

        [HttpPost("cancel-subscription")]
        public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscriptionRequest request)
        {
            try
            {
                var stripeApiKey = "sk_test_51R2AHHEI7JiDsJpZ8KjwWbM6RlDVhMuLedVY2FgO8ExWDRFRVkomPJDpE3fjAfYCsjuIJBbMjpFzNvACPVXFF60700G8VtiiW4";
                StripeConfiguration.ApiKey = stripeApiKey;

                var subscriptionService = new SubscriptionService();
                var cancelOptions = new SubscriptionCancelOptions
                {
                    InvoiceNow = true,
                    Prorate = false,
                };

                var canceledSubscription = await subscriptionService.CancelAsync(request.SubscriptionId, cancelOptions);

                var invoiceService = new InvoiceService();
                var invoice = await invoiceService.GetAsync(canceledSubscription.LatestInvoiceId);

                if (invoice.PaymentIntentId != null)
                {
                    var refundService = new RefundService();
                    var refundOptions = new RefundCreateOptions
                    {
                        PaymentIntent = invoice.PaymentIntentId
                    };

                    await refundService.CreateAsync(refundOptions);
                }

                // 1. Update UserSubscriptions
                var subscription = await _dbContext.UserSubscriptions
                    .FirstOrDefaultAsync(s => s.SubscriptionKey == request.SubscriptionId && s.UserId == request.Email);

                if (subscription != null)
                {
                    subscription.Status = false;
                    subscription.CancellationReason = request.CancellationReason;
                    subscription.CancelledDate = DateTime.UtcNow;

                    _dbContext.Entry(subscription).State = EntityState.Modified;
                }

                //Delete user
                var user = await _userManager.FindByIdAsync(request.Email);
                EmailHelper.SendCancelMembershipEmail(user.Email, user.FirstName);
                if (user != null)
                {
                    await _userManager.DeleteAsync(user);
                }


                await _dbContext.SaveChangesAsync();

                return Ok(new { success = true, message = "Membership cancelled successfully." });
            }
            catch (StripeException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = "Internal server error." });
            }
        }
        public class CancelSubscriptionRequest
        {
            public string Email { get; set; }
            public string? SubscriptionId { get; set; }
            public string CancellationReason { get; set; }
        }

        public class CreatePaymentIntentRequest
        {
            public decimal Amount { get; set; }
            public string PaymentMethodId { get; set; }
        }
        public class ConfirmPaymentIntentRequest
        {
            public string PaymentIntentId { get; set; }
            public string PaymentMethodId { get; set; }
        }
    }
}
