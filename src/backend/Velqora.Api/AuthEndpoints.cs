using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Velqora.Core;
using Velqora.Infrastructure;
namespace Velqora.Api;
public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        app.MapGet("/api/auth/csrf", (HttpContext ctx, IAntiforgery anti) =>
        {
            var tokens = anti.GetAndStoreTokens(ctx);
            ctx.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new() { HttpOnly = false, SameSite = SameSiteMode.Strict, Secure = ctx.Request.IsHttps, Path = "/" });
            return Results.Ok(new { token = tokens.RequestToken });
        });
        app.MapPost("/api/auth/login", async (LoginInput input, SignInManager<SocUser> signIn, SocDb db, CancellationToken ct) =>
        {
            var email = Guard.Text(input.Email, "Email", 256);
            if (string.IsNullOrEmpty(input.Password) || input.Password.Length > 256) return Results.Unauthorized();
            var result = await signIn.PasswordSignInAsync(email, input.Password, false, true);
            db.Record("anonymous", result.Succeeded ? "auth.login" : "auth.failed", email);
            await db.SaveChangesAsync(ct);
            return result.Succeeded ? Results.Ok(new { message = "Signed in" }) : Results.Unauthorized();
        }).RequireRateLimiting("login");
        app.MapPost("/api/auth/logout", async (SignInManager<SocUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
        app.MapGet("/api/auth/me", async (HttpContext ctx, UserManager<SocUser> users) =>
        {
            var user = ApiHelpers.Required(await users.GetUserAsync(ctx.User));
            return Results.Ok(new { user.Id, user.Email, roles = await users.GetRolesAsync(user) });
        }).RequireAuthorization();
        app.MapGet("/api/users", async (UserManager<SocUser> users, CancellationToken ct) =>
        {
            var result = new List<object>();
            foreach (var user in await users.Users.OrderBy(x => x.Email).Take(200).ToListAsync(ct))
                result.Add(new { user.Id, user.Email, roles = await users.GetRolesAsync(user) });
            return Results.Ok(result);
        }).RequireAuthorization();
        app.MapPost("/api/users", async (UserInput input, UserManager<SocUser> users, HttpContext ctx, SocDb db, CancellationToken ct) =>
        {
            if (input.Role is not ("Admin" or "Analyst" or "ReadOnly")) throw new DomainException("Invalid role.");
            var email = Guard.Text(input.Email, "Email", 256);
            var user = new SocUser { UserName = email, Email = email };
            var result = await users.CreateAsync(user, input.Password);
            if (!result.Succeeded) throw new DomainException(string.Join("; ", result.Errors.Select(x => x.Description)));
            result = await users.AddToRoleAsync(user, input.Role);
            if (!result.Succeeded) { await users.DeleteAsync(user); throw new DomainException("Could not assign role."); }
            db.Record(ctx.User.Actor(), "user.created", user.Id, input.Role); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email, input.Role });
        }).RequireAuthorization("Admin");
    }
}
