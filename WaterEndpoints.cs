
using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// US38 - Spremljanje dnevnega vnosa vode
public static class WaterEndpoints
{
    public static void MapWaterEndpoints(this WebApplication app)
    {
        var waterGroup = app.MapGroup("/api/water")
            .WithTags("Water");

        // Dodajanje novega vnosa vode
        waterGroup.MapPost("", (
            WaterEntryRequest request,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            WaterService waterService) =>
        {
            var user = GetUser(authorization, authService);

            if (user is null)
                return Results.Unauthorized();

            if (request.AmountMl <= 0)
                return Results.BadRequest(new
                {
                    message = "Kolicina vode mora biti vecja od 0 ml."
                });

            var entry = waterService.AddEntry(
                user.Id,
                request.Date,
                request.AmountMl);

            return Results.Created(
                $"/api/water/{entry.Date:yyyy-MM-dd}",
                new
                {
                    entry.Id,
                    entry.Date,
                    entry.AmountMl
                });
        })
        .WithName("AddWaterEntry")
        .WithSummary("Dodajanje vnosa vode (US38)");

        // Pregled vode za izbrani datum
        waterGroup.MapGet("/{date}", (
            DateOnly date,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            WaterService waterService) =>
        {
            var user = GetUser(authorization, authService);

            if (user is null)
                return Results.Unauthorized();

            var entries = waterService.GetEntries(user.Id, date);

            return Results.Ok(new
            {
                date,
                totalMl = entries.Sum(e => e.AmountMl),
                entries = entries.Select(e => new
                {
                    e.Id,
                    e.AmountMl,
                    e.Date
                }).ToArray()
            });
        })
        .WithName("GetDailyWater")
        .WithSummary("Dnevni vnos vode in zgodovina (US38)");
    }

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

// Podatki, ki jih poslje uporabnik
public record WaterEntryRequest(
    DateOnly Date,
    int AmountMl);

// Posamezni shranjeni vnos
public record WaterEntry(
    Guid Id,
    Guid UserId,
    DateOnly Date,
    int AmountMl);

// Shranjevanje in pregled vnosov
public sealed class WaterService
{
    private readonly ConcurrentDictionary<Guid,
        ConcurrentQueue<WaterEntry>> _entries = new();

    public WaterEntry AddEntry(
        Guid userId,
        DateOnly date,
        int amountMl)
    {
        var entry = new WaterEntry(
            Guid.NewGuid(),
            userId,
            date,
            amountMl);

        _entries.GetOrAdd(
            userId,
            _ => new ConcurrentQueue<WaterEntry>())
            .Enqueue(entry);

        return entry;
    }

    public WaterEntry[] GetEntries(Guid userId, DateOnly date)
    {
        if (!_entries.TryGetValue(userId, out var entries))
            return Array.Empty<WaterEntry>();

        return entries
            .Where(e => e.Date == date)
            .ToArray();
    }
}
