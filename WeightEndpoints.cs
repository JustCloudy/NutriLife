
using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// US29 - Spremljanje telesne teze
public static class WeightEndpoints
{
    public static void MapWeightEndpoints(this WebApplication app)
    {
        var weightGroup = app.MapGroup("/api/weight")
            .WithTags("Weight");

        // Dodajanje nove meritve teze
        weightGroup.MapPost("", (
            WeightEntryRequest request,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            WeightService weightService) =>
        {
            var user = GetUser(authorization, authService);

            if (user is null)
                return Results.Unauthorized();

            if (request.WeightKg <= 0)
                return Results.BadRequest(new
                {
                    message = "Telesna teza mora biti vecja od 0 kg."
                });

            if (request.Date == default)
                return Results.BadRequest(new
                {
                    message = "Datum meritve ni veljaven."
                });

            var entry = weightService.AddEntry(
                user.Id,
                request.Date,
                request.WeightKg);

            return Results.Created(
                $"/api/weight/{entry.Id}",
                new
                {
                    entry.Id,
                    entry.Date,
                    entry.WeightKg
                });
        })
        .WithName("AddWeightEntry")
        .WithSummary("Dodajanje meritve telesne teze (US29)");

        // Pregled zgodovine in spremembe teze
        weightGroup.MapGet("", (
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            WeightService weightService) =>
        {
            var user = GetUser(authorization, authService);

            if (user is null)
                return Results.Unauthorized();

            var entries = weightService.GetEntries(user.Id);

            decimal? lastWeight = entries.Length > 0
                ? entries[^1].WeightKg
                : null;

            decimal? change = entries.Length >= 2
                ? entries[^1].WeightKg - entries[0].WeightKg
                : null;

            return Results.Ok(new
            {
                lastWeightKg = lastWeight,
                changeKg = change,
                count = entries.Length,
                entries = entries.Select(e => new
                {
                    e.Id,
                    e.Date,
                    e.WeightKg
                }).ToArray()
            });
        })
        .WithName("GetWeightHistory")
        .WithSummary("Zgodovina in sprememba telesne teze (US29)");
    }

    // Preverjanje prijavljenega uporabnika
    private static UserAccount? GetUser(
        string? authorization,
        AuthService authService)
    {
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization["Bearer ".Length..].Trim();

        return authService.GetUserByToken(token);
    }
}

// Podatki, ki jih vnese uporabnik
public record WeightEntryRequest(
    DateOnly Date,
    decimal WeightKg);

// Posamezna meritev telesne teze
public record WeightEntry(
    Guid Id,
    Guid UserId,
    DateOnly Date,
    decimal WeightKg,
    DateTime CreatedAtUtc);

// Shranjevanje in pregled meritev
public sealed class WeightService
{
    private readonly ConcurrentDictionary<
        Guid, ConcurrentQueue<WeightEntry>> _entries = new();

    public WeightEntry AddEntry(
        Guid userId,
        DateOnly date,
        decimal weightKg)
    {
        var entry = new WeightEntry(
            Guid.NewGuid(),
            userId,
            date,
            weightKg,
            DateTime.UtcNow);

        _entries.GetOrAdd(
            userId,
            _ => new ConcurrentQueue<WeightEntry>())
            .Enqueue(entry);

        return entry;
    }

    public WeightEntry[] GetEntries(Guid userId)
    {
        if (!_entries.TryGetValue(userId, out var entries))
            return Array.Empty<WeightEntry>();

        return entries
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedAtUtc)
            .ToArray();
    }
}
