using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Browse events (everyone) and manage events (Organisers, own events only).</summary>
[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists upcoming events, optionally filtered.</summary>
    /// <remarks>
    /// Public endpoint. Query parameters: <c>eventType</c> (Run, Walk or Cycle), <c>location</c> (text contained in the location),
    /// <c>includePast</c> (true to also return events that already took place).
    /// </remarks>
    /// <response code="200">Array of events ordered by date.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetEvents(
        [FromQuery] string? eventType, [FromQuery] string? location, [FromQuery] bool includePast = false)
    {
        var today = DateTime.Today;
        var query = _db.Events.AsQueryable();

        if (!includePast) query = query.Where(e => e.EventDate >= today);
        if (!string.IsNullOrWhiteSpace(eventType)) query = query.Where(e => e.EventType == eventType);
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(e => e.Location.Contains(location));

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(EventDto.From));
    }

    /// <summary>Returns one event with its categories and route information.</summary>
    /// <response code="200">The event, its categories and its route.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> GetEvent(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories)
            .Include(e => e.Route)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev == null) return NotFound(new ApiError("Event not found."));
        return Ok(EventDetailDto.FromDetail(ev));
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <remarks>Body: { eventName, eventDate, location, description, eventType (Run/Walk/Cycle), distanceKm }.</remarks>
    /// <response code="201">The created event.</response>
    /// <response code="400">Validation error (e.g. event type is not Run, Walk or Cycle).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpPost]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventDto>> CreateEvent([FromBody] EventUpsertDto dto)
    {
        var type = EventTypes.Normalise(dto.EventType);
        if (type == null) return BadRequest(new ApiError("EventType must be Run, Walk or Cycle."));

        var ev = new Event
        {
            OrganiserId = CurrentUserId!.Value,
            EventName = dto.EventName.Trim(),
            EventDate = dto.EventDate!.Value.Date,
            Location = dto.Location.Trim(),
            Description = dto.Description,
            EventType = type,
            DistanceKm = dto.DistanceKm
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{ev.EventId}", EventDto.From(ev));
    }

    /// <summary>Updates an event. Only the Organiser who owns the event may do this.</summary>
    /// <remarks>Body: { eventName, eventDate, location, description, eventType, distanceKm }.</remarks>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an Organiser who does not own this event.</response>
    /// <response code="404">Event not found.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> UpdateEvent(int id, [FromBody] EventUpsertDto dto)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new ApiError("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only change your own events.");

        var type = EventTypes.Normalise(dto.EventType);
        if (type == null) return BadRequest(new ApiError("EventType must be Run, Walk or Cycle."));

        ev.EventName = dto.EventName.Trim();
        ev.EventDate = dto.EventDate!.Value.Date;
        ev.Location = dto.Location.Trim();
        ev.Description = dto.Description;
        ev.EventType = type;
        ev.DistanceKm = dto.DistanceKm;
        await _db.SaveChangesAsync();

        return Ok(EventDto.From(ev));
    }

    /// <summary>Deletes an event (with its categories and route). Only the owning Organiser may do this.</summary>
    /// <remarks>An event that already has enrolments cannot be deleted.</remarks>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an Organiser who does not own this event.</response>
    /// <response code="404">Event not found.</response>
    /// <response code="409">The event has enrolments.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories)
            .Include(e => e.Route)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev == null) return NotFound(new ApiError("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only delete your own events.");

        var categoryIds = ev.Categories.Select(c => c.CategoryId).ToList();
        if (await _db.Enrolments.AnyAsync(en => categoryIds.Contains(en.CategoryId)))
        {
            return Conflict(new ApiError("This event has enrolments and cannot be deleted."));
        }

        if (ev.Route != null) _db.Routes.Remove(ev.Route);
        _db.Categories.RemoveRange(ev.Categories);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
