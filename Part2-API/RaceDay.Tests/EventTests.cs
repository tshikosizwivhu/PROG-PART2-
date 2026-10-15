using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Tests;

public class EventTests
{
    private static EventUpsertDto ValidDto(string name = "Soweto 10km", string type = "Run") => new()
    {
        EventName = name,
        EventDate = DateTime.Today.AddDays(40),
        Location = "Soweto, Gauteng",
        Description = "Community run",
        EventType = type,
        DistanceKm = 10m
    };

    [Fact]
    public async Task CreateEvent_AsOrganiser_SavesEventOwnedByLoggedInOrganiser()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var controller = new EventsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.CreateEvent(ValidDto());

        var created = Assert.IsType<CreatedResult>(result.Result);
        var dto = Assert.IsType<EventDto>(created.Value);
        Assert.Equal(organiser.OrganiserId, dto.OrganiserId);
        Assert.Equal("Run", dto.EventType);
        Assert.Equal(10m, dto.DistanceKm);
        Assert.Equal(1, await db.Events.CountAsync());
    }

    [Fact]
    public async Task CreateEvent_InvalidEventType_ReturnsBadRequest()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var controller = new EventsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.CreateEvent(ValidDto(type: "Swim"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, await db.Events.CountAsync());
    }

    [Fact]
    public async Task CreateEvent_LowercaseType_IsNormalised()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var controller = new EventsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.CreateEvent(ValidDto(type: "cycle"));

        var dto = Assert.IsType<EventDto>(Assert.IsType<CreatedResult>(result.Result).Value);
        Assert.Equal("Cycle", dto.EventType);
    }

    [Fact]
    public async Task UpdateEvent_ByOwner_UpdatesFields()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var controller = new EventsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.UpdateEvent(ev.EventId, ValidDto("Renamed Event", "Walk"));

        var dto = Assert.IsType<EventDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Renamed Event", dto.EventName);
        Assert.Equal("Walk", dto.EventType);
    }

    [Fact]
    public async Task UpdateEvent_ByDifferentOrganiser_ReturnsForbidden()
    {
        using var db = TestHelpers.NewDb();
        var owner = await TestHelpers.AddOrganiserAsync(db);
        var other = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, owner.OrganiserId);
        var controller = new EventsController(db).WithSession(other.OrganiserId, Roles.Organiser);

        var result = await controller.UpdateEvent(ev.EventId, ValidDto("Hijacked"));

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal("Test Event", (await db.Events.FindAsync(ev.EventId))!.EventName);
    }

    [Fact]
    public async Task UpdateEvent_NonExistentEvent_ReturnsNotFound()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var controller = new EventsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.UpdateEvent(999, ValidDto());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteEvent_ByOwner_RemovesEventAndItsCategories()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var controller = new EventsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.DeleteEvent(ev.EventId);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await db.Events.CountAsync());
        Assert.Equal(0, await db.Categories.CountAsync());
    }

    [Fact]
    public async Task DeleteEvent_ByDifferentOrganiser_ReturnsForbidden()
    {
        using var db = TestHelpers.NewDb();
        var owner = await TestHelpers.AddOrganiserAsync(db);
        var other = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, owner.OrganiserId);
        var controller = new EventsController(db).WithSession(other.OrganiserId, Roles.Organiser);

        var result = await controller.DeleteEvent(ev.EventId);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(1, await db.Events.CountAsync());
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_ReturnsConflict()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var participant = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        await TestHelpers.AddEnrolmentAsync(db, participant.ParticipantId, category.CategoryId);
        var controller = new EventsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.DeleteEvent(ev.EventId);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(1, await db.Events.CountAsync());
    }

    [Fact]
    public async Task GetEvents_ByDefault_ReturnsOnlyUpcomingEvents()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        await TestHelpers.AddEventAsync(db, organiser.OrganiserId, daysFromNow: -10, name: "Past");
        await TestHelpers.AddEventAsync(db, organiser.OrganiserId, daysFromNow: 10, name: "Upcoming");
        var controller = new EventsController(db).WithSession();

        var result = await controller.GetEvents(null, null);

        var events = Assert.IsAssignableFrom<IEnumerable<EventDto>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();
        Assert.Single(events);
        Assert.Equal("Upcoming", events[0].EventName);
    }

    [Fact]
    public async Task GetEvents_IncludePast_ReturnsAllEvents()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        await TestHelpers.AddEventAsync(db, organiser.OrganiserId, daysFromNow: -10);
        await TestHelpers.AddEventAsync(db, organiser.OrganiserId, daysFromNow: 10);
        var controller = new EventsController(db).WithSession();

        var result = await controller.GetEvents(null, null, includePast: true);

        var events = Assert.IsAssignableFrom<IEnumerable<EventDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, events.Count());
    }

    [Fact]
    public async Task GetEvent_ReturnsDetailsWithCategories()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var controller = new EventsController(db).WithSession();

        var result = await controller.GetEvent(ev.EventId);

        var detail = Assert.IsType<EventDetailDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Single(detail.Categories);
    }

    [Fact]
    public async Task GetEvent_UnknownId_ReturnsNotFound()
    {
        using var db = TestHelpers.NewDb();
        var controller = new EventsController(db).WithSession();

        var result = await controller.GetEvent(123);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
