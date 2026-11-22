using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

// Dnevni opomniki (voda, obroki, gibanje)
public static class ReminderEndpoints
{
    public static void MapReminderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reminders")
            .WithTags("Reminders");

        // Ustvarjanje opomnika
        group.MapPost("", (
            ReminderRequest request,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            ReminderService reminderService) =>
        {
            var user = GetUser(authorization, authService);
            if (user is null)
                return Results.Unauthorized();

            var error = Validate(request.Type, request.Time, request.Message);
            if (error is not null)
                return Results.BadRequest(new { message = error });

            var reminder = reminderService.Add(
                user.Id,
                request.Type!.Value,
                request.Time!.Value,
                request.Message?.Trim());

            return Results.Created($"/api/reminders", reminder);
        })
        .WithName("AddReminder")
        .WithSummary("Ustvarjanje dnevnega opomnika");

        // Seznam vseh opomnikov prijavljenega uporabnika
        group.MapGet("", (
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            ReminderService reminderService) =>
        {
            var user = GetUser(authorization, authService);
            if (user is null)
                return Results.Unauthorized();

            return Results.Ok(reminderService.GetAll(user.Id));
        })
        .WithName("GetReminders")
        .WithSummary("Pregled vseh opomnikov");

        // Urejanje opomnika
        group.MapPut("/{id:guid}", (
            Guid id,
            ReminderUpdateRequest request,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            ReminderService reminderService) =>
        {
            var user = GetUser(authorization, authService);
            if (user is null)
                return Results.Unauthorized();

            var error = Validate(request.Type, request.Time, request.Message);
            if (error is not null)
                return Results.BadRequest(new { message = error });

            var updated = reminderService.Update(
                user.Id,
                id,
                request.Type!.Value,
                request.Time!.Value,
                request.Message?.Trim(),
                request.IsActive);

            return updated is null
                ? Results.NotFound(new { message = "Opomnik ne obstaja." })
                : Results.Ok(updated);
        })
        .WithName("UpdateReminder")
        .WithSummary("Urejanje opomnika");

        // Brisanje opomnika
        group.MapDelete("/{id:guid}", (
            Guid id,
            [FromHeader(Name = "Authorization")] string? authorization,
            AuthService authService,
            ReminderService reminderService) =>
        {
            var user = GetUser(authorization, authService);
            if (user is null)
                return Results.Unauthorized();

            return reminderService.Delete(user.Id, id)
                ? Results.NoContent()
                : Results.NotFound(new { message = "Opomnik ne obstaja." });
        })
        .WithName("DeleteReminder")
        .WithSummary("Brisanje opomnika");
    }

    private static string? Validate(ReminderType? type, TimeOnly? time, string? message)
    {
        if (type is null || !Enum.IsDefined(type.Value))
            return "Vrsta opomnika mora biti Water, Meal ali Activity.";

        if (time is null)
            return "Manjka ura opomnika (npr. 09:00:00).";

        if (message is not null && message.Length > 200)
            return "Sporocilo je lahko dolgo najvec 200 znakov.";

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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReminderType
{
    Water,      // pitje vode
    Meal,       // vnos obrokov
    Activity    // gibanje
}

// Podatki, ki jih poslje uporabnik
public record ReminderRequest(
    ReminderType? Type,
    TimeOnly? Time,
    string? Message);

public record ReminderUpdateRequest(
    ReminderType? Type,
    TimeOnly? Time,
    string? Message,
    bool IsActive = true);

// Shranjeni opomnik
public record Reminder(
    Guid Id,
    Guid UserId,
    ReminderType Type,
    TimeOnly Time,
    string? Message,
    bool IsActive);

// Shranjevanje in upravljanje opomnikov
public sealed class ReminderService
{
    private readonly ConcurrentDictionary<Guid,
        ConcurrentDictionary<Guid, Reminder>> _reminders = new();

    public Reminder Add(
        Guid userId,
        ReminderType type,
        TimeOnly time,
        string? message)
    {
        var reminder = new Reminder(
            Guid.NewGuid(),
            userId,
            type,
            time,
            message,
            true);

        _reminders
            .GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Reminder>())
            [reminder.Id] = reminder;

        return reminder;
    }

    public Reminder[] GetAll(Guid userId)
    {
        if (!_reminders.TryGetValue(userId, out var items))
            return Array.Empty<Reminder>();

        return items.Values
            .OrderBy(r => r.Time)
            .ToArray();
    }

    public Reminder? Update(
        Guid userId,
        Guid id,
        ReminderType type,
        TimeOnly time,
        string? message,
        bool isActive)
    {
        if (!_reminders.TryGetValue(userId, out var items) ||
            !items.TryGetValue(id, out var existing))
        {
            return null;
        }

        var updated = existing with
        {
            Type = type,
            Time = time,
            Message = message,
            IsActive = isActive
        };

        items[id] = updated;
        return updated;
    }

    public bool Delete(Guid userId, Guid id)
    {
        return _reminders.TryGetValue(userId, out var items)
            && items.TryRemove(id, out _);
    }
}