using System.Text.Json;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Rinha;
using Rinha.CompiledModels;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpLogging(o =>
{
    o.CombineLogs = true;
    o.LoggingFields = HttpLoggingFields.All;
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            var problemDetails = factory.CreateValidationProblemDetails(
                context.HttpContext,
                context.ModelState,
                422);
            return new UnprocessableEntityObjectResult(problemDetails);
        };
    });
builder.Services.AddDbContextPool<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("AppDbContext"));
    options.EnableThreadSafetyChecks(false);
    options.UseModel(AppDbContextModel.Instance);
    options.EnableSensitiveDataLogging();
});

var app = builder.Build();

app.UseHttpLogging();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new { Message = "It's up" }));

app.Run();
