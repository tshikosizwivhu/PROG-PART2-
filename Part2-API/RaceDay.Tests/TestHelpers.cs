using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Tests;

/// <summary>Minimal in-memory ISession so controllers can be tested without a web server.</summary>
public sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public string Id { get; } = Guid.NewGuid().ToString();
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _store.TryGetValue(key, out value);
}

public static class TestHelpers
{
    /// <summary>A fresh, isolated in-memory database for each test.</summary>
    public static RaceDayDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RaceDayDbContext(options);
    }

    /// <summary>Gives a controller a fake session, optionally already "logged in" as the given user and role.</summary>
    public static T WithSession<T>(this T controller, int? userId = null, string? role = null) where T : ControllerBase
    {
        var session = new TestSession();
        if (userId.HasValue) session.SetInt32(SessionKeys.UserId, userId.Value);
        if (role != null) session.SetString(SessionKeys.Role, role);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Session = session }
        };
        return controller;
    }

    public static async Task<Organiser> AddOrganiserAsync(RaceDayDbContext db, string name = "Test Organiser", string? email = null)
    {
        var organiser = new Organiser
        {
            FullName = name,
            Email = email ?? $"{Guid.NewGuid():N}@test.com",
            PasswordHash = "hash"
        };
        db.Organisers.Add(organiser);
        await db.SaveChangesAsync();
        return organiser;
    }

    public static async Task<Participant> AddParticipantAsync(RaceDayDbContext db, string name = "Test Participant", string? email = null)
    {
        var participant = new Participant
        {
            FullName = name,
            Email = email ?? $"{Guid.NewGuid():N}@test.com",
            PasswordHash = "hash"
        };
        db.Participants.Add(participant);
        await db.SaveChangesAsync();
        return participant;
    }

    public static async Task<Event> AddEventAsync(RaceDayDbContext db, int organiserId, int daysFromNow = 30, string name = "Test Event")
    {
        var ev = new Event
        {
            OrganiserId = organiserId,
            EventName = name,
            EventDate = DateTime.Today.AddDays(daysFromNow),
            Location = "Pretoria",
            EventType = "Run",
            DistanceKm = 10m
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    public static async Task<Category> AddCategoryAsync(RaceDayDbContext db, int eventId, int maxParticipants = 100)
    {
        var category = new Category
        {
            EventId = eventId,
            CategoryName = "10km",
            DistanceKm = 10m,
            EntryFee = 100m,
            MaxParticipants = maxParticipants
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public static async Task<Enrolment> AddEnrolmentAsync(RaceDayDbContext db, int participantId, int categoryId)
    {
        var enrolment = new Enrolment { ParticipantId = participantId, CategoryId = categoryId };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();
        return enrolment;
    }
}
