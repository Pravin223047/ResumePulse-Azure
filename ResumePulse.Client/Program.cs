using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ResumePulse.Client;
using ResumePulse.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Use Azure API URL in production, local in development
var apiBaseUrl = builder.HostEnvironment.IsProduction()
    ? "YOUR_APP_SERVICE_URL"
    : "http://localhost:5266";

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

builder.Services.AddScoped<ResumeApiService>();

await builder.Build().RunAsync();
