using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using SMS.Web;
using SMS.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMudServices();

// The sms api is a separate origin, so it gets its own client keyed off wwwroot/appsettings.json.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;

if (!apiBaseAddress.EndsWith('/'))
{
    apiBaseAddress += "/";
}

builder.Services.AddScoped(sp => new SmsApiClient(new HttpClient { BaseAddress = new Uri(apiBaseAddress) }));

await builder.Build().RunAsync();
