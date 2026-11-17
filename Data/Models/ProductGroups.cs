using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class ProductGroups
{
    public string Path { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string SourceUrl { get; set; } = null!;

    public virtual ICollection<Products> ProductSlug { get; set; } = new List<Products>();
}
