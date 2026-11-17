using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class StoreOrders
{
    public Guid Id { get; set; }

    public Guid? AccountId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string? Note { get; set; }

    public long SubtotalVnd { get; set; }

    public DateTime CreatedUtc { get; set; }

    public virtual CustomerAccounts? Account { get; set; }

    public virtual ICollection<StoreOrderItems> StoreOrderItems { get; set; } = new List<StoreOrderItems>();
}
