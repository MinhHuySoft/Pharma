using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class Articles
{
    public string Slug { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Summary { get; set; } = null!;

    public string Section { get; set; } = null!;

    public string ImagePath { get; set; } = null!;

    public string SourceUrl { get; set; } = null!;

    public DateOnly? PublishedOn { get; set; }

    public int SortOrder { get; set; }

    public string BodyHtml { get; set; } = null!;

    public string AuthorName { get; set; } = null!;
}
