using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Testing;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;

namespace TestSystem.Services.Features.Testing;

public static class TestingConstants
{
    public const int MaxQuestions = 20;
    public const int MinQuestions = 5;
    public static readonly TimeSpan InactivityTimeout = TimeSpan.FromHours(2);
}

// Start Test Session (from TestId)
public record StartTestCommand(Guid StudentId, Guid TestId) : IRequest<TestSessionDto>;

public class StartTestHandler : IRequestHandler<StartTestCommand, TestSessionDto>
{
    private readonly DbContext _db;
    public StartTestHandler(DbContext db) => _db = db;

    public async Task<TestSessionDto> Handle(StartTestCommand request, CancellationToken ct)
    {
        var test = await _db.Set<Test>()
            .Include(t => t.Group)
            .Include(t => t.TopicModule)
            .Include(t => t.TestQuestions).ThenInclude(tq => tq.Question).ThenInclude(q => q.TopicModule)
            .FirstOrDefaultAsync(t => t.Id == request.TestId && t.IsActive, ct)
            ?? throw new NotFoundException("Test", request.TestId);

        var isMember = await _db.Set<GroupMember>()
            .AnyAsync(gm => gm.GroupId == test.GroupId && gm.StudentId == request.StudentId, ct);
        if (!isMember)
            throw new ForbiddenException("Ви не є учасником групи, якій призначений цей тест");

        if (test.TestQuestions.Count == 0)
            throw new BadRequestException("Для цього тесту ще немає питань");

        // Feature 4: Schedule availability check
        var now = DateTime.UtcNow;
        if (test.AvailableFrom.HasValue && now < test.AvailableFrom.Value)
            throw new BadRequestException("Тест ще не доступний");
        if (test.AvailableTo.HasValue && now > test.AvailableTo.Value)
            throw new BadRequestException("Час доступу до тесту вже минув");

        // Feature 2: Max attempts check
        if (test.MaxAttempts.HasValue)
        {
            var attemptsUsed = await _db.Set<TestSession>()
                .CountAsync(s => s.StudentId == request.StudentId && s.TestId == request.TestId
                    && (s.Status == TestSessionStatus.Completed || s.Status == TestSessionStatus.InProgress), ct);
            if (attemptsUsed >= test.MaxAttempts.Value)
                throw new BadRequestException($"Ви вичерпали максимальну кількість спроб ({test.MaxAttempts.Value})");
        }

        // For single-topic tests use that topic, for multi-topic leave null
        var distinctTopicIds = test.TestQuestions
            .Select(tq => tq.Question.TopicModuleId)
            .Distinct().ToList();

        Guid? topicModuleId = test.TopicModuleId;
        if (!topicModuleId.HasValue && distinctTopicIds.Count == 1)
            topicModuleId = distinctTopicIds.First();

        var topicNames = test.TestQuestions
            .Select(tq => tq.Question.TopicModule.Title)
            .Distinct().ToList();
        var topicTitle = topicNames.Count == 1 ? topicNames.First() : "Комплексний тест";

        var session = new TestSession
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            TopicModuleId = topicModuleId,
            TestId = test.Id,
            Status = TestSessionStatus.InProgress,
            CurrentDifficulty = DifficultyLevel.Easy,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        // Feature 1: Calculate deadline
        if (test.TimeLimitMinutes.HasValue)
        {
            session.DeadlineAt = session.StartedAt.AddMinutes(test.TimeLimitMinutes.Value);
        }

        _db.Set<TestSession>().Add(session);
        await _db.SaveChangesAsync(ct);

        return new TestSessionDto(session.Id, session.TopicModuleId, topicTitle,
            test.Id, test.Title,
            session.Status.ToString(), session.CurrentDifficulty.ToString(),
            0, 0, 0, 0, 0, session.StartedAt, null, session.DeadlineAt);
    }
}

// Get Next Question (from TestQuestion pool)
public record GetNextQuestionQuery(Guid SessionId) : IRequest<TestQuestionDto?>;

public class GetNextQuestionHandler : IRequestHandler<GetNextQuestionQuery, TestQuestionDto?>
{
    private readonly DbContext _db;
    public GetNextQuestionHandler(DbContext db) => _db = db;

    public async Task<TestQuestionDto?> Handle(GetNextQuestionQuery request, CancellationToken ct)
    {
        var session = await _db.Set<TestSession>()
            .Include(s => s.Answers)
            .Include(s => s.Test)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, ct)
            ?? throw new NotFoundException("TestSession", request.SessionId);

        if (session.Status != TestSessionStatus.InProgress)
            return null;

        // Feature 1: Time limit check
        if (session.DeadlineAt.HasValue && DateTime.UtcNow > session.DeadlineAt.Value)
        {
            session.Status = TestSessionStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        // Feature 5: Auto-abandon inactive sessions
        if ((DateTime.UtcNow - session.LastActivityAt) > TestingConstants.InactivityTimeout)
        {
            session.Status = TestSessionStatus.Abandoned;
            session.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        var answeredIds = session.Answers.Select(a => a.QuestionId).ToList();

        IQueryable<Question> questionPool;
        int maxQuestions;

        if (session.TestId.HasValue)
        {
            // Feature 3: If ShuffleQuestions is false, order by OrderIndex
            var shuffleQuestions = session.Test?.ShuffleQuestions ?? false;

            var testQuestionQuery = _db.Set<TestQuestion>()
                .Where(tq => tq.TestId == session.TestId.Value);

            var testQuestionIds = await testQuestionQuery
                .Select(tq => tq.QuestionId)
                .ToListAsync(ct);

            questionPool = _db.Set<Question>()
                .Where(q => testQuestionIds.Contains(q.Id) && q.IsActive && !answeredIds.Contains(q.Id));

            maxQuestions = testQuestionIds.Count;

            if (session.TotalQuestions >= maxQuestions)
                return null;

            // For non-shuffled test questions, pick the next by OrderIndex
            if (!shuffleQuestions)
            {
                var question = await FindNextOrderedTestQuestionAsync(testQuestionQuery, answeredIds, session.CurrentDifficulty, ct);

                if (question == null)
                {
                    var adjacent = GetAdjacentDifficulties(session.CurrentDifficulty);
                    foreach (var diff in adjacent)
                    {
                        question = await FindNextOrderedTestQuestionAsync(testQuestionQuery, answeredIds, diff, ct);
                        if (question != null) break;
                    }
                }
                question ??= await FindNextOrderedTestQuestionAsync(testQuestionQuery, answeredIds, null, ct);
                return question == null ? null : BuildQuestionDto(question, session, maxQuestions);
            }
        }
        else
        {
            questionPool = _db.Set<Question>()
                .Where(q => q.TopicModuleId == session.TopicModuleId && q.IsActive && !answeredIds.Contains(q.Id));
            maxQuestions = TestingConstants.MaxQuestions;
        }

        if (session.TotalQuestions >= maxQuestions)
            return null;

        var foundQuestion = await questionPool
            .Include(q => q.AnswerOptions)
            .Where(q => q.DifficultyLevel == session.CurrentDifficulty)
            .OrderBy(q => Guid.NewGuid())
            .FirstOrDefaultAsync(ct);

        if (foundQuestion == null)
        {
            var adjacent = GetAdjacentDifficulties(session.CurrentDifficulty);
            foreach (var diff in adjacent)
            {
                foundQuestion = await questionPool
                    .Include(q => q.AnswerOptions)
                    .Where(q => q.DifficultyLevel == diff)
                    .OrderBy(q => Guid.NewGuid())
                    .FirstOrDefaultAsync(ct);
                if (foundQuestion != null) break;
            }
        }

        if (foundQuestion == null)
        {
            foundQuestion = await questionPool
                .Include(q => q.AnswerOptions)
                .OrderBy(q => Guid.NewGuid())
                .FirstOrDefaultAsync(ct);
        }

        if (foundQuestion == null)
            return null;

        return BuildQuestionDto(foundQuestion, session, maxQuestions);
    }

    private TestQuestionDto BuildQuestionDto(Question question, TestSession session, int maxQuestions)
    {
        var shuffleAnswers = session.Test?.ShuffleAnswers ?? false;

        var options = question.IsOpenAnswer
            ? new List<TestAnswerOptionDto>()
            : (shuffleAnswers
                ? question.AnswerOptions.OrderBy(_ => Guid.NewGuid())
                : question.AnswerOptions.OrderBy(a => a.OrderIndex))
                .Select(a => new TestAnswerOptionDto(a.Id, a.Text, a.OrderIndex)).ToList();

        return new TestQuestionDto(
            question.Id,
            question.Text,
            question.DifficultyLevel.ToString(),
            session.TotalQuestions + 1,
            maxQuestions,
            question.IsMultipleChoice,
            question.IsOpenAnswer,
            options);
    }

    private async Task<Question?> FindNextOrderedTestQuestionAsync(
        IQueryable<TestQuestion> testQuestionQuery,
        List<Guid> answersIds,
        DifficultyLevel? difficulty,
        CancellationToken ct)
    {
        var query = testQuestionQuery.Where(tq => !answersIds.Contains(tq.QuestionId) && tq.Question.IsActive);

        if (difficulty.HasValue)
            query = query.Where(tq => tq.Question.DifficultyLevel == difficulty.Value);

        var questionId = await query
            .OrderBy(tq => tq.OrderIndex)
            .Select(tq => (Guid?)tq.QuestionId)
            .FirstOrDefaultAsync(ct);

        if (!questionId.HasValue)
            return null;

        return await _db.Set<Question>()
            .Include(q => q.AnswerOptions)
            .FirstOrDefaultAsync(q => q.Id == questionId.Value, ct);
    }

    private static DifficultyLevel[] GetAdjacentDifficulties(DifficultyLevel current) => current switch
    {
        DifficultyLevel.Easy => new[] { DifficultyLevel.Medium },
        DifficultyLevel.Medium => new[] { DifficultyLevel.Easy, DifficultyLevel.Hard },
        DifficultyLevel.Hard => new[] { DifficultyLevel.Medium },
        _ => Array.Empty<DifficultyLevel>()
    };
}

// Submit Answer — supports single and multiple choice
public record SubmitAnswerCommand(Guid SessionId, Guid QuestionId, List<Guid>? SelectedAnswerOptionIds, string? TextAnswer = null) : IRequest<SubmitAnswerResponse>;

public class SubmitAnswerHandler : IRequestHandler<SubmitAnswerCommand, SubmitAnswerResponse>
{
    private readonly DbContext _db;
    public SubmitAnswerHandler(DbContext db) => _db = db;

    public async Task<SubmitAnswerResponse> Handle(SubmitAnswerCommand request, CancellationToken ct)
    {
        var session = await _db.Set<TestSession>()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.Status == TestSessionStatus.InProgress, ct)
            ?? throw new BadRequestException("Тестова сесія не знайдена або вже завершена");

        // Feature 1: Time limit check
        if (session.DeadlineAt.HasValue && DateTime.UtcNow > session.DeadlineAt.Value)
        {
            session.Status = TestSessionStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new BadRequestException("Час тесту вичерпано. Тест завершено автоматично.");
        }

        var alreadyAnswered = await _db.Set<TestSessionAnswer>()
            .AnyAsync(a => a.TestSessionId == session.Id && a.QuestionId == request.QuestionId, ct);

        if (alreadyAnswered)
            throw new BadRequestException("Question has already been answered in this session");

        var question = await _db.Set<Question>()
            .Include(q => q.AnswerOptions)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, ct)
            ?? throw new NotFoundException("Question", request.QuestionId);

        bool isCorrect;
        TestSessionAnswer answer;

        if (question.IsOpenAnswer)
        {
            if (string.IsNullOrWhiteSpace(request.TextAnswer))
                throw new BadRequestException("Потрібно ввести відповідь");

            var studentAnswer = request.TextAnswer.Trim();
            isCorrect = AreEquivalentOpenAnswers(
                studentAnswer,
                question.CorrectAnswerText,
                question.IgnoreCase,
                question.IgnoreSimilarLetters);
            var points = isCorrect ? question.Points : 0;

            answer = new TestSessionAnswer
            {
                Id = Guid.NewGuid(),
                TestSessionId = session.Id,
                QuestionId = question.Id,
                TextAnswer = studentAnswer,
                IsCorrect = isCorrect,
                DifficultyAtTime = session.CurrentDifficulty,
                PointsAwarded = points,
                // Feature 6: Open answers get Pending review status
                ReviewStatus = OpenAnswerReviewStatus.Pending
            };
        }
        else
        {
            if (request.SelectedAnswerOptionIds == null || request.SelectedAnswerOptionIds.Count == 0)
                throw new BadRequestException("Потрібно обрати хоча б один варіант відповіді");

            // Validate all selected options belong to this question
            var questionOptionIds = question.AnswerOptions.Select(a => a.Id).ToHashSet();
            foreach (var selectedId in request.SelectedAnswerOptionIds)
            {
                if (!questionOptionIds.Contains(selectedId))
                    throw new BadRequestException("Невірний варіант відповіді");
            }

            // Determine correctness
            var correctOptionIds = question.AnswerOptions.Where(a => a.IsCorrect).Select(a => a.Id).ToHashSet();
            var selectedIds = request.SelectedAnswerOptionIds.ToHashSet();
            isCorrect = correctOptionIds.SetEquals(selectedIds);
            var points = isCorrect ? question.Points : 0;

            answer = new TestSessionAnswer
            {
                Id = Guid.NewGuid(),
                TestSessionId = session.Id,
                QuestionId = question.Id,
                SelectedAnswerOptionId = request.SelectedAnswerOptionIds.First(),
                SelectedAnswerOptionIds = string.Join(",", request.SelectedAnswerOptionIds),
                IsCorrect = isCorrect,
                DifficultyAtTime = session.CurrentDifficulty,
                PointsAwarded = points
            };
        }

        _db.Set<TestSessionAnswer>().Add(answer);

        session.TotalQuestions++;
        session.TotalScore += answer.PointsAwarded;
        session.MaxPossibleScore += question.Points;
        if (isCorrect) session.CorrectAnswers++;

        // Feature 5: Update last activity
        session.LastActivityAt = DateTime.UtcNow;

        AdjustDifficulty(session, isCorrect);

        session.ScorePercentage = session.MaxPossibleScore > 0
            ? Math.Round((double)session.TotalScore / session.MaxPossibleScore * 100, 1)
            : 0;

        int maxQuestions = TestingConstants.MaxQuestions;
        if (session.TestId.HasValue)
        {
            maxQuestions = await _db.Set<TestQuestion>()
                .CountAsync(tq => tq.TestId == session.TestId.Value, ct);
        }

        var isFinished = session.TotalQuestions >= maxQuestions;
        if (isFinished)
        {
            session.Status = TestSessionStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return new SubmitAnswerResponse(
            isCorrect, answer.PointsAwarded, question.Explanation,
            session.CurrentDifficulty.ToString(),
            session.TotalScore, session.TotalQuestions, isFinished);
    }

    private static void AdjustDifficulty(TestSession session, bool isCorrect)
    {
        if (isCorrect)
        {
            session.ConsecutiveCorrect++;
            session.ConsecutiveWrong = 0;

            if (session.ConsecutiveCorrect >= 2 && session.CurrentDifficulty < DifficultyLevel.Hard)
            {
                session.CurrentDifficulty++;
                session.ConsecutiveCorrect = 0;
            }

            return;
        }

        session.ConsecutiveWrong++;
        session.ConsecutiveCorrect = 0;

        var wrongAnswersToDecrease = session.CurrentDifficulty switch
        {
            DifficultyLevel.Hard => 1,
            DifficultyLevel.Medium => 2,
            _ => int.MaxValue
        };

        if (session.ConsecutiveWrong >= wrongAnswersToDecrease && session.CurrentDifficulty > DifficultyLevel.Easy)
        {
            session.CurrentDifficulty--;
            session.ConsecutiveWrong = 0;
        }
    }

    private static bool AreEquivalentOpenAnswers(
        string studentAnswer,
        string? correctAnswer,
        bool ignoreCase,
        bool ignoreSimilarLetters)
    {
        return string.Equals(
            NormalizeOpenAnswerForComparison(studentAnswer, ignoreCase, ignoreSimilarLetters),
            NormalizeOpenAnswerForComparison(correctAnswer, ignoreCase, ignoreSimilarLetters),
            StringComparison.Ordinal);
    }

    private static string NormalizeOpenAnswerForComparison(string? answer, bool ignoreCase, bool ignoreSimilarLetters)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return string.Empty;

        var trimmed = answer.Trim();
        if (ignoreSimilarLetters)
            trimmed = new string(trimmed.Select(NormalizeLookalikeLetter).ToArray());

        return ignoreCase ? trimmed.ToUpperInvariant() : trimmed;
    }

    private static char NormalizeLookalikeLetter(char value) => value switch
    {
        '\u0410' or '\u0430' => 'A',
        '\u0412' or '\u0432' => 'B',
        '\u0421' or '\u0441' => 'C',
        '\u0415' or '\u0435' => 'E',
        '\u041D' or '\u043D' => 'H',
        '\u0406' or '\u0456' => 'I',
        '\u041A' or '\u043A' => 'K',
        '\u041C' or '\u043C' => 'M',
        '\u041E' or '\u043E' => 'O',
        '\u0420' or '\u0440' => 'P',
        '\u0422' or '\u0442' => 'T',
        '\u0425' or '\u0445' => 'X',
        _ => value
    };
}

// Finish Test (manual)
public record FinishTestCommand(Guid SessionId, Guid StudentId) : IRequest<TestSessionDto>;

public class FinishTestHandler : IRequestHandler<FinishTestCommand, TestSessionDto>
{
    private readonly DbContext _db;
    public FinishTestHandler(DbContext db) => _db = db;

    public async Task<TestSessionDto> Handle(FinishTestCommand request, CancellationToken ct)
    {
        var session = await _db.Set<TestSession>()
            .Include(s => s.TopicModule)
            .Include(s => s.Test)
            .Include(s => s.Answers).ThenInclude(a => a.Question).ThenInclude(q => q.TopicModule)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.StudentId == request.StudentId, ct)
            ?? throw new NotFoundException("TestSession", request.SessionId);

        if (session.Status != TestSessionStatus.InProgress)
            throw new BadRequestException("Тест вже завершено");

        if (session.TotalQuestions < TestingConstants.MinQuestions)
            throw new BadRequestException($"Потрібно відповісти мінімум на {TestingConstants.MinQuestions} питань");

        session.Status = TestSessionStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var topicTitle = session.TopicModule?.Title ?? "Комплексний тест";

        return new TestSessionDto(session.Id, session.TopicModuleId, topicTitle,
            session.TestId, session.Test?.Title,
            session.Status.ToString(), session.CurrentDifficulty.ToString(),
            session.TotalScore, session.MaxPossibleScore, session.ScorePercentage,
            session.TotalQuestions, session.CorrectAnswers, session.StartedAt, session.CompletedAt,
            session.DeadlineAt);
    }
}

// Get Test Result
public record GetTestResultQuery(Guid SessionId) : IRequest<TestResultDto>;

public class GetTestResultHandler : IRequestHandler<GetTestResultQuery, TestResultDto>
{
    private readonly DbContext _db;
    public GetTestResultHandler(DbContext db) => _db = db;

    public async Task<TestResultDto> Handle(GetTestResultQuery request, CancellationToken ct)
    {
        var session = await _db.Set<TestSession>()
            .Include(s => s.Test)
            .Include(s => s.TopicModule).ThenInclude(t => t!.Subject)
            .Include(s => s.Answers).ThenInclude(a => a.Question).ThenInclude(q => q.TopicModule).ThenInclude(tm => tm.Subject)
            .Include(s => s.Answers).ThenInclude(a => a.Question).ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, ct)
            ?? throw new NotFoundException("TestSession", request.SessionId);

        var showCorrectAnswers = session.Test?.ShowCorrectAnswers ?? true;

        var answers = session.Answers.OrderBy(a => a.CreatedAt).Select(a =>
        {
            var selectedIds = string.IsNullOrEmpty(a.SelectedAnswerOptionIds)
                ? (a.SelectedAnswerOptionId.HasValue ? new List<Guid> { a.SelectedAnswerOptionId.Value } : new List<Guid>())
                : a.SelectedAnswerOptionIds.Split(',').Select(Guid.Parse).ToList();

            var selectedTexts = a.Question.AnswerOptions
                .Where(o => selectedIds.Contains(o.Id))
                .Select(o => o.Text).ToList();

            var correctTexts = showCorrectAnswers
                ? a.Question.AnswerOptions.Where(o => o.IsCorrect).Select(o => o.Text).ToList()
                : new List<string>();

            return new TestAnswerDetailDto(
                a.QuestionId,
                a.Question.Text,
                showCorrectAnswers ? a.Question.Explanation : null,
                selectedIds,
                selectedTexts,
                correctTexts,
                a.IsCorrect,
                a.Question.DifficultyLevel.ToString(),
                a.OverriddenPoints ?? a.PointsAwarded,
                a.Question.IsOpenAnswer,
                a.TextAnswer,
                showCorrectAnswers ? a.Question.CorrectAnswerText : null,
                a.ReviewStatus?.ToString(),
                a.ReviewFeedback,
                a.OverriddenPoints
            );
        }).ToList();

        string topicTitle;
        string subjectName;
        if (session.TopicModule != null)
        {
            topicTitle = session.TopicModule.Title;
            subjectName = session.TopicModule.Subject.Name;
        }
        else
        {
            var topicNames = session.Answers.Select(a => a.Question.TopicModule.Title).Distinct().ToList();
            topicTitle = topicNames.Count == 1 ? topicNames.First() : string.Join(", ", topicNames);
            var subjectNames = session.Answers.Select(a => a.Question.TopicModule.Subject.Name).Distinct().ToList();
            subjectName = subjectNames.Count == 1 ? subjectNames.First() : string.Join(", ", subjectNames);
        }

        return new TestResultDto(session.Id, topicTitle, subjectName,
            session.TotalScore, session.MaxPossibleScore,
            session.ScorePercentage, session.TotalQuestions, session.CorrectAnswers,
            session.StartedAt, session.CompletedAt, answers, showCorrectAnswers);
    }
}

// Get Test History
public record GetTestHistoryQuery(Guid StudentId) : IRequest<List<TestSessionDto>>;

public class GetTestHistoryHandler : IRequestHandler<GetTestHistoryQuery, List<TestSessionDto>>
{
    private readonly DbContext _db;
    public GetTestHistoryHandler(DbContext db) => _db = db;

    public async Task<List<TestSessionDto>> Handle(GetTestHistoryQuery request, CancellationToken ct)
    {
        // Feature 5: batch-update stale sessions
        var staleSessions = await _db.Set<TestSession>()
            .Where(s => s.StudentId == request.StudentId && s.Status == TestSessionStatus.InProgress)
            .ToListAsync(ct);

        foreach (var s in staleSessions)
        {
            var isExpiredByTime = s.DeadlineAt.HasValue && DateTime.UtcNow > s.DeadlineAt.Value;
            var isInactive = (DateTime.UtcNow - s.LastActivityAt) > TestingConstants.InactivityTimeout;
            if (isInactive)
            {
                s.Status = TestSessionStatus.Abandoned;
                s.CompletedAt = DateTime.UtcNow;
            }
            else if (isExpiredByTime)
            {
                s.Status = TestSessionStatus.Completed;
                s.CompletedAt = DateTime.UtcNow;
            }
        }
        if (staleSessions.Any(s => s.Status != TestSessionStatus.InProgress))
            await _db.SaveChangesAsync(ct);

        return await _db.Set<TestSession>()
            .Include(s => s.TopicModule)
            .Include(s => s.Test)
            .Where(s => s.StudentId == request.StudentId)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new TestSessionDto(s.Id, s.TopicModuleId,
                s.TopicModule != null ? s.TopicModule.Title : (s.Test != null ? s.Test.Title : "Комплексний тест"),
                s.TestId, s.Test != null ? s.Test.Title : null,
                s.Status.ToString(), s.CurrentDifficulty.ToString(),
                s.TotalScore, s.MaxPossibleScore, s.ScorePercentage,
                s.TotalQuestions, s.CorrectAnswers, s.StartedAt, s.CompletedAt,
                s.DeadlineAt))
            .ToListAsync(ct);
    }
}
