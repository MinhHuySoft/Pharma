using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class CustomerAccounts
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string NormalizedEmail { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public virtual ICollection<StoreOrders> StoreOrders { get; set; } = new List<StoreOrders>();
}
