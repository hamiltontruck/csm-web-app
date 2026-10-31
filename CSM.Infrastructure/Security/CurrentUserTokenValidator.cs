using System.Security.Claims;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Security;

/// <summary>Signing a token does not freeze a user's roles/company for its lifetime.</summary>
public sealed class CurrentUserTokenValidator(ApplicationDbContext db)
{
    public async Task ValidateAsync(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id == Guid.Empty)
        {
            context.Fail("Your session is no longer valid.");
            return;
        }
        var actor = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsDeleted, context.HttpContext.RequestAborted);
        if (actor is null)
        {
            context.Fail("Your session is no longer valid.");
            return;
        }
        var replaced = new HashSet<string>(StringComparer.Ordinal) {
            ClaimTypes.Role, "role", "roles", "company_id", "companyId", "CompanyId", "employee_id", "employeeId", "EmployeeId", ClaimTypes.Name, ClaimTypes.Email
        };
        var source = context.Principal!.Identity as ClaimsIdentity;
        var identity = new ClaimsIdentity(context.Principal.Claims.Where(x => !replaced.Contains(x.Type)),
            source?.AuthenticationType ?? "Bearer", ClaimTypes.Name, ClaimTypes.Role);
        foreach (var role in actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).Select(x => x.Role.Name).Distinct())
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        if (actor.CompanyId.HasValue) identity.AddClaim(new Claim("company_id", actor.CompanyId.Value.ToString()));
        if (actor.EmployeeId.HasValue) identity.AddClaim(new Claim("employee_id", actor.EmployeeId.Value.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, $"{actor.FirstName} {actor.LastName}".Trim()));
        identity.AddClaim(new Claim(ClaimTypes.Email, actor.Email));
        context.Principal = new ClaimsPrincipal(identity);
    }
}
