using Microsoft.JSInterop;
using Pharma.Data.Models;

namespace Pharma.Data;

public sealed class CartState(IJSRuntime browser)
{
    private const string StorageKey = "pharma-cart-v1";
    public const int MaxQuantity = 99;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Dictionary<string, int> items = new(StringComparer.Ordinal);

    public bool Ready { get; private set; }
    public int Count => items.Values.Sum();
    public IReadOnlyDictionary<string, int> Items => items;
    public string? Message { get; private set; }
    public event Action? Changed;

    public async Task LoadAsync()
    {
        if (Ready)
            return;

        await gate.WaitAsync();
        try
        {
            if (Ready)
                return;

            try
            {
                var saved = await browser.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (!string.IsNullOrEmpty(saved))
                {
                    var basket = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(saved);
                    if (basket is not null)
                        items = basket.Where(x => x.Key.Length <= 160 && x.Value is > 0 and <= MaxQuantity)
                            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                items.Clear();
            }
            catch (JSException)
            {
                Message = "Trình duyệt đang chặn lưu giỏ hàng.";
            }

            Ready = true;
        }
        finally
        {
            gate.Release();
        }

        Changed?.Invoke();
    }

    public Task AddAsync(Products product, int quantity = 1) => Catalog.CanAdd(product)
        ? UpdateAsync(product.Slug, current => current + Math.Clamp(quantity, 1, MaxQuantity), "Đã cập nhật giỏ hàng.")
        : Task.CompletedTask;

    public Task AdjustAsync(string slug, int difference) =>
        UpdateAsync(slug, current => current + difference);

    public Task RemoveAsync(string slug) => UpdateAsync(slug, _ => 0);

    private async Task UpdateAsync(string slug, Func<int, int> change, string? message = null)
    {
        await LoadAsync();
        await gate.WaitAsync();
        try
        {
            var quantity = Math.Clamp(change(items.GetValueOrDefault(slug)), 0, MaxQuantity);
            if (quantity == 0)
                items.Remove(slug);
            else
                items[slug] = quantity;
            Message = message;
            await PersistAsync();
        }
        finally
        {
            gate.Release();
        }

        Changed?.Invoke();
    }

    public async Task RemovePlacedItemsAsync(IReadOnlyDictionary<string, int> quantities)
    {
        await gate.WaitAsync();
        try
        {
            foreach (var (slug, quantity) in quantities)
            {
                var remaining = items.GetValueOrDefault(slug) - quantity;
                if (remaining > 0)
                    items[slug] = remaining;
                else
                    items.Remove(slug);
            }
            Message = null;
            await PersistAsync();
        }
        finally
        {
            gate.Release();
        }
        Changed?.Invoke();
    }

    private async Task PersistAsync()
    {
        try
        {
            var saved = System.Text.Json.JsonSerializer.Serialize(items);
            await browser.InvokeVoidAsync("localStorage.setItem", StorageKey, saved);
        }
        catch (JSException)
        {
            Message = "Giỏ hàng chỉ được giữ trong phiên hiện tại.";
        }
    }
}
