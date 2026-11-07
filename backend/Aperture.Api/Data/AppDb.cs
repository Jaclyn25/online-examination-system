using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Data;
public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
}
public class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<AppUser>(options);
