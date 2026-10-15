using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Tests;

public class ResultTests
{
    private static ResultUpsertDto ValidResult() => new()
    {
        FinishTime = new TimeSpan(0, 45, 30),
        OverallPosition = 7,
        CategoryPosition = 2
    };

    [Fact]
    public async Task CaptureResult_ByOwningOrganiser_SavesFinishTimeAndPositions()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var participant = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId, daysFromNow: -1);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var enrolment = await TestHelpers.AddEnrolmentAsync(db, participant.ParticipantId, category.CategoryId);
        var controller = new ResultsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.CaptureResult(enrolment.EnrolmentId, ValidResult());

        var dto = Assert.IsType<ResultDto>(Assert.IsType<CreatedResult>(result.Result).Value);
        Assert.Equal(new TimeSpan(0, 45, 30), dto.FinishTime);
        Assert.Equal(7, dto.OverallPosition);
        Assert.Equal(organiser.OrganiserId, (await db.Results.SingleAsync()).CapturedBy);
    }

    [Fact]
    public async Task CaptureResult_ByOrganiserWhoDoesNotOwnEvent_ReturnsForbidden()
    {
        using var db = TestHelpers.NewDb();
        var owner = await TestHelpers.AddOrganiserAsync(db);
        var other = await TestHelpers.AddOrganiserAsync(db);
        var participant = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, owner.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var enrolment = await TestHelpers.AddEnrolmentAsync(db, participant.ParticipantId, category.CategoryId);
        var controller = new ResultsController(db).WithSession(other.OrganiserId, Roles.Organiser);

        var result = await controller.CaptureResult(enrolment.EnrolmentId, ValidResult());

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(0, await db.Results.CountAsync());
    }

    [Fact]
    public async Task CaptureResult_Twice_ReturnsConflict()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var participant = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var enrolment = await TestHelpers.AddEnrolmentAsync(db, participant.ParticipantId, category.CategoryId);
        var controller = new ResultsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);
        await controller.CaptureResult(enrolment.EnrolmentId, ValidResult());

        var result = await controller.CaptureResult(enrolment.EnrolmentId, ValidResult());

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(1, await db.Results.CountAsync());
    }

    [Fact]
    public async Task CaptureResult_UnknownEnrolment_ReturnsNotFound()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var controller = new ResultsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);

        var result = await controller.CaptureResult(404, ValidResult());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMyResults_ReturnsOnlyTheLoggedInParticipantsResults()
    {
        using var db = TestHelpers.NewDb();
        var organiser = await TestHelpers.AddOrganiserAsync(db);
        var me = await TestHelpers.AddParticipantAsync(db);
        var other = await TestHelpers.AddParticipantAsync(db);
        var ev = await TestHelpers.AddEventAsync(db, organiser.OrganiserId, daysFromNow: -1);
        var category = await TestHelpers.AddCategoryAsync(db, ev.EventId);
        var myEnrolment = await TestHelpers.AddEnrolmentAsync(db, me.ParticipantId, category.CategoryId);
        var otherEnrolment = await TestHelpers.AddEnrolmentAsync(db, other.ParticipantId, category.CategoryId);

        var organiserController = new ResultsController(db).WithSession(organiser.OrganiserId, Roles.Organiser);
        await organiserController.CaptureResult(myEnrolment.EnrolmentId, ValidResult());
        await organiserController.CaptureResult(otherEnrolment.EnrolmentId, ValidResult());

        var participantController = new ResultsController(db).WithSession(me.ParticipantId, Roles.Participant);
        var result = await participantController.GetMyResults();

        var list = Assert.IsAssignableFrom<IEnumerable<ResultDto>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();
        Assert.Single(list);
        Assert.Equal(me.ParticipantId, list[0].ParticipantId);
    }
}
