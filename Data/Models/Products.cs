using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class Products
{
    public string Slug { get; set; } = null!;

    public string CategorySlug { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Summary { get; set; } = null!;

    public int? PriceVnd { get; set; }

    public bool InStock { get; set; }

    public string? Packaging { get; set; }

    public string? Origin { get; set; }

    public string? Manufacturer { get; set; }

    public string? Distributor { get; set; }

    public string SourceUrl { get; set; } = null!;

    public int SortOrder { get; set; }

    public string BodyHtml { get; set; } = null!;

    public virtual Categories CategorySlugNavigation { get; set; } = null!;

    public virtual ICollection<ProductImages> ProductImages { get; set; } = new List<ProductImages>();

    public virtual ICollection<ProductGroups> GroupPath { get; set; } = new List<ProductGroups>();
}
