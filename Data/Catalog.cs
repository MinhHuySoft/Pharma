using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pharma.Data.Models;

namespace Pharma.Data;

public sealed class Catalog(IDbContextFactory<PharmaContext> factory)
{
    public static string FormatPrice(int? price) => price is > 0
        ? FormatMoney(price.Value)
        : "Liên hệ";

    public static string FormatMoney(long amount) =>
        amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫";

    public async Task<List<Categories>> CategoriesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync();
    }

    public async Task<List<Products>> ProductsAsync(string? category = null, string? search = null, string? sort = null, string? group = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Products.AsNoTracking()
            .Include(p => p.CategorySlugNavigation)
            .Include(p => p.ProductImages.OrderBy(i => i.SortOrder).Take(1))
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.CategorySlug == category);
        if (group is not null)
            query = query.Where(p => p.GroupPath.Any(g => g.Path == group));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.CategorySlugNavigation.Name.Contains(term));
        }

        var ordered = sort switch
        {
            "price-up" => query.OrderBy(p => p.PriceVnd == null).ThenBy(p => p.PriceVnd),
            "price-down" => query.OrderByDescending(p => p.PriceVnd),
            _ => query.OrderBy(p => p.SortOrder)
        };
        return await ordered.ThenBy(p => p.Slug).ToListAsync();
    }

    public async Task<Products?> ProductAsync(string slug)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Products.AsNoTracking()
            .Include(p => p.CategorySlugNavigation)
            .Include(p => p.ProductImages.OrderBy(i => i.SortOrder))
            .Include(p => p.GroupPath)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task<List<Articles>> ArticlesAsync(int take = 8, string? exclude = null, string? topic = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Articles.AsNoTracking().Where(a => a.Section != "chinh-sach" && a.Slug != exclude);
        if (topic is not null)
            query = query.Where(a => a.Slug.Contains(topic));
        return await ArticleCards(query).Take(take).ToListAsync();
    }

    public async Task<(List<Articles> Items, int Pages, int Page)> ArchiveAsync(string? section, string? search, int page)
    {
        const int pageSize = 9;
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Articles.AsNoTracking().Where(a => section == null ? a.Section != "chinh-sach" : a.Section == section);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.Title.Contains(term) || a.Summary.Contains(term));
        }
        var pages = Math.Max(1, (await query.CountAsync() + pageSize - 1) / pageSize);
        var current = Math.Clamp(page, 1, pages);
        var items = await ArticleCards(query).Skip((current - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, pages, current);
    }

    private static IQueryable<Articles> ArticleCards(IQueryable<Articles> query) =>
        query.OrderByDescending(a => a.PublishedOn).ThenBy(a => a.SortOrder)
            .Select(a => new Articles
            {
                Slug = a.Slug, Title = a.Title, Summary = a.Summary, Section = a.Section,
                ImagePath = a.ImagePath, PublishedOn = a.PublishedOn, AuthorName = a.AuthorName
            });

    public async Task<Articles?> ArticleAsync(string slug)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Articles.AsNoTracking().SingleOrDefaultAsync(a => a.Slug == slug);
    }

    public async Task<Dictionary<string, SiteAssets>> AssetsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SiteAssets.AsNoTracking().OrderBy(a => a.AssetKey).ToDictionaryAsync(a => a.AssetKey);
    }

    public async Task<SitePages?> SitePageAsync(string slug)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SitePages.AsNoTracking().SingleOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task<List<ProductGroups>> ProductGroupsAsync(string prefix)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ProductGroups.AsNoTracking().Where(g => g.Path.StartsWith(prefix))
            .OrderBy(g => g.Name).ToListAsync();
    }

    public async Task<ProductGroups?> ProductGroupAsync(string path)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ProductGroups.AsNoTracking().SingleOrDefaultAsync(g => g.Path == path);
    }

    public static bool CanAdd(Products product) => product.InStock && product.PriceVnd is > 0;
}
