
using Microsoft.AspNetCore.Http.Features;
using QuickHired.API;
using QuickHired.API.Extensions;
using QuickHired.API.Services;
using Serilog;
using System.Globalization;


SerilogConfig.ConfigureLogger();
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddApplicationServices(builder);
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddSwaggerDocumentation();
builder.Services.AddHttpContextAccessor();

// Register Analytics and Daily Digest Services
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddHostedService<DailyDigestService>();

// Register Job Integration Services
builder.Services.AddScoped<JoobleJobService>();
builder.Services.AddScoped<JobApiService>();
builder.Services.AddScoped<ResumeParserService>();

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = int.MaxValue;
});

//builder.WebHost.ConfigureKestrel(options =>
//{
//    options.Limits.MaxRequestBodySize = 1024L * 1024L * 1024L; // 1 GB
//});


//builder.Services.Configure<FormOptions>(options =>
//{
//    options.MultipartBodyLengthLimit = 1024L * 1024L * 1024L; // 1 GB
//});

// Configure session services
builder.Services.AddDistributedMemoryCache();


// Configure the HTTP request pipeline.

var app = builder.Build();

app.UseSwaggerDocumentation();
app.UseHttpsRedirection();
app.UseStaticFiles();


app.UseExceptionHandler("/Home/Error");


app.UseDeveloperExceptionPage();

app.UseRouting();
app.UseCors("MyWebCors");

app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        await context.Response.CompleteAsync();
    }
    await next();
});


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
