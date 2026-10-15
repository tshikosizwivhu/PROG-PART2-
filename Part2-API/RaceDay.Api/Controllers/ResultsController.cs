using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Organisers capture finish times and positions; Participants view their own results.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private IQueryable<Result> WithDetails() =>
        _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event);

    /// <summary>Captures the finish time and positions for an enrolment. Only the Organiser who owns the event may do this.</summary>
    /// <remarks>Body: { finishTime (hh:mm:ss), overallPosition, categoryPosition }. Leave finishTime empty for a DNF.</remarks>
    /// <response code="201">The captured result.</response>
    /// <response code="400">Validation error, or the enrolment was cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an Organiser who does not own the event.</response>
    /// <response code="404">Enrolment not found.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpPost("enrolments/{enrolmentId:int}/results")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> CaptureResult(int enrolmentId, [FromBody] ResultUpsertDto dto)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);

        if (enrolment == null) return NotFound(new ApiError("Enrolment not found."));
        if (enrolment.Category.Event.OrganiserId != CurrentUserId)
            return Forbidden("You can only capture results for your own events.");
        if (enrolment.Status != EnrolmentStatus.Confirmed)
            return BadRequest(new ApiError("Results can only be captured for confirmed enrolments."));
        if (await _db.Results.AnyAsync(r => r.EnrolmentId == enrolmentId))
            return Conflict(new ApiError("A result has already been captured for this enrolment."));

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = dto.FinishTime,
            OverallPosition = dto.OverallPosition,
            CategoryPosition = dto.CategoryPosition,
            CapturedBy = CurrentUserId!.Value
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var saved = await WithDetails().FirstAsync(r => r.ResultId == result.ResultId);
        return Created($"/api/results/{saved.ResultId}", ResultDto.From(saved));
    }

    /// <summary>Corrects a previously captured result. Only the Organiser who owns the event may do this.</summary>
    /// <remarks>Body: { finishTime (hh:mm:ss), overallPosition, categoryPosition }.</remarks>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an Organiser who does not own the event.</response>
    /// <response code="404">Result not found.</response>
    [HttpPut("results/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultDto>> UpdateResult(int id, [FromBody] ResultUpsertDto dto)
    {
        var result = await WithDetails().FirstOrDefaultAsync(r => r.ResultId == id);
        if (result == null) return NotFound(new ApiError("Result not found."));
        if (result.Enrolment.Category.Event.OrganiserId != CurrentUserId)
            return Forbidden("You can only change results for your own events.");

        result.FinishTime = dto.FinishTime;
        result.OverallPosition = dto.OverallPosition;
        result.CategoryPosition = dto.CategoryPosition;
        await _db.SaveChangesAsync();

        return Ok(ResultDto.From(result));
    }

    /// <summary>Returns the logged-in Participant's own results (personal performance history).</summary>
    /// <response code="200">Array of the participant's results, most recent event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("results/my")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultDto>>> GetMyResults()
    {
        var participantId = CurrentUserId!.Value;
        var results = await WithDetails()
            .Where(r => r.Enrolment.ParticipantId == participantId)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .ToListAsync();

        return Ok(results.Select(ResultDto.From));
    }

    /// <summary>Returns the results (leaderboard) of an event.</summary>
    /// <response code="200">Array of results ordered by overall position.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("events/{eventId:int}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultDto>>> GetEventResults(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new ApiError("Event not found."));

        var results = await WithDetails()
            .Where(r => r.Enrolment.Category.EventId == eventId)
            .ToListAsync();

        return Ok(results
            .OrderBy(r => r.OverallPosition ?? int.MaxValue)
            .Select(ResultDto.From));
    }
}
