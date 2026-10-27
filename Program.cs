using OpsHub.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using OpsHub.Models;
using OpsHub.Data;
using OpsHub.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);


var dbPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "opshub.db");

builder.Services.AddDbContextFactory<OpsHubDbContext>(
    options => options.UseSqlite($"Data Source={dbPath}"));

builder.Services
    .AddOptions<SlaOptions>()
    .Bind(builder.Configuration.GetSection("Sla"))
    .Validate(options =>
            new[] { "Low", "Medium", "High", "Critical" }.All(priority =>
                options.ResolutionTargetHours.TryGetValue(priority, out var hours) &&
                double.IsFinite(hours) && hours > 0 &&
                hours <= TimeSpan.MaxValue.TotalHours) &&
            double.IsFinite(options.AtRiskWindowHours) &&
            options.AtRiskWindowHours > 0 &&
            options.AtRiskWindowHours <= TimeSpan.MaxValue.TotalHours,
        "Configure positive finite SLA targets for Low, Medium, High, and Critical and a positive at-risk window.")
    .ValidateOnStart();
builder.Services.AddSingleton(serviceProvider =>
    new SlaCalculator(
        serviceProvider.GetRequiredService<IOptions<SlaOptions>>().Value));
builder.Services.AddSingleton<TicketCsvExporter>();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<OpsHubDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<IdentityBootstrapper>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider
        .GetRequiredService<IdentityBootstrapper>()
        .InitializeAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
