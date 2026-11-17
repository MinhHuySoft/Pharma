using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class SitePages
{
    public string Slug { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string BodyHtml { get; set; } = null!;

    public string SourceUrl { get; set; } = null!;
}
