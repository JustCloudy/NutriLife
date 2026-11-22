using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// Osebni nakupovalni seznam
public static class ShoppingEndpoints
{
    public static void MapShoppingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shopping")
            .WithTags("Shopping");

        // Dodajanje artikla na seznam
        group.MapPost("", (
            ShoppingItemRequest request,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            ShoppingService shoppingService) =>
        {
            var user = GetUser(authorization, authService);
            if (user is null)
                return Results.Unauthorized();

            var error = Validate(request);
            if (error is not null)
                return Results.BadRequest(new { message = error });

            var item = shoppingService.Add(
                user.Id,
                request.Name!.Trim(),
                request.Quantity,
                request.Unit?.Trim(),
                request.RecipeId);

            return Results.Created($"/api/shopping", item);
        })
        .WithName("AddShoppingItem")
        .WithSummary("Dodajanje artikla na nakupovalni seznam");

        // Pregled seznama
        group.MapGet("", (
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            ShoppingService shoppingService) =>
        {
            var user = GetUser(authorization, authService);
            if (user is null)
                return Results.Unauthorized();

            return Results.Ok(shoppingService.GetAll(user.Id));
        })
        .WithName("GetShoppingItems")
        .WithSummary("Pregled nakupovalnega seznama");

        // Urejanje artikla
        group.MapPut("/{id:guid}", (
            Guid id,
            ShoppingItemRequest request,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            ShoppingService shoppingService) =>
        {
            var user = GetUser(authorization, authService);
            if (user is null)
                return Results.Unauthorized();

            var error = Validate(request);
            if (error is not null)
                return Results.BadRequest(new { message = error });

            var updated = shoppingService.Update(
                user.Id,
                id,
                request.Name!.Trim(),
                request.Quantity,
                request.Unit?.Trim());

            return updated is null
                ? Results.NotFound(new { message = "Artikel ne obstaja." })
                : Results.Ok(updated);
        })
        .WithName("UpdateShoppingItem")
        .WithSummary("Urejanje artikla");

        // Oznacevanje kot kupljeno / nekupljeno
        group.MapPatch("/{id:guid}/purchased", (
            Guid id,
            bool isPurchased,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            ShoppingService shoppingService) =>
        {
            var user = GetUser(authorization, authService);
            if (user is null)
                return Results.Unauthorized();

            var updated = shoppingService.SetPurchased(user.Id, id, isPurchased);

            return updated is null
                ? Results.NotFound(new { message = "Artikel ne obstaja." })
                : Results.Ok(updated);
        })
        .WithName("SetShoppingItemPurchased")
        .WithSummary("Oznacitev artikla kot kupljenega");

        // Brisanje artikla
        group.MapDelete("/{id:guid}", (
            Guid id,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            ShoppingService shoppingService) =>
        {
            var user = GetUser(authorization, authService);
            if (user is null)
                return Results.Unauthorized();

            return shoppingService.Delete(user.Id, id)
                ? Results.NoContent()
                : Results.NotFound(new { message = "Artikel ne obstaja." });
        })
        .WithName("DeleteShoppingItem")
        .WithSummary("Brisanje artikla");
    }

    private static string? Validate(ShoppingItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Ime artikla je obvezno.";

        if (request.Name.Trim().Length > 100)
            return "Ime artikla je lahko dolgo najvec 100 znakov.";

        if (request.Quantity <= 0)
            return "Kolicina mora biti vecja od 0.";

        if (request.Unit is not null && request.Unit.Length > 20)
            return "Enota je lahko dolga najvec 20 znakov.";

        return null;
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
public record ShoppingItemRequest(
    string? Name,
    decimal Quantity,
    string? Unit,
    Guid? RecipeId);

// Shranjeni artikel
public record ShoppingItem(
    Guid Id,
    Guid UserId,
    string Name,
    decimal Quantity,
    string? Unit,
    bool IsPurchased,
    Guid? RecipeId);

// Shranjevanje in upravljanje nakupovalnega seznama
public sealed class ShoppingService
{
    private readonly ConcurrentDictionary<Guid,
        ConcurrentDictionary<Guid, ShoppingItem>> _items = new();

    public ShoppingItem Add(
        Guid userId,
        string name,
        decimal quantity,
        string? unit,
        Guid? recipeId)
    {
        var item = new ShoppingItem(
            Guid.NewGuid(),
            userId,
            name,
            quantity,
            unit,
            false,
            recipeId);

        _items
            .GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, ShoppingItem>())
            [item.Id] = item;

        return item;
    }

    // Nekupljeni artikli so na vrhu
    public ShoppingItem[] GetAll(Guid userId)
    {
        if (!_items.TryGetValue(userId, out var items))
            return Array.Empty<ShoppingItem>();

        return items.Values
            .OrderBy(i => i.IsPurchased)
            .ThenBy(i => i.Name)
            .ToArray();
    }

    public ShoppingItem? Update(
        Guid userId,
        Guid id,
        string name,
        decimal quantity,
        string? unit)
    {
        if (!_items.TryGetValue(userId, out var items) ||
            !items.TryGetValue(id, out var existing))
        {
            return null;
        }

        var updated = existing with
        {
            Name = name,
            Quantity = quantity,
            Unit = unit
        };

        items[id] = updated;
        return updated;
    }

    public ShoppingItem? SetPurchased(Guid userId, Guid id, bool isPurchased)
    {
        if (!_items.TryGetValue(userId, out var items) ||
            !items.TryGetValue(id, out var existing))
        {
            return null;
        }

        var updated = existing with { IsPurchased = isPurchased };
        items[id] = updated;
        return updated;
    }

    public bool Delete(Guid userId, Guid id)
    {
        return _items.TryGetValue(userId, out var items)
            && items.TryRemove(id, out _);
    }
}