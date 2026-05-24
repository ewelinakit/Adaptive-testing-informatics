using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Tests;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;

namespace TestSystem.Services.Features.Tests;

// Create Test (Teacher)
public record CreateTestCommand(
    string Title, string? Description, Guid GroupId, Guid? TopicModuleId,
    List<Guid> QuestionIds, Guid CreatedByUserId,
    int? TimeLimitMinutes = null, int? MaxAttempts = null,
    bool ShuffleQuestions = false, bool ShuffleAnswers = false,
    DateTime? AvailableFrom = null, DateTime? AvailableTo = null,
    bool ShowCorrectAnswers = true) : IRequest<TestDto>;

public class CreateTestHandler : IRequestHandler<CreateTestCommand, TestDto>
{
    private readonly DbContext _db;
    public CreateTestHandler(DbContext db) => _db = db;

    public async Task<TestDto> Handle(CreateTestCommand request, CancellationToken ct)
    {
        var group = await _db.Set<Group>()
            .FirstOrDefaultAsync(g => g.Id == request.GroupId && g.TeacherId == request.CreatedByUserId, ct)
            ?? throw new NotFoundException("Group", request.GroupId);

        var test = new Test
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            GroupId = request.GroupId,
            TopicModuleId = request.TopicModuleId,
            CreatedByUserId = request.CreatedByUserId,
            TimeLimitMinutes = request.TimeLimitMinutes,
            MaxAttempts = request.MaxAttempts,
            ShuffleQuestions = request.ShuffleQuestions,
            ShuffleAnswers = request.ShuffleAnswers,
            ShowCorrectAnswers = request.ShowCorrectAnswers,
            AvailableFrom = request.AvailableFrom.HasValue ? DateTime.SpecifyKind(request.AvailableFrom.Value, DateTimeKind.Utc) : null,
            AvailableTo = request.AvailableTo.HasValue ? DateTime.SpecifyKind(request.AvailableTo.Value, DateTimeKind.Utc) : null
        };

        _db.Set<Test>().Add(test);

        for (int i = 0; i < request.QuestionIds.Count; i++)
        {
            _db.Set<TestQuestion>().Add(new TestQuestion
            {
                TestId = test.Id,
                QuestionId = request.QuestionIds[i],
                OrderIndex = i
            });
        }

        await _db.SaveChangesAsync(ct);

        string? topicName = null;
        if (request.TopicModuleId.HasValue)
        {
            topicName = (await _db.Set<TopicModule>().FindAsync(new object[] { request.TopicModuleId.Value }, ct))?.Title;
        }

        var topicNames = await _db.Set<TestQuestion>()
            .Where(tq => tq.TestId == test.Id)
            .Select(tq => tq.Question.TopicModule.Title)
            .Distinct()
            .ToListAsync(ct);

        return new TestDto(test.Id, test.Title, test.Description, test.GroupId, group.Name,
            test.TopicModuleId, topicName, topicNames, request.QuestionIds.Count, test.IsActive, test.CreatedAt,
            test.TimeLimitMinutes, test.MaxAttempts, test.ShuffleQuestions, test.ShuffleAnswers,
            test.AvailableFrom, test.AvailableTo, test.ShowCorrectAnswers);
    }
}

// Get Tests By Group
public record GetTestsByGroupQuery(Guid GroupId) : IRequest<List<TestDto>>;

public class GetTestsByGroupHandler : IRequestHandler<GetTestsByGroupQuery, List<TestDto>>
{
    private readonly DbContext _db;
    public GetTestsByGroupHandler(DbContext db) => _db = db;

    public async Task<List<TestDto>> Handle(GetTestsByGroupQuery request, CancellationToken ct)
    {
        // Auto-deactivate tests whose deadline has passed
        var now = DateTime.UtcNow;
        var expiredTests = await _db.Set<Test>()
            .Where(t => t.GroupId == request.GroupId && t.IsActive && t.AvailableTo.HasValue && t.AvailableTo.Value < now)
            .ToListAsync(ct);

        if (expiredTests.Count > 0)
        {
            foreach (var t in expiredTests) t.IsActive = false;
            await _db.SaveChangesAsync(ct);
        }

        return await _db.Set<Test>()
            .Include(t => t.Group)
            .Include(t => t.TopicModule)
            .Include(t => t.TestQuestions).ThenInclude(tq => tq.Question).ThenInclude(q => q.TopicModule)
            .Where(t => t.GroupId == request.GroupId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TestDto(t.Id, t.Title, t.Description, t.GroupId, t.Group.Name,
                t.TopicModuleId, t.TopicModule != null ? t.TopicModule.Title : null,
                t.TestQuestions.Select(tq => tq.Question.TopicModule.Title).Distinct().ToList(),
                t.TestQuestions.Count, t.IsActive, t.CreatedAt,
                t.TimeLimitMinutes, t.MaxAttempts, t.ShuffleQuestions, t.ShuffleAnswers,
                t.AvailableFrom, t.AvailableTo, t.ShowCorrectAnswers))
            .ToListAsync(ct);
    }
}

// Get Test By Id
public record GetTestByIdQuery(Guid Id) : IRequest<TestDto>;

public class GetTestByIdHandler : IRequestHandler<GetTestByIdQuery, TestDto>
{
    private readonly DbContext _db;
    public GetTestByIdHandler(DbContext db) => _db = db;

    public async Task<TestDto> Handle(GetTestByIdQuery request, CancellationToken ct)
    {
        var t = await _db.Set<Test>()
            .Include(t => t.Group)
            .Include(t => t.TopicModule)
            .Include(t => t.TestQuestions).ThenInclude(tq => tq.Question).ThenInclude(q => q.TopicModule)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("Test", request.Id);

        var topicNames = t.TestQuestions.Select(tq => tq.Question.TopicModule.Title).Distinct().ToList();

        return new TestDto(t.Id, t.Title, t.Description, t.GroupId, t.Group.Name,
            t.TopicModuleId, t.TopicModule?.Title, topicNames, t.TestQuestions.Count, t.IsActive, t.CreatedAt,
            t.TimeLimitMinutes, t.MaxAttempts, t.ShuffleQuestions, t.ShuffleAnswers,
            t.AvailableFrom, t.AvailableTo, t.ShowCorrectAnswers);
    }
}

// Update Test
public record UpdateTestCommand(
    Guid Id, Guid TeacherId, string? Title, string? Description, bool? IsActive,
    int? TimeLimitMinutes = null, int? MaxAttempts = null,
    bool? ShuffleQuestions = null, bool? ShuffleAnswers = null,
    DateTime? AvailableFrom = null, DateTime? AvailableTo = null,
    bool ClearTimeLimitMinutes = false, bool ClearMaxAttempts = false,
    bool ClearAvailableFrom = false, bool ClearAvailableTo = false,
    bool? ShowCorrectAnswers = null) : IRequest<TestDto>;

public class UpdateTestHandler : IRequestHandler<UpdateTestCommand, TestDto>
{
    private readonly DbContext _db;
    public UpdateTestHandler(DbContext db) => _db = db;

    public async Task<TestDto> Handle(UpdateTestCommand request, CancellationToken ct)
    {
        var test = await _db.Set<Test>()
            .Include(t => t.Group)
            .Include(t => t.TopicModule)
            .Include(t => t.TestQuestions).ThenInclude(tq => tq.Question).ThenInclude(q => q.TopicModule)
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.CreatedByUserId == request.TeacherId, ct)
            ?? throw new NotFoundException("Test", request.Id);

        if (request.Title != null) test.Title = request.Title;
        if (request.Description != null) test.Description = request.Description;
        if (request.IsActive.HasValue) test.IsActive = request.IsActive.Value;

        if (request.ClearTimeLimitMinutes) test.TimeLimitMinutes = null;
        else if (request.TimeLimitMinutes.HasValue) test.TimeLimitMinutes = request.TimeLimitMinutes.Value;

        if (request.ClearMaxAttempts) test.MaxAttempts = null;
        else if (request.MaxAttempts.HasValue) test.MaxAttempts = request.MaxAttempts.Value;

        if (request.ShuffleQuestions.HasValue) test.ShuffleQuestions = request.ShuffleQuestions.Value;
        if (request.ShuffleAnswers.HasValue) test.ShuffleAnswers = request.ShuffleAnswers.Value;
        if (request.ShowCorrectAnswers.HasValue) test.ShowCorrectAnswers = request.ShowCorrectAnswers.Value;

        if (request.ClearAvailableFrom) test.AvailableFrom = null;
        else if (request.AvailableFrom.HasValue)
            test.AvailableFrom = DateTime.SpecifyKind(request.AvailableFrom.Value, DateTimeKind.Utc);

        if (request.ClearAvailableTo) test.AvailableTo = null;
        else if (request.AvailableTo.HasValue)
            test.AvailableTo = DateTime.SpecifyKind(request.AvailableTo.Value, DateTimeKind.Utc);

        await _db.SaveChangesAsync(ct);

        var topicNames = test.TestQuestions.Select(tq => tq.Question.TopicModule.Title).Distinct().ToList();

        return new TestDto(test.Id, test.Title, test.Description, test.GroupId, test.Group.Name,
            test.TopicModuleId, test.TopicModule?.Title, topicNames, test.TestQuestions.Count, test.IsActive, test.CreatedAt,
            test.TimeLimitMinutes, test.MaxAttempts, test.ShuffleQuestions, test.ShuffleAnswers,
            test.AvailableFrom, test.AvailableTo, test.ShowCorrectAnswers);
    }
}

// Delete Test
public record DeleteTestCommand(Guid Id, Guid TeacherId) : IRequest;

public class DeleteTestHandler : IRequestHandler<DeleteTestCommand>
{
    private readonly DbContext _db;
    public DeleteTestHandler(DbContext db) => _db = db;

    public async Task Handle(DeleteTestCommand request, CancellationToken ct)
    {
        var test = await _db.Set<Test>()
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.CreatedByUserId == request.TeacherId, ct)
            ?? throw new NotFoundException("Test", request.Id);

        _db.Set<Test>().Remove(test);
        await _db.SaveChangesAsync(ct);
    }
}

// Get Assigned Tests (Student — tests from my groups)
public record GetAssignedTestsQuery(Guid StudentId) : IRequest<List<AssignedTestDto>>;

public class GetAssignedTestsHandler : IRequestHandler<GetAssignedTestsQuery, List<AssignedTestDto>>
{
    private readonly DbContext _db;
    public GetAssignedTestsHandler(DbContext db) => _db = db;

    public async Task<List<AssignedTestDto>> Handle(GetAssignedTestsQuery request, CancellationToken ct)
    {
        var groupIds = await _db.Set<GroupMember>()
            .Where(gm => gm.StudentId == request.StudentId)
            .Select(gm => gm.GroupId)
            .ToListAsync(ct);

        var tests = await _db.Set<Test>()
            .Include(t => t.Group)
            .Include(t => t.TopicModule)
            .Include(t => t.TestQuestions).ThenInclude(tq => tq.Question).ThenInclude(q => q.TopicModule)
            .Where(t => groupIds.Contains(t.GroupId) && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var testIds = tests.Select(t => t.Id).ToList();
        var attemptCounts = await _db.Set<TestSession>()
            .Where(s => s.StudentId == request.StudentId && testIds.Contains(s.TestId!.Value)
                && (s.Status == TestSessionStatus.Completed || s.Status == TestSessionStatus.InProgress))
            .GroupBy(s => s.TestId)
            .Select(g => new { TestId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var attemptsDict = attemptCounts.ToDictionary(x => x.TestId!.Value, x => x.Count);

        return tests.Select(t => new AssignedTestDto(t.Id, t.Title, t.Description, t.Group.Name,
            t.TopicModule != null ? t.TopicModule.Title : null,
            t.TestQuestions.Select(tq => tq.Question.TopicModule.Title).Distinct().ToList(),
            t.TestQuestions.Count, t.IsActive,
            t.TimeLimitMinutes, t.MaxAttempts,
            attemptsDict.GetValueOrDefault(t.Id, 0),
            t.AvailableFrom, t.AvailableTo)).ToList();
    }
}

// Get Assigned Tests By Group (Student)
public record GetAssignedTestsByGroupQuery(Guid GroupId, Guid StudentId) : IRequest<List<AssignedTestDto>>;

public class GetAssignedTestsByGroupHandler : IRequestHandler<GetAssignedTestsByGroupQuery, List<AssignedTestDto>>
{
    private readonly DbContext _db;
    public GetAssignedTestsByGroupHandler(DbContext db) => _db = db;

    public async Task<List<AssignedTestDto>> Handle(GetAssignedTestsByGroupQuery request, CancellationToken ct)
    {
        var isMember = await _db.Set<GroupMember>()
            .AnyAsync(gm => gm.GroupId == request.GroupId && gm.StudentId == request.StudentId, ct);

        if (!isMember)
            throw new ForbiddenException("Ви не є учасником цієї групи");

        var tests = await _db.Set<Test>()
            .Include(t => t.Group)
            .Include(t => t.TopicModule)
            .Include(t => t.TestQuestions).ThenInclude(tq => tq.Question).ThenInclude(q => q.TopicModule)
            .Where(t => t.GroupId == request.GroupId && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var testIds = tests.Select(t => t.Id).ToList();
        var attemptCounts = await _db.Set<TestSession>()
            .Where(s => s.StudentId == request.StudentId && testIds.Contains(s.TestId!.Value)
                && (s.Status == TestSessionStatus.Completed || s.Status == TestSessionStatus.InProgress))
            .GroupBy(s => s.TestId)
            .Select(g => new { TestId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var attemptsDict = attemptCounts.ToDictionary(x => x.TestId!.Value, x => x.Count);

        return tests.Select(t => new AssignedTestDto(t.Id, t.Title, t.Description, t.Group.Name,
            t.TopicModule != null ? t.TopicModule.Title : null,
            t.TestQuestions.Select(tq => tq.Question.TopicModule.Title).Distinct().ToList(),
            t.TestQuestions.Count, t.IsActive,
            t.TimeLimitMinutes, t.MaxAttempts,
            attemptsDict.GetValueOrDefault(t.Id, 0),
            t.AvailableFrom, t.AvailableTo)).ToList();
    }
}
