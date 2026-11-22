using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<AuthService>();

var app = builder.Build();

app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/openapi/v1.json",
        "NutriLife API v1");
});

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

var authGroup = app.MapGroup("/api/auth")
    .WithTags("Auth");

// US01 - Registracija novega uporabnika
authGroup.MapPost("/register", (RegisterRequest request, AuthService authService) =>
{
    var validationError = ValidateRegistration(request);
    if (validationError is not null)
    {
        return Results.BadRequest(new { message = validationError });
    }

    var (created, error, user) = authService.Register(request.Username!, request.Email!, request.Password!);
    if (!created)
    {
        return Results.Conflict(new { message = error });
    }

    return Results.Created($"/api/auth/users/{user!.Id}", new
    {
        user.Id,
        user.Username,
        user.Email,
        user.CreatedAtUtc
    });
})
.WithName("RegisterUser")
.WithSummary("Registracija novega uporabnika (US01)");

// US02 - Prijava v aplikacijo
authGroup.MapPost("/login", (LoginRequest request, AuthService authService) =>
{
    if (string.IsNullOrWhiteSpace(request.EmailOrUsername) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Vnesi uporabniško ime ali e-pošto ter geslo." });
    }

    var result = authService.Login(request.EmailOrUsername, request.Password);
    if (result is null)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        message = "Prijava uspešna.",
        token = result.Value.Token,
        expiresAtUtc = result.Value.ExpiresAtUtc,
        user = new
        {
            result.Value.User.Id,
            result.Value.User.Username,
            result.Value.User.Email
        }
    });
})
.WithName("LoginUser")
.WithSummary("Prijava uporabnika (US02)");

// US02 - Odjava iz aplikacije
authGroup.MapPost("/logout", (HttpRequest httpRequest, AuthService authService) =>
{
    var token = ExtractBearerToken(httpRequest);
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.BadRequest(new { message = "Manjka Bearer žeton v glavi Authorization." });
    }

    var loggedOut = authService.Logout(token);
    if (!loggedOut)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new { message = "Odjava uspešna." });
})
.WithName("LogoutUser")
.WithSummary("Odjava uporabnika (US02)");

// Pomožni endpoint za preverjanje trenutne prijave
authGroup.MapGet("/me", (HttpRequest httpRequest, AuthService authService) =>
{
    var token = ExtractBearerToken(httpRequest);
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Unauthorized();
    }

    var user = authService.GetUserByToken(token);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        user.Id,
        user.Username,
        user.Email,
        user.CreatedAtUtc
    });
})
.WithName("GetCurrentUser")
.WithSummary("Podatki trenutno prijavljenega uporabnika");

app.Run();

static string? ValidateRegistration(RegisterRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 3)
    {
        return "Uporabniško ime mora imeti vsaj 3 znake.";
    }

    if (string.IsNullOrWhiteSpace(request.Email) || !new EmailAddressAttribute().IsValid(request.Email.Trim()))
    {
        return "Vnesi veljaven e-poštni naslov.";
    }

    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
    {
        return "Geslo mora imeti vsaj 6 znakov.";
    }

    return null;
}

static string? ExtractBearerToken(HttpRequest request)
{
    var authHeader = request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(authHeader))
    {
        return null;
    }

    const string prefix = "Bearer ";
    if (authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return authHeader[prefix.Length..].Trim();
    }

    return authHeader.Trim();
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record RegisterRequest(string? Username, string? Email, string? Password);

record LoginRequest(string? EmailOrUsername, string? Password);

record UserAccount(
    Guid Id,
    string Username,
    string Email,
    string PasswordHash,
    DateTime CreatedAtUtc);

sealed class AuthService
{
    private readonly ConcurrentDictionary<Guid, UserAccount> _usersById = new();
    private readonly ConcurrentDictionary<string, string> _activeTokens = new();
    private readonly PasswordHasher<UserAccount> _passwordHasher = new();

    public (bool Created, string? Error, UserAccount? User) Register(string username, string email, string password)
    {
        var normalizedUsername = username.Trim();
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var exists = _usersById.Values.Any(u =>
            u.Username.Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase) ||
            u.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            return (false, "Uporabnik s tem uporabniškim imenom ali e-poštnim naslovom že obstaja.", null);
        }

        var user = new UserAccount(
            Guid.NewGuid(),
            normalizedUsername,
            normalizedEmail,
            string.Empty,
            DateTime.UtcNow);

        var hashed = user with { PasswordHash = _passwordHasher.HashPassword(user, password) };
        _usersById[hashed.Id] = hashed;

        return (true, null, hashed);
    }

    public (string Token, DateTime ExpiresAtUtc, UserAccount User)? Login(string emailOrUsername, string password)
    {
        var query = emailOrUsername.Trim();
        var user = _usersById.Values.FirstOrDefault(u =>
            u.Email.Equals(query, StringComparison.OrdinalIgnoreCase) ||
            u.Username.Equals(query, StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            return null;
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + "-" + Guid.NewGuid().ToString("N");
        var expiresAtUtc = DateTime.UtcNow.AddHours(12);
        _activeTokens[token] = user.Id.ToString();

        return (token, expiresAtUtc, user);
    }

    public UserAccount? GetUserByToken(string token)
    {
        if (!_activeTokens.TryGetValue(token, out var userIdRaw) || !Guid.TryParse(userIdRaw, out var userId))
        {
            return null;
        }

        _usersById.TryGetValue(userId, out var user);
        return user;
    }

    public bool Logout(string token)
    {
        return _activeTokens.TryRemove(token, out _);
    }
}