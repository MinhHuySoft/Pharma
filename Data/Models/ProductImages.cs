using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class ProductImages
{
    public string ProductSlug { get; set; } = null!;

    public int SortOrder { get; set; }

    public string Path { get; set; } = null!;

    public string SourceUrl { get; set; } = null!;

    public virtual Products ProductSlugNavigation { get; set; } = null!;
}
