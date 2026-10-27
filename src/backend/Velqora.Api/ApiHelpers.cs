using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Velqora.Core;
using Velqora.Infrastructure;
namespace Velqora.Api;

public static class ApiHelpers
{
    public static string Actor(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
    public static T Required<T>(T? value) where T : class => value ?? throw new DomainException("Resource not found.", 404);
    public static void Version(VersionedEntity entity, int expected)
    {
        if (entity.Version != expected) throw new DomainException("The resource changed. Reload and retry.", 409);
    }
    public static async Task Owner(UserManager<SocUser> users, string? id)
    {
        if (id == null) return;
        var user = await users.FindByIdAsync(id);
        if (user == null || !(await users.GetRolesAsync(user)).Any(r => r is "Analyst" or "Admin"))
            throw new DomainException("Owner must be an existing analyst or admin.");
    }
    public static async Task<Page<T>> PageAsync<T>(IQueryable<T> query, int? page, int? size, CancellationToken ct)
    {
        var p = Math.Clamp(page ?? 1, 1, 1000000); var s = Math.Clamp(size ?? 25, 1, 100);
        return new(await query.Skip((p - 1) * s).Take(s).ToListAsync(ct), await query.CountAsync(ct), p, s);
    }
}
