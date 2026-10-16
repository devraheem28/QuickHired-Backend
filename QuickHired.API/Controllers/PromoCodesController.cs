using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuickHired.BOL;
using QuickHired.DAL.Data;
using QuickHired.API.Helpers;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PromoCodesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUsers> _userManager;
        public PromoCodesController(AppDbContext context, UserManager<AppUsers> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        [HttpGet]
        public async Task<IActionResult> GetPromoCodes() =>
        Ok(await _context.PromoCodes.ToListAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPromoCode(int id)
        {
            var promo = await _context.PromoCodes.FindAsync(id);
            if (promo == null) return NotFound();
            return Ok(promo);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePromoCode([FromBody] PromoCodes promo)
        {
            _context.PromoCodes.Add(promo);
            await _context.SaveChangesAsync();
            return Ok(promo);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePromoCode(int id, [FromBody] PromoCodes promo)
        {
            if (id != promo.Id) return BadRequest();
            _context.Entry(promo).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePromoCode(int id)
        {
            var promo = await _context.PromoCodes.FindAsync(id);
            if (promo == null) return NotFound();
            _context.PromoCodes.Remove(promo);
            await _context.SaveChangesAsync();
            return Ok();
        }
        public class PromoCodeRequest
        {
            public string Code { get; set; }
            public string PlanName { get; set; }
        }

        [HttpPost("validate")]
        public IActionResult ValidatePromo([FromBody] PromoCodeRequest request)
        {
            var promo = _context.PromoCodes
                .FirstOrDefault(p => p.Code == request.Code && p.SubscriptionName == request.PlanName && p.ExpirationDate > DateTime.UtcNow);
            if (promo == null)
                return Ok(new { isValid = false });


            var subscription = _context.Subscriptions.FirstOrDefault(s => s.SubscriptionName == request.PlanName);
            if (subscription == null)
                return Ok(new { isValid = false });

            var originalPrice = subscription.Price;
            var discountPercent = promo.Percentage;
            var discountAmount = (originalPrice * discountPercent) / 100m;
            var finalPrice = originalPrice - discountAmount;

            return Ok(new
            {
                isValid = true,
                stripeButtonId = promo.StripeButtonId,
                publishableKey = promo.PublishableKey,
                discountPercent,
                originalPrice,
                discountAmount,
                finalPrice
            });
        }

    }
}
