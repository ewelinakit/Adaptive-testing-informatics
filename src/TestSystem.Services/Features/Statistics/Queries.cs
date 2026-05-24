using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Statistics;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;

namespace TestSystem.Services.Features.Statistics;

// Student Stats
public record GetStudentStatsQuery(Guid StudentId) : IRequest<StudentStatsDto>;

public class GetStudentStatsHandler : IRequestHandler<GetStudentStatsQuery, StudentStatsDto>
{
    private readonly DbContext _db;
    public GetStudentStatsHandler(DbContext db) => _db = db;

    public async Task<StudentStatsDto> Handle(GetStudentStatsQuery request, CancellationToken ct)
    {
        var student = await _db.Set<User>().FindAsync(new object[] { request.StudentId }, ct)
            ?? throw new NotFoundException("User", request.StudentId);

        var sessions = await _db.Set<TestSession>()
            .Include(s => s.TopicModule).ThenInclude(t => t.Subject)
            .Where(s => s.StudentId == request.StudentId && s.Status == TestSessionStatus.Completed)
            .ToListAsync(ct);

        var topicStats = sessions.Where(s => s.TopicModuleId.HasValue).GroupBy(s => s.TopicModuleId!.Value).Select(g => new TopicStatsDto(
            g.Key, g.First().TopicModule?.Title ?? "Комплексний тест", g.First().TopicModule?.Subject?.Name ?? "",
            g.Count(),
            Math.Round(g.Average(s => s.ScorePercentage), 1),
            g.Max(s => s.ScorePercentage)
        )).ToList();

        return new StudentStatsDto(
            student.Id, $"{student.FirstName} {student.LastName}",
            sessions.Count,
            sessions.Count > 0 ? Math.Round(sessions.Average(s => s.ScorePercentage), 1) : 0,
            sessions.Sum(s => s.CorrectAnswers),
            sessions.Sum(s => s.TotalQuestions),
            topicStats);
    }
}

// Group Stats (replaces Class Stats)
public record GetGroupStatsQuery(Guid GroupId) : IRequest<GroupStatsDto>;

public class GetGroupStatsHandler : IRequestHandler<GetGroupStatsQuery, GroupStatsDto>
{
    private readonly DbContext _db;
    public GetGroupStatsHandler(DbContext db) => _db = db;

    public async Task<GroupStatsDto> Handle(GetGroupStatsQuery request, CancellationToken ct)
    {
        var group = await _db.Set<Group>()
            .Include(g => g.Members).ThenInclude(m => m.Student)
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, ct)
            ?? throw new NotFoundException("Group", request.GroupId);

        var studentIds = group.Members.Select(m => m.StudentId).ToList();
        var sessions = await _db.Set<TestSession>()
            .Where(s => studentIds.Contains(s.StudentId) && s.Status == TestSessionStatus.Completed)
            .ToListAsync(ct);

        var studentSummaries = group.Members
            .Select(m =>
            {
                var studentSessions = sessions.Where(ss => ss.StudentId == m.StudentId).ToList();
                return new StudentSummaryDto(
                    m.StudentId, $"{m.Student.FirstName} {m.Student.LastName}",
                    studentSessions.Count,
                    studentSessions.Count > 0 ? Math.Round(studentSessions.Average(ss => ss.ScorePercentage), 1) : 0,
                    studentSessions.Count > 0 ? studentSessions.Max(ss => ss.ScorePercentage) : 0);
            }).OrderByDescending(s => s.AverageScore).ToList();

        return new GroupStatsDto(
            group.Id, group.Name, studentSummaries.Count,
            sessions.Count,
            sessions.Count > 0 ? Math.Round(sessions.Average(s => s.ScorePercentage), 1) : 0,
            studentSummaries);
    }
}

// Overview Stats (Admin)
public record GetOverviewStatsQuery() : IRequest<OverviewStatsDto>;

public class GetOverviewStatsHandler : IRequestHandler<GetOverviewStatsQuery, OverviewStatsDto>
{
    private readonly DbContext _db;
    public GetOverviewStatsHandler(DbContext db) => _db = db;

    public async Task<OverviewStatsDto> Handle(GetOverviewStatsQuery request, CancellationToken ct)
    {
        var totalStudents = await _db.Set<User>().CountAsync(u => u.Role == UserRole.Student && u.IsActive, ct);
        var totalTeachers = await _db.Set<User>().CountAsync(u => u.Role == UserRole.Teacher && u.IsActive, ct);
        var totalQuestions = await _db.Set<Question>().CountAsync(q => q.IsActive, ct);

        var completedSessions = await _db.Set<TestSession>()
            .Where(s => s.Status == TestSessionStatus.Completed)
            .ToListAsync(ct);

        var recentTests = await _db.Set<TestSession>()
            .Include(s => s.Student)
            .Include(s => s.TopicModule)
            .Where(s => s.Status == TestSessionStatus.Completed)
            .OrderByDescending(s => s.CompletedAt)
            .Take(10)
            .Select(s => new RecentTestDto(
                s.Id,
                s.Student.FirstName + " " + s.Student.LastName,
                s.TopicModule != null ? s.TopicModule.Title : "Комплексний тест",
                s.ScorePercentage,
                s.CompletedAt ?? s.StartedAt))
            .ToListAsync(ct);

        return new OverviewStatsDto(
            totalStudents, totalTeachers, completedSessions.Count, totalQuestions,
            completedSessions.Count > 0 ? Math.Round(completedSessions.Average(s => s.ScorePercentage), 1) : 0,
            recentTests);
    }
}
