using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class Consultations
{
    public Guid Id { get; set; }

    public string CustomerName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string? ProductSlug { get; set; }

    public DateTime CreatedUtc { get; set; }
}
