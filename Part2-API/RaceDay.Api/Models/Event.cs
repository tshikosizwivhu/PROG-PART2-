namespace RaceDay.Api.Models;

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EventType { get; set; } = string.Empty;   // Run / Walk / Cycle
    public decimal DistanceKm { get; set; }

    public Organiser Organiser { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public EventRoute? Route { get; set; }
}

public static class EventTypes
{
    public static readonly string[] All = { "Run", "Walk", "Cycle" };

    /// <summary>Returns the canonical spelling of an event type, or null when invalid.</summary>
    public static string? Normalise(string? value) =>
        All.FirstOrDefault(t => string.Equals(t, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
