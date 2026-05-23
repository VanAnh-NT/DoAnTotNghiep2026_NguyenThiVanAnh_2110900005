using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class User
{
    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? Phone { get; set; }

    public int RoleId { get; set; }
    public DateTime? CreatedAt { get; set; }

    public bool? Status { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Avatar { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public bool IsEmailVerified { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AddressBook> AddressBooks { get; set; } = new List<AddressBook>();

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    public virtual Role Role { get; set; } = null!;
}
