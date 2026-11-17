using Pharma.Components;
using Pharma.Data;
using Pharma.Data.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddDbContextFactory<PharmaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Pharma")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Pharma.")));
builder.Services.AddScoped<Catalog>();
builder.Services.AddScoped<CartState>();
builder.Services.AddScoped<Storefront>();
builder.Services.AddScoped<IPasswordHasher<CustomerAccounts>, PasswordHasher<CustomerAccounts>>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Pharma.Local";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/tai-khoan";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapLocalAccounts();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
