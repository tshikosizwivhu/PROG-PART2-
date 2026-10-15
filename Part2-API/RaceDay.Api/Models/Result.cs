namespace RaceDay.Api.Models;

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public int CapturedBy { get; set; }   // OrganiserId

    public Enrolment Enrolment { get; set; } = null!;
    public Organiser CapturedByOrganiser { get; set; } = null!;
}
