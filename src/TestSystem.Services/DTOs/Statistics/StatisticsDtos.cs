namespace TestSystem.Services.DTOs.Statistics;

public record StudentStatsDto(
    Guid StudentId,
    string StudentName,
    int TotalTests,
    double AverageScore,
    int TotalCorrect,
    int TotalQuestions,
    List<TopicStatsDto> TopicStats);

public record TopicStatsDto(
    Guid TopicModuleId,
    string TopicTitle,
    string SubjectName,
    int TestsTaken,
    double AverageScore,
    double BestScore);

public record GroupStatsDto(
    Guid GroupId,
    string GroupName,
    int StudentCount,
    int TotalTests,
    double AverageScore,
    List<StudentSummaryDto> Students);

public record StudentSummaryDto(
    Guid Id,
    string FullName,
    int TestsTaken,
    double AverageScore,
    double BestScore);

public record OverviewStatsDto(
    int TotalStudents,
    int TotalTeachers,
    int TotalTests,
    int TotalQuestions,
    double AverageScore,
    List<RecentTestDto> RecentTests);

public record RecentTestDto(
    Guid Id,
    string StudentName,
    string TopicTitle,
    double ScorePercentage,
    DateTime CompletedAt);
