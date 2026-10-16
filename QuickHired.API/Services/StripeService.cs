using Microsoft.Extensions.Options;
using QuickHired.API.Settings;
using Stripe;

namespace QuickHired.API.Services
{
    public class StripeService
    {
        private readonly StripeSettings _stripeSettings;

        public StripeService(IOptions<StripeSettings> stripeSettings)
        {
            _stripeSettings = stripeSettings.Value;
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
        }
        public async Task<PaymentIntent> CreatePaymentIntentAsync(decimal amount, string paymentMethodId, string currency = "usd")
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Stripe amounts are in cents
                Currency = currency,
                PaymentMethod = paymentMethodId,
                PaymentMethodTypes = new List<string> { "card" },
            };

            var service = new PaymentIntentService();
            return await service.CreateAsync(options);
        }

        public async Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId, string paymentMethodId)
        {
            var options = new PaymentIntentConfirmOptions
            {
                PaymentMethod = paymentMethodId
            };

            var service = new PaymentIntentService();
            return await service.ConfirmAsync(paymentIntentId, options);
        }
    }
}
