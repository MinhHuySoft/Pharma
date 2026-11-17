using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharma.Data.Models;

namespace Pharma.Data;

public sealed class CustomerDetails
{
    [Required(ErrorMessage = "Nhập họ tên."), StringLength(120)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Nhập số điện thoại."), StringLength(25)]
    [RegularExpression(@"\+?[0-9][0-9 .()\-]{7,23}", ErrorMessage = "Số điện thoại chưa hợp lệ.")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Nhập địa chỉ."), StringLength(350)]
    public string Address { get; set; } = "";

    [StringLength(600)]
    public string? Note { get; set; }

    [StringLength(160)]
    public string? Product { get; set; }
}

public sealed class Storefront(IDbContextFactory<PharmaContext> factory)
{
    public static Guid? AccountId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public async Task RequestConsultationAsync(CustomerDetails customer)
    {
        await using var db = await factory.CreateDbContextAsync();
        var product = string.IsNullOrWhiteSpace(customer.Product) ? null : customer.Product;
        if (product is not null && !await db.Products.AnyAsync(p => p.Slug == product))
            throw new InvalidOperationException("Sản phẩm đã thay đổi. Vui lòng chọn lại.");
        db.Consultations.Add(new Consultations
        {
            Id = Guid.NewGuid(), CustomerName = customer.Name.Trim(), Phone = customer.Phone.Trim(),
            Address = customer.Address.Trim(), ProductSlug = product
        });
        await db.SaveChangesAsync();
    }

    public async Task SubscribeAsync(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > 254 || !new EmailAddressAttribute().IsValid(normalized))
            throw new InvalidOperationException("Địa chỉ email chưa hợp lệ.");
        await using var db = await factory.CreateDbContextAsync();
        if (await db.NewsletterSubscriptions.AnyAsync(s => s.Email == normalized))
            return;
        db.NewsletterSubscriptions.Add(new NewsletterSubscriptions { Email = normalized });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicate(ex))
        {
            // An identical submission completed while this request was in progress.
        }
    }

    public async Task<List<StoreOrders>> OrdersAsync(Guid accountId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.StoreOrders.AsNoTracking().Where(o => o.AccountId == accountId)
            .Include(o => o.StoreOrderItems).OrderByDescending(o => o.CreatedUtc).Take(25).ToListAsync();
    }

    public async Task<StoreOrders> CheckoutAsync(Guid requestId, Guid? accountId, CustomerDetails customer,
        IReadOnlyDictionary<string, int> quantities, IReadOnlyDictionary<string, int> displayedPrices)
    {
        if (quantities.Count == 0 || quantities.Any(line => line.Value is < 1 or > CartState.MaxQuantity))
            throw new InvalidOperationException("Giỏ hàng chưa sẵn sàng. Vui lòng kiểm tra lại số lượng.");

        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.StoreOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == requestId && o.AccountId == accountId);
        if (existing is not null)
            return existing;

        var keys = quantities.Keys.ToArray();
        var products = await db.Products.AsNoTracking().Where(p => keys.Contains(p.Slug)).ToListAsync();
        if (products.Count != keys.Length || products.Any(p =>
            !Catalog.CanAdd(p) || !displayedPrices.TryGetValue(p.Slug, out var price) || p.PriceVnd != price))
            throw new InvalidOperationException("Giá hoặc tình trạng hàng đã thay đổi. Vui lòng tải lại trang để xem giỏ hàng mới nhất.");

        var items = products.Select(p => new StoreOrderItems
        {
            ProductSlug = p.Slug, ProductName = p.Name, Quantity = quantities[p.Slug], UnitPriceVnd = p.PriceVnd!.Value
        }).ToList();
        var order = new StoreOrders
        {
            Id = requestId, AccountId = accountId, CustomerName = customer.Name.Trim(), Phone = customer.Phone.Trim(),
            Address = customer.Address.Trim(), Note = customer.Note?.Trim(), StoreOrderItems = items,
            SubtotalVnd = items.Sum(i => (long)i.UnitPriceVnd * i.Quantity)
        };
        db.StoreOrders.Add(order);
        try
        {
            await db.SaveChangesAsync();
            return order;
        }
        catch (DbUpdateException ex) when (IsDuplicate(ex))
        {
            return await db.StoreOrders.AsNoTracking().SingleAsync(o => o.Id == requestId && o.AccountId == accountId);
        }
    }

    internal static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}

public static class AccountEndpoints
{
    public static void MapLocalAccounts(this WebApplication app)
    {
        app.MapPost("/tai-khoan/dang-ky", RegisterAsync);
        app.MapPost("/tai-khoan/dang-nhap", LoginAsync);
        app.MapPost("/tai-khoan/dang-xuat", SignOutAsync);
    }

    private static async Task<IResult> RegisterAsync([FromForm] string? email, [FromForm] string? password,
        [FromForm] bool? remember, HttpContext http,
        IDbContextFactory<PharmaContext> factory, IPasswordHasher<CustomerAccounts> hasher)
    {
        email = email?.Trim();
        if (!IsEmail(email) || password is not { Length: >= 10 and <= 128 })
            return Results.Redirect("/tai-khoan?che-do=dang-ky&loi=du-lieu");

        await using var db = await factory.CreateDbContextAsync();
        var account = new CustomerAccounts
        {
            Id = Guid.NewGuid(), Email = email!, NormalizedEmail = email!.ToUpperInvariant()
        };
        account.PasswordHash = hasher.HashPassword(account, password);
        db.CustomerAccounts.Add(account);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (Storefront.IsDuplicate(ex))
        {
            return Results.Redirect("/tai-khoan?che-do=dang-ky&loi=da-co");
        }
        return await SignInAsync(http, account, remember == true);
    }

    private static async Task<IResult> LoginAsync([FromForm] string? email, [FromForm] string? password,
        [FromForm] bool? remember, HttpContext http,
        IDbContextFactory<PharmaContext> factory, IPasswordHasher<CustomerAccounts> hasher)
    {
        email = email?.Trim();
        if (!IsEmail(email) || password is not { Length: > 0 and <= 128 })
            return Results.Redirect("/tai-khoan?loi=dang-nhap");
        await using var db = await factory.CreateDbContextAsync();
        var normalized = email!.ToUpperInvariant();
        var account = await db.CustomerAccounts.SingleOrDefaultAsync(a => a.NormalizedEmail == normalized);
        if (account is null)
            return Results.Redirect("/tai-khoan?loi=dang-nhap");
        var verification = hasher.VerifyHashedPassword(account, account.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
            return Results.Redirect("/tai-khoan?loi=dang-nhap");
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = hasher.HashPassword(account, password);
            await db.SaveChangesAsync();
        }
        return await SignInAsync(http, account, remember == true);
    }

    private static async Task<IResult> SignOutAsync(HttpContext http, [FromForm(Name = "confirm")] string confirm)
    {
        if (confirm != "yes")
            return Results.BadRequest();
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/tai-khoan");
    }

    private static bool IsEmail(string? email) =>
        email is { Length: > 0 and <= 254 } && new EmailAddressAttribute().IsValid(email);

    private static async Task<IResult> SignInAsync(HttpContext http, CustomerAccounts account, bool persistent)
    {
        var identity = new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, account.Id.ToString()), new(ClaimTypes.Name, account.Email)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = persistent });
        return Results.Redirect("/tai-khoan");
    }
}
