using Microsoft.AspNetCore.Mvc;
using QuickHired.API.Interfaces.Services;
using QuickHired.API.Models;
using QuickHired.API.Services;
using QuickHired.API.Settings;
using QuickHired.DAL.Services;
using Serilog;
using System.Net;

namespace QuickHired.API.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            WebApplicationBuilder builder)
        {

            builder.Services.AddControllersWithViews();

            //Serilog
            var logger = new LoggerConfiguration()
                            .ReadFrom.Configuration(builder.Configuration)
                            .Enrich.FromLogContext()
                            .CreateLogger();
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(logger);

            //Add services to the container.
            builder.Services.AddScoped<StripeService>();
            //builder.Services.AddScoped<PaypalService>();
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
            builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection("Paypal"));
            //Scrapping Service
            builder.Services.AddHttpClient<ScrapingService>();
            // Register MappingProfiles with AutoMapper
            builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            builder.Services.AddHttpClient();

            //Service Cors
            var allowedOrigins = builder.Configuration.GetSection("MyWeb:Origins").Get<string[]>();
            builder.Services.AddCors(p => p.AddPolicy("MyWebCors", builder =>
            {
                builder.WithOrigins(
                    "https://quickhired.com",
                    "https://quickhired.com/",
                    "http://localhost:4200",
                    "http://localhost:4200/"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
            }));
            //builder.Services.AddCors(p => p.AddPolicy("MyWebCors", builder =>
            //{
            //    builder
            //        .SetIsOriginAllowed(origin => true)
            //        .AllowAnyMethod()
            //        .AllowAnyHeader()
            //        .AllowCredentials();
            //}));
            //Model Validations
            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {
                    var errors = actionContext.ModelState
                      .Where(x => x.Value.Errors.Count > 0)
                      .SelectMany(x => x.Value.Errors)
                      .Select(x => x.ErrorMessage).ToArray();

                    var errorResponse = new ApiValidationResponse
                    {
                        IsError = true,
                        Status = HttpStatusCode.BadRequest,
                        Model = errors.ToList()
                    };

                    return new BadRequestObjectResult(errorResponse);
                };

            });

            return services;
        }
    }
}
