namespace RaceDay.Api.Models;

/// <summary>Maps to the "Route" table (named EventRoute in code to avoid clashing with ASP.NET routing types).</summary>
public class EventRoute
{
    public int RouteId { get; set; }
    public int EventId { get; set; }
    public string? RouteDescription { get; set; }
    public int? ElevationGainM { get; set; }
    public string? MapUrl { get; set; }
    public string? WeatherNotes { get; set; }

    public Event Event { get; set; } = null!;
}
