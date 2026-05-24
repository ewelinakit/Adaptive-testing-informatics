using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Testing;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;

namespace TestSystem.Services.Features.Testing;

// Get Pending Reviews for Teacher
public record GetPendingReviewsQuery(Guid TeacherId) : IRequest<List<PendingReviewDto>>;

public class GetPendingReviewsHandler : IRequestHandler<GetPendingReviewsQuery, List<PendingReviewDto>>
{
    private readonly DbContext _db;
    public GetPendingReviewsHandler(DbContext db) => _db = db;

    public async Task<List<PendingReviewDto>> Handle(GetPendingReviewsQuery request, CancellationToken ct)
    {
        // Get tests created by this teacher
        var teacherTestIds = await _db.Set<Test>()
            .Where(t => t.CreatedByUserId == request.TeacherId)
            .Select(t => t.Id)
            .ToListAsync(ct);

        return await _db.Set<TestSessionAnswer>()
            .Include(a => a.TestSession).ThenInclude(s => s.Student)
            .Include(a => a.TestSession).ThenInclude(s => s.Test)
            .Include(a => a.Question)
            .Where(a => a.ReviewStatus == OpenAnswerReviewStatus.Pending
                && a.TestSession.TestId.HasValue
                && teacherTestIds.Contains(a.TestSession.TestId.Value))
            .OrderBy(a => a.CreatedAt)
            .Select(a => new PendingReviewDto(
                a.Id,
                a.TestSessionId,
                a.TestSession.Student.FirstName + " " + a.TestSession.Student.LastName,
                a.TestSession.Test!.Title,
                a.Question.Text,
                a.TextAnswer,
                a.PointsAwarded,
                a.Question.Points,
                a.CreatedAt))
            .ToListAsync(ct);
    }
}

// Review an Answer
public record ReviewAnswerCommand(Guid AnswerId, Guid TeacherId, string Status, string? Feedback, int? Points) : IRequest<ReviewedAnswerDto>;

public class ReviewAnswerHandler : IRequestHandler<ReviewAnswerCommand, ReviewedAnswerDto>
{
    private readonly DbContext _db;
    public ReviewAnswerHandler(DbContext db) => _db = db;

    public async Task<ReviewedAnswerDto> Handle(ReviewAnswerCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<OpenAnswerReviewStatus>(request.Status, true, out var status))
            throw new BadRequestException("Невірний статус перевірки. Допустимі: Approved, Rejected");

        var answer = await _db.Set<TestSessionAnswer>()
            .Include(a => a.TestSession).ThenInclude(s => s.Test)
            .Include(a => a.Question)
            .FirstOrDefaultAsync(a => a.Id == request.AnswerId, ct)
            ?? throw new NotFoundException("Answer", request.AnswerId);

        // Verify teacher owns the test
        if (answer.TestSession.Test == null || answer.TestSession.Test.CreatedByUserId != request.TeacherId)
            throw new ForbiddenException("Ви не є автором цього тесту");

        var oldPoints = answer.OverriddenPoints ?? answer.PointsAwarded;

        answer.ReviewStatus = status;
        answer.ReviewFeedback = request.Feedback;
        answer.ReviewedByUserId = request.TeacherId;
        answer.ReviewedAt = DateTime.UtcNow;

        int newPoints;
        if (request.Points.HasValue)
        {
            newPoints = Math.Min(request.Points.Value, answer.Question.Points);
            newPoints = Math.Max(0, newPoints);
            answer.OverriddenPoints = newPoints;
        }
        else
        {
            newPoints = status == OpenAnswerReviewStatus.Approved ? answer.Question.Points : 0;
            answer.OverriddenPoints = newPoints;
        }

        // Update session score
        var session = answer.TestSession;
        session.TotalScore = session.TotalScore - oldPoints + newPoints;
        session.ScorePercentage = session.MaxPossibleScore > 0
            ? Math.Round((double)session.TotalScore / session.MaxPossibleScore * 100, 1)
            : 0;

        // Update correctness based on review
        if (status == OpenAnswerReviewStatus.Approved && !answer.IsCorrect)
        {
            answer.IsCorrect = true;
            session.CorrectAnswers++;
        }
        else if (status == OpenAnswerReviewStatus.Rejected && answer.IsCorrect)
        {
            answer.IsCorrect = false;
            session.CorrectAnswers = Math.Max(0, session.CorrectAnswers - 1);
        }

        await _db.SaveChangesAsync(ct);

        return new ReviewedAnswerDto(answer.Id, answer.ReviewStatus.ToString()!, answer.ReviewFeedback, newPoints);
    }
}
