using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace OpsHub.Models;

public sealed class ApplicationUser : IdentityUser
{
    [MaxLength(150)]
    public string DisplayName { get; set; } = string.Empty;
}
