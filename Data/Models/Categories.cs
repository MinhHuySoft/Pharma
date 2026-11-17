using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class Categories
{
    public string Slug { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string SourceUrl { get; set; } = null!;

    public int SortOrder { get; set; }

    public virtual ICollection<Products> Products { get; set; } = new List<Products>();
}
