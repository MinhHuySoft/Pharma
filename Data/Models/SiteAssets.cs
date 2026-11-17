using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class SiteAssets
{
    public string AssetKey { get; set; } = null!;

    public string Path { get; set; } = null!;

    public string AltText { get; set; } = null!;

    public string SourceUrl { get; set; } = null!;

    public string? LinkUrl { get; set; }
}
