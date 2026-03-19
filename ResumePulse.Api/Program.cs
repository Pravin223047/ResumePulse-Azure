using Azure.Identity;
using ResumePulse.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Key Vault (SAFE VERSION - no crash)
var keyVaultUrl = builder.Configuration["KeyVault:Url"];

if (!string.IsNullOrEmpty(keyVaultUrl))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUrl),
            new DefaultAzureCredential());

        Console.WriteLine("✅ Key Vault loaded successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Key Vault failed: {ex.Message}");
    }
}

// 🔹 Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🔹 CORS (STRICT + CORRECT)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "YOUR_STATIC_WEB_APP_URL"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
            // ⚠️ Remove AllowCredentials unless needed
    });
});

// 🔹 Dependency Injection
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IServiceBusService, ServiceBusService>();

var app = builder.Build();

// 🔹 Middleware pipeline (ORDER IS CRITICAL)

app.UseHttpsRedirection();

// ✅ Enable Swagger (even in Azure for debugging)
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();