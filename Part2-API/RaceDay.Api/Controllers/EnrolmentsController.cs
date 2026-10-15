using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Participants enter events by choosing a category; Organisers view enrolments for their events.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private IQueryable<Enrolment> WithDetails() =>
        _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category)
            .ThenInclude(c => c.Event);

    /// <summary>Enrols the logged-in Participant in a category (which also links them to its event).</summary>
    /// <remarks>
    /// No body needed: the participant is taken from the session. A previously cancelled enrolment is re-activated.
    /// </remarks>
    /// <response code="201">The enrolment (status Confirmed), recording participant, category and event.</response>
    /// <response code="400">The event has already taken place.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    /// <response code="404">Category not found.</response>
    /// <response code="409">Already enrolled in this category, or the category is full.</response>
    [HttpPost("categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDto>> Enrol(int categoryId)
    {
        var participantId = CurrentUserId!.Value;

        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category == null) return NotFound(new ApiError("Category not found."));

        if (category.Event.EventDate.Date < DateTime.Today)
            return BadRequest(new ApiError("This event has already taken place."));

        var existing = await _db.Enrolments.FirstOrDefaultAsync(e => e.ParticipantId == participantId && e.CategoryId == categoryId);
        if (existing != null && existing.Status == EnrolmentStatus.Confirmed)
            return Conflict(new ApiError("You are already enrolled in this category."));

        var confirmed = await _db.Enrolments.CountAsync(e => e.CategoryId == categoryId && e.Status == EnrolmentStatus.Confirmed);
        if (confirmed >= category.MaxParticipants)
            return Conflict(new ApiError("This category is full."));

        Enrolment enrolment;
        if (existing != null)
        {
            existing.Status = EnrolmentStatus.Confirmed;
            existing.EnrolmentDate = DateTime.Now;
            enrolment = existing;
        }
        else
        {
            enrolment = new Enrolment { ParticipantId = participantId, CategoryId = categoryId };
            _db.Enrolments.Add(enrolment);
        }
        await _db.SaveChangesAsync();

        var saved = await WithDetails().FirstAsync(e => e.EnrolmentId == enrolment.EnrolmentId);
        return Created($"/api/enrolments/{saved.EnrolmentId}", EnrolmentDto.From(saved));
    }

    /// <summary>Lists the logged-in Participant's own enrolments.</summary>
    /// <response code="200">Array of the participant's enrolments, newest first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("enrolments/my")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> GetMyEnrolments()
    {
        var participantId = CurrentUserId!.Value;
        var enrolments = await WithDetails()
            .Where(e => e.ParticipantId == participantId)
            .OrderByDescending(e => e.EnrolmentDate)
            .ToListAsync();

        return Ok(enrolments.Select(EnrolmentDto.From));
    }

    /// <summary>Lists all enrolments for an event. Only the owning Organiser may do this.</summary>
    /// <response code="200">Array of enrolments for the event.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an Organiser who does not own the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> GetEventEnrolments(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound(new ApiError("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only view enrolments for your own events.");

        var enrolments = await WithDetails()
            .Where(e => e.Category.EventId == eventId)
            .OrderBy(e => e.Category.CategoryName)
            .ThenBy(e => e.Participant.FullName)
            .ToListAsync();

        return Ok(enrolments.Select(EnrolmentDto.From));
    }

    /// <summary>Cancels one of the logged-in Participant's own enrolments.</summary>
    /// <response code="204">Enrolment cancelled (status set to Cancelled).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Organiser, or the enrolment belongs to another participant.</response>
    /// <response code="404">Enrolment not found.</response>
    [HttpDelete("enrolments/{id:int}")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var enrolment = await _db.Enrolments.FindAsync(id);
        if (enrolment == null) return NotFound(new ApiError("Enrolment not found."));
        if (enrolment.ParticipantId != CurrentUserId) return Forbidden("You can only cancel your own enrolments.");

        enrolment.Status = EnrolmentStatus.Cancelled;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
