using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SpoolManager.Client;
using SpoolManager.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SpoolService>();
builder.Services.AddScoped<MaterialService>();
builder.Services.AddScoped<PrinterService>();
builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<DryerService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddScoped<AdminMaterialService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<NfcService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<BrandingService>();
builder.Services.AddScoped<SlicerProfileService>();
builder.Services.AddScoped<CsvImportService>();

await builder.Build().RunAsync();
