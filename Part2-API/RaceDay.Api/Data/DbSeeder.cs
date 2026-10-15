using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Data;

/// <summary>
/// Seeds demo data (development only) equivalent to the Part 1 SQL seed script,
/// but with real BCrypt password hashes so you can log in. Demo password for every account: Password123!
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password123!";

    public static async Task SeedAsync(RaceDayDbContext db)
    {
        if (await db.Organisers.AnyAsync() || await db.Participants.AnyAsync())
        {
            return;
        }

        var hash = PasswordService.Hash(DemoPassword);

        var org1 = new Organiser { FullName = "Thandiwe Mokoena", Email = "thandiwe.mokoena@raceday.co.za", PasswordHash = hash };
        var org2 = new Organiser { FullName = "Johan van der Merwe", Email = "johan.vdm@raceday.co.za", PasswordHash = hash };
        var par1 = new Participant { FullName = "Sipho Ndlovu", Email = "sipho.ndlovu@example.com", PasswordHash = hash };
        var par2 = new Participant { FullName = "Emma Peters", Email = "emma.peters@example.com", PasswordHash = hash };
        db.Organisers.AddRange(org1, org2);
        db.Participants.AddRange(par1, par2);
        await db.SaveChangesAsync();

        var today = DateTime.Today;
        var ev1 = new Event { OrganiserId = org1.OrganiserId, EventName = "Pretoria Park Run Challenge", EventDate = today.AddDays(-30), Location = "Pretoria, Gauteng", Description = "A community park run through the Pretoria botanical gardens.", EventType = "Run", DistanceKm = 10m };
        var ev2 = new Event { OrganiserId = org1.OrganiserId, EventName = "Tshwane Charity Walk", EventDate = today.AddDays(30), Location = "Centurion, Gauteng", Description = "A family-friendly charity walk raising funds for local schools.", EventType = "Walk", DistanceKm = 5m };
        var ev3 = new Event { OrganiserId = org2.OrganiserId, EventName = "Cape Winelands Cycle Tour", EventDate = today.AddDays(60), Location = "Stellenbosch, Western Cape", Description = "A scenic cycling tour through the Cape Winelands.", EventType = "Cycle", DistanceKm = 100m };
        db.Events.AddRange(ev1, ev2, ev3);
        await db.SaveChangesAsync();

        var c1 = new Category { EventId = ev1.EventId, CategoryName = "5km", DistanceKm = 5m, EntryFee = 80m, MaxParticipants = 300 };
        var c2 = new Category { EventId = ev1.EventId, CategoryName = "10km", DistanceKm = 10m, EntryFee = 120m, MaxParticipants = 300 };
        var c3 = new Category { EventId = ev2.EventId, CategoryName = "5km Walk", DistanceKm = 5m, EntryFee = 60m, MaxParticipants = 500 };
        var c4 = new Category { EventId = ev3.EventId, CategoryName = "50km", DistanceKm = 50m, EntryFee = 350m, MaxParticipants = 200 };
        var c5 = new Category { EventId = ev3.EventId, CategoryName = "100km", DistanceKm = 100m, EntryFee = 450m, MaxParticipants = 150 };
        db.Categories.AddRange(c1, c2, c3, c4, c5);

        db.Routes.AddRange(
            new EventRoute { EventId = ev1.EventId, RouteDescription = "Loop through the Pretoria National Botanical Garden, mostly flat with one short hill.", ElevationGainM = 45, MapUrl = "https://maps.example.com/pretoria-parkrun", WeatherNotes = "Check for summer thunderstorms in the afternoon." },
            new EventRoute { EventId = ev2.EventId, RouteDescription = "Flat route through Centurion suburbs, finishing at the Centurion Lake.", ElevationGainM = 20, MapUrl = "https://maps.example.com/tshwane-walk", WeatherNotes = "Mild mornings, bring sunscreen." },
            new EventRoute { EventId = ev3.EventId, RouteDescription = "Rolling hills through vineyards from Stellenbosch to Franschhoek and back.", ElevationGainM = 620, MapUrl = "https://maps.example.com/winelands-cycle", WeatherNotes = "Windy in the afternoon; start early." });
        await db.SaveChangesAsync();

        var en1 = new Enrolment { ParticipantId = par1.ParticipantId, CategoryId = c1.CategoryId };
        var en2 = new Enrolment { ParticipantId = par1.ParticipantId, CategoryId = c4.CategoryId };
        var en3 = new Enrolment { ParticipantId = par2.ParticipantId, CategoryId = c2.CategoryId };
        var en4 = new Enrolment { ParticipantId = par2.ParticipantId, CategoryId = c3.CategoryId };
        db.Enrolments.AddRange(en1, en2, en3, en4);
        await db.SaveChangesAsync();

        // Results exist only for the (past) Pretoria event.
        db.Results.AddRange(
            new Result { EnrolmentId = en1.EnrolmentId, FinishTime = new TimeSpan(0, 24, 15), OverallPosition = 12, CategoryPosition = 5, CapturedBy = org1.OrganiserId },
            new Result { EnrolmentId = en3.EnrolmentId, FinishTime = new TimeSpan(0, 52, 40), OverallPosition = 30, CategoryPosition = 10, CapturedBy = org1.OrganiserId });
        await db.SaveChangesAsync();
    }
}
