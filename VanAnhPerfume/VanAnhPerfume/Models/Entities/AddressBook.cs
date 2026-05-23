using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class AddressBook
{
    public int AddressId { get; set; }

    public int UserId { get; set; }

    public string ReceiverName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string StreetAddress { get; set; } = null!;

    public string Ward { get; set; } = null!;

    public string District { get; set; } = null!;

    public string City { get; set; } = null!;

    public bool? IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
