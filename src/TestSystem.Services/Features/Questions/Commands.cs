using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Questions;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;

namespace TestSystem.Services.Features.Questions;

public record GetQuestionsQuery(Guid? TopicModuleId = null, DifficultyLevel? Difficulty = null, bool? IsActive = null) : IRequest<List<QuestionDto>>;

public class GetQuestionsHandler : IRequestHandler<GetQuestionsQuery, List<QuestionDto>>
{
    private readonly DbContext _db;
    public GetQuestionsHandler(DbContext db) => _db = db;

    public async Task<List<QuestionDto>> Handle(GetQuestionsQuery request, CancellationToken ct)
    {
        var query = _db.Set<Question>()
            .Include(q => q.AnswerOptions)
            .Include(q => q.TopicModule)
            .AsQueryable();

        if (request.TopicModuleId.HasValue) query = query.Where(q => q.TopicModuleId == request.TopicModuleId.Value);
        if (request.Difficulty.HasValue) query = query.Where(q => q.DifficultyLevel == request.Difficulty.Value);
        if (request.IsActive.HasValue) query = query.Where(q => q.IsActive == request.IsActive.Value);

        return await query.OrderBy(q => q.CreatedAt).Select(q => new QuestionDto(
            q.Id, q.Text, q.Explanation, q.TopicModuleId, q.TopicModule.Title,
            q.DifficultyLevel.ToString(), q.Points, q.IsMultipleChoice,
            q.IsOpenAnswer, q.CorrectAnswerText, q.IgnoreCase, q.IgnoreSimilarLetters, q.IsActive,
            q.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => new AnswerOptionDto(a.Id, a.Text, a.IsCorrect, a.OrderIndex)).ToList()
        )).ToListAsync(ct);
    }
}

public record GetQuestionByIdQuery(Guid Id) : IRequest<QuestionDto>;

public class GetQuestionByIdHandler : IRequestHandler<GetQuestionByIdQuery, QuestionDto>
{
    private readonly DbContext _db;
    public GetQuestionByIdHandler(DbContext db) => _db = db;

    public async Task<QuestionDto> Handle(GetQuestionByIdQuery request, CancellationToken ct)
    {
        var q = await _db.Set<Question>()
            .Include(q => q.AnswerOptions)
            .Include(q => q.TopicModule)
            .FirstOrDefaultAsync(q => q.Id == request.Id, ct)
            ?? throw new NotFoundException("Question", request.Id);

        return new QuestionDto(q.Id, q.Text, q.Explanation, q.TopicModuleId, q.TopicModule.Title,
            q.DifficultyLevel.ToString(), q.Points, q.IsMultipleChoice,
            q.IsOpenAnswer, q.CorrectAnswerText, q.IgnoreCase, q.IgnoreSimilarLetters, q.IsActive,
            q.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => new AnswerOptionDto(a.Id, a.Text, a.IsCorrect, a.OrderIndex)).ToList());
    }
}

public record CreateQuestionCommand(
    string Text,
    string? Explanation,
    Guid TopicModuleId,
    DifficultyLevel DifficultyLevel,
    int? Points,
    List<CreateAnswerOptionRequest>? AnswerOptions,
    bool IsOpenAnswer = false,
    string? CorrectAnswerText = null,
    bool IgnoreCase = true,
    bool IgnoreSimilarLetters = true) : IRequest<QuestionDto>;

public class CreateQuestionHandler : IRequestHandler<CreateQuestionCommand, QuestionDto>
{
    private readonly DbContext _db;
    public CreateQuestionHandler(DbContext db) => _db = db;

    public async Task<QuestionDto> Handle(CreateQuestionCommand request, CancellationToken ct)
    {
        var topic = await _db.Set<TopicModule>().FindAsync(new object[] { request.TopicModuleId }, ct)
            ?? throw new NotFoundException("TopicModule", request.TopicModuleId);

        if (request.IsOpenAnswer)
        {
            if (string.IsNullOrWhiteSpace(request.CorrectAnswerText))
                throw new BadRequestException("Для відкритого питання потрібно вказати правильну відповідь");
        }
        else
        {
            if (request.AnswerOptions == null || !request.AnswerOptions.Any(a => a.IsCorrect))
                throw new BadRequestException("Потрібна хоча б одна правильна відповідь");
        }

        var defaultPoints = request.DifficultyLevel switch
        {
            DifficultyLevel.Easy => 1,
            DifficultyLevel.Medium => 2,
            DifficultyLevel.Hard => 3,
            _ => 1
        };
        var points = request.Points ?? defaultPoints;
        if (points < 1) throw new BadRequestException("Кількість балів має бути не менше 1");

        var isMultipleChoice = !request.IsOpenAnswer && (request.AnswerOptions?.Count(a => a.IsCorrect) > 1);

        var question = new Question
        {
            Id = Guid.NewGuid(),
            Text = request.Text,
            Explanation = request.Explanation,
            TopicModuleId = request.TopicModuleId,
            DifficultyLevel = request.DifficultyLevel,
            Points = points,
            IsMultipleChoice = isMultipleChoice,
            IsOpenAnswer = request.IsOpenAnswer,
            CorrectAnswerText = request.IsOpenAnswer ? request.CorrectAnswerText?.Trim() : null,
            IgnoreCase = request.IgnoreCase,
            IgnoreSimilarLetters = request.IgnoreSimilarLetters
        };

        if (!request.IsOpenAnswer && request.AnswerOptions != null)
        {
            foreach (var ao in request.AnswerOptions)
            {
                question.AnswerOptions.Add(new AnswerOption
                {
                    Id = Guid.NewGuid(),
                    Text = ao.Text,
                    IsCorrect = ao.IsCorrect,
                    OrderIndex = ao.OrderIndex
                });
            }
        }

        _db.Set<Question>().Add(question);
        await _db.SaveChangesAsync(ct);

        return new QuestionDto(question.Id, question.Text, question.Explanation, question.TopicModuleId,
            topic.Title, question.DifficultyLevel.ToString(), question.Points, question.IsMultipleChoice,
            question.IsOpenAnswer, question.CorrectAnswerText, question.IgnoreCase, question.IgnoreSimilarLetters, question.IsActive,
            question.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => new AnswerOptionDto(a.Id, a.Text, a.IsCorrect, a.OrderIndex)).ToList());
    }
}

public record UpdateQuestionCommand(
    Guid Id,
    string? Text,
    string? Explanation,
    DifficultyLevel? DifficultyLevel,
    int? Points,
    bool? IsActive,
    List<UpdateAnswerOptionRequest>? AnswerOptions,
    bool? IsOpenAnswer = null,
    string? CorrectAnswerText = null,
    bool? IgnoreCase = null,
    bool? IgnoreSimilarLetters = null) : IRequest<QuestionDto>;

public class UpdateQuestionHandler : IRequestHandler<UpdateQuestionCommand, QuestionDto>
{
    private readonly DbContext _db;
    public UpdateQuestionHandler(DbContext db) => _db = db;

    public async Task<QuestionDto> Handle(UpdateQuestionCommand request, CancellationToken ct)
    {
        var question = await _db.Set<Question>()
            .Include(q => q.AnswerOptions)
            .Include(q => q.TopicModule)
            .FirstOrDefaultAsync(q => q.Id == request.Id, ct)
            ?? throw new NotFoundException("Question", request.Id);

        if (request.Text != null) question.Text = request.Text;
        if (request.Explanation != null) question.Explanation = request.Explanation;
        if (request.IsActive.HasValue) question.IsActive = request.IsActive.Value;
        if (request.IsOpenAnswer.HasValue)
        {
            question.IsOpenAnswer = request.IsOpenAnswer.Value;
            if (request.IsOpenAnswer.Value)
            {
                question.IsMultipleChoice = false;
                question.CorrectAnswerText = request.CorrectAnswerText?.Trim();
                // Remove answer options for open answer questions
                _db.Set<AnswerOption>().RemoveRange(question.AnswerOptions);
                question.AnswerOptions.Clear();
            }
        }
        if (request.CorrectAnswerText != null) question.CorrectAnswerText = request.CorrectAnswerText.Trim();
        if (request.IgnoreCase.HasValue) question.IgnoreCase = request.IgnoreCase.Value;
        if (request.IgnoreSimilarLetters.HasValue) question.IgnoreSimilarLetters = request.IgnoreSimilarLetters.Value;

        if (request.Points.HasValue)
        {
            if (request.Points.Value < 1) throw new BadRequestException("Кількість балів має бути не менше 1");
            question.Points = request.Points.Value;
        }

        if (request.DifficultyLevel.HasValue)
        {
            question.DifficultyLevel = request.DifficultyLevel.Value;
            // Only auto-set points if custom points not provided
            if (!request.Points.HasValue)
            {
                question.Points = request.DifficultyLevel.Value switch
                {
                    DifficultyLevel.Easy => 1,
                    DifficultyLevel.Medium => 2,
                    DifficultyLevel.Hard => 3,
                    _ => 1
                };
            }
        }

        if (request.AnswerOptions != null)
        {
            _db.Set<AnswerOption>().RemoveRange(question.AnswerOptions);
            question.AnswerOptions.Clear();
            question.IsMultipleChoice = request.AnswerOptions.Count(a => a.IsCorrect) > 1;
            await _db.SaveChangesAsync(ct);

            foreach (var ao in request.AnswerOptions)
            {
                var newOption = new AnswerOption
                {
                    Id = ao.Id ?? Guid.NewGuid(),
                    QuestionId = question.Id,
                    Text = ao.Text,
                    IsCorrect = ao.IsCorrect,
                    OrderIndex = ao.OrderIndex
                };
                _db.Set<AnswerOption>().Add(newOption);
            }
            await _db.SaveChangesAsync(ct);

            // Reload to get fresh navigation collection
            await _db.Entry(question).Collection(q => q.AnswerOptions).LoadAsync(ct);
        }
        else
        {
            await _db.SaveChangesAsync(ct);
        }

        return new QuestionDto(question.Id, question.Text, question.Explanation, question.TopicModuleId,
            question.TopicModule.Title, question.DifficultyLevel.ToString(), question.Points, question.IsMultipleChoice,
            question.IsOpenAnswer, question.CorrectAnswerText, question.IgnoreCase, question.IgnoreSimilarLetters, question.IsActive,
            question.AnswerOptions.OrderBy(a => a.OrderIndex).Select(a => new AnswerOptionDto(a.Id, a.Text, a.IsCorrect, a.OrderIndex)).ToList());
    }
}

public record DeleteQuestionCommand(Guid Id) : IRequest;

public class DeleteQuestionHandler : IRequestHandler<DeleteQuestionCommand>
{
    private readonly DbContext _db;
    public DeleteQuestionHandler(DbContext db) => _db = db;

    public async Task Handle(DeleteQuestionCommand request, CancellationToken ct)
    {
        var question = await _db.Set<Question>().FindAsync(new object[] { request.Id }, ct)
            ?? throw new NotFoundException("Question", request.Id);
        _db.Set<Question>().Remove(question);
        await _db.SaveChangesAsync(ct);
    }
}

public record BulkCreateQuestionsCommand(List<CreateQuestionCommand> Questions) : IRequest<List<QuestionDto>>;

public class BulkCreateQuestionsHandler : IRequestHandler<BulkCreateQuestionsCommand, List<QuestionDto>>
{
    private readonly IMediator _mediator;
    public BulkCreateQuestionsHandler(IMediator mediator) => _mediator = mediator;

    public async Task<List<QuestionDto>> Handle(BulkCreateQuestionsCommand request, CancellationToken ct)
    {
        var results = new List<QuestionDto>();
        foreach (var q in request.Questions)
            results.Add(await _mediator.Send(q, ct));
        return results;
    }
}
