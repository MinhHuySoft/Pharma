using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class StoreOrderItems
{
    public Guid OrderId { get; set; }

    public string ProductSlug { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public int Quantity { get; set; }

    public int UnitPriceVnd { get; set; }

    public virtual StoreOrders Order { get; set; } = null!;
}
