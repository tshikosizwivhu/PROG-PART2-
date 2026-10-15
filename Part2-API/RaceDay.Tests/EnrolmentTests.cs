using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Tests;

public class EnrolmentTests
{
    [Fact]
    public async Task Enrol_ValidCategory_RecordsLinkBetweenParticipantEventAndCategory()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var participant = await TestHelpers.AddParticipantAsync(db, "Sipho Ndlovu");
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var controller = new EnrolmentsController(db).WithSession(participant.ParticipantId, Roles.Participant);

        var result = await controller.Enrol(category.CategoryId);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var dto = Assert.IsType<EnrolmentDto>(created.Value);
        Assert.Equal(participant.ParticipantId, dto.ParticipantId);
        Assert.Equal(category.CategoryId, dto.CategoryId);
        Assert.Equal(ev.EventId, dto.EventId);
        Assert.Equal("Confirmed", dto.Status);

        var saved = await db.Enrolments.SingleAsync();
        Assert.Equal(participant.ParticipantId, saved.ParticipantId);
        Assert.Equal(category.CategoryId, saved.CategoryId);
    }

    [Fact]
    public async Task Enrol_SameCategoryTwice_ReturnsConflict()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var participant = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var controller = new EnrolmentsController(db).WithSession(participant.ParticipantId, Roles.Participant);
        await controller.Enrol(category.CategoryId);

        var result = await controller.Enrol(category.CategoryId);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(1, await db.Enrolments.CountAsync());
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_ReturnsConflict()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var first = await TestHelpers.AddParticipantAsync(db);
        var second = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId, maxParticipants: 1);
        await TestHelpers.AddEnrolmentAsync(db, first.ParticipantId, category.CategoryId);
        var controller = new EnrolmentsController(db).WithSession(second.ParticipantId, Roles.Participant);

        var result = await controller.Enrol(category.CategoryId);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(1, await db.Enrolments.CountAsync());
    }

    [Fact]
    public async Task Enrol_UnknownCategory_ReturnsNotFound()
    {
        using var db = TestHelpers.NewDb();
        var participant = await TestHelpers.AddParticipantAsync(db);
        var controller = new EnrolmentsController(db).WithSession(participant.ParticipantId, Roles.Participant);

        var result = await controller.Enrol(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Enrol_EventAlreadyTookPlace_ReturnsBadRequest()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var participant = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId, daysFromNow: -5);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var controller = new EnrolmentsController(db).WithSession(participant.ParticipantId, Roles.Participant);

        var result = await controller.Enrol(category.CategoryId);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Enrol_AfterCancelling_ReactivatesTheEnrolment()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var participant = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var enrolment = await TestHelpers.AddEnrolmentAsync(db, participant.ParticipantId, category.CategoryId);
        var controller = new EnrolmentsController(db).WithSession(participant.ParticipantId, Roles.Participant);
        await controller.CancelEnrolment(enrolment.EnrolmentId);

        var result = await controller.Enrol(category.CategoryId);

        Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(1, await db.Enrolments.CountAsync());
        Assert.Equal(EnrolmentStatus.Confirmed, (await db.Enrolments.SingleAsync()).Status);
    }

    [Fact]
    public async Task GetMyEnrolments_ReturnsOnlyTheLoggedInParticipantsEnrolments()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var me = await TestHelpers.AddParticipantAsync(db);
        var other = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        await TestHelpers.AddEnrolmentAsync(db, me.ParticipantId, category.CategoryId);
        await TestHelpers.AddEnrolmentAsync(db, other.ParticipantId, category.CategoryId);
        var controller = new EnrolmentsController(db).WithSession(me.ParticipantId, Roles.Participant);

        var result = await controller.GetMyEnrolments();

        var list = Assert.IsAssignableFrom<IEnumerable<EnrolmentDto>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();
        Assert.Single(list);
        Assert.Equal(me.ParticipantId, list[0].ParticipantId);
    }

    [Fact]
    public async Task GetEventEnrolments_ByOwningOrganiser_ReturnsAllEnrolmentsForEvent()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var p1 = await TestHelpers.AddParticipantAsync(db);
        var p2 = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        await TestHelpers.AddEnrolmentAsync(db, p1.ParticipantId, category.CategoryId);
        await TestHelpers.AddEnrolmentAsync(db, p2.ParticipantId, category.CategoryId);
        var controller = new EnrolmentsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.GetEventEnrolments(ev.EventId);

        var list = Assert.IsAssignableFrom<IEnumerable<EnrolmentDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public async Task GetEventEnrolments_ByOrganiserWhoDoesNotOwnEvent_ReturnsForbidden()
    {
        using var db = TestHelpers.NewDb();
        var owner = await TestHelpers.AddOrganiserAsync(db);
        var other = await TestHelpers.AddOrganiserAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, owner.OrganiserId);
        var controller = new EnrolmentsController(db).WithSession(other.OrganiserId, Roles.Organiser);

        var result = await controller.GetEventEnrolments(ev.EventId);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task CancelEnrolment_Own_SetsStatusToCancelled()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var participant = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var enrolment = await TestHelpers.AddEnrolmentAsync(db, participant.ParticipantId, category.CategoryId);
        var controller = new EnrolmentsController(db).WithSession(participant.ParticipantId, Roles.Participant);

        var result = await controller.CancelEnrolment(enrolment.EnrolmentId);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(EnrolmentStatus.Cancelled, (await db.Enrolments.SingleAsync()).Status);
    }

    [Fact]
    public async Task CancelEnrolment_OfAnotherParticipant_ReturnsForbidden()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var owner = await TestHelpers.AddParticipantAsync(db);
        var other = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var enrolment = await TestHelpers.AddEnrolmentAsync(db, owner.ParticipantId, category.CategoryId);
        var controller = new EnrolmentsController(db).WithSession(other.ParticipantId, Roles.Participant);

        var result = await controller.CancelEnrolment(enrolment.EnrolmentId);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(EnrolmentStatus.Confirmed, (await db.Enrolments.SingleAsync()).Status);
    }
}
