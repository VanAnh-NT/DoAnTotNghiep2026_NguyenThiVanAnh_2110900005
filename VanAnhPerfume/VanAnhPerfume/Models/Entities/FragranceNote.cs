using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class FragranceNote
{
    public int NoteId { get; set; }

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
