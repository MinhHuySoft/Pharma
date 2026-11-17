using System;
using System.Collections.Generic;

namespace Pharma.Data.Models;

public partial class NewsletterSubscriptions
{
    public string Email { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
}
