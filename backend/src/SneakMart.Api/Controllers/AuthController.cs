using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SneakMart.Api.Data;
using SneakMart.Api.Dtos;
using SneakMart.Api.Infrastructure;

namespace SneakMart.Api.Controllers;

/// <summary>
/// Minimal login so Sprint 2 is runnable on its own. If your Sprint 1 repo already has an auth
/// controller (BCrypt + JWT), keep that one and delete this file. The JWT only needs "sub" and "role".
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController(AppDbContext db, JwtTokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email!.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new ApiException(401, "UNAUTHENTICATED", "Invalid email or password.");

        return Ok(new AuthResponse(tokens.Create(user), JwtTokenService.LifetimeSeconds, user.Role));
    }
}
