using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Topics;
using TestSystem.Models.Entities;

namespace TestSystem.Services.Features.Topics;

public record GetTopicsQuery(Guid? SubjectId = null) : IRequest<List<TopicModuleDto>>;

public class GetTopicsHandler : IRequestHandler<GetTopicsQuery, List<TopicModuleDto>>
{
    private readonly DbContext _db;
    public GetTopicsHandler(DbContext db) => _db = db;

    public async Task<List<TopicModuleDto>> Handle(GetTopicsQuery request, CancellationToken ct)
    {
        var query = _db.Set<TopicModule>().AsQueryable();
        if (request.SubjectId.HasValue) query = query.Where(t => t.SubjectId == request.SubjectId.Value);

        return await query
            .OrderBy(t => t.OrderIndex)
            .Select(t => new TopicModuleDto(
                t.Id, t.Title, t.SubjectId, t.Subject.Name,
                t.OrderIndex, t.Questions.Count(q => q.IsActive),
                t.CreatedByUserId, t.CreatedAt))
            .ToListAsync(ct);
    }
}

public record GetTopicByIdQuery(Guid Id) : IRequest<TopicModuleDto>;

public class GetTopicByIdHandler : IRequestHandler<GetTopicByIdQuery, TopicModuleDto>
{
    private readonly DbContext _db;
    public GetTopicByIdHandler(DbContext db) => _db = db;

    public async Task<TopicModuleDto> Handle(GetTopicByIdQuery request, CancellationToken ct)
    {
        var t = await _db.Set<TopicModule>().Include(t => t.Subject).Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("TopicModule", request.Id);

        return new TopicModuleDto(t.Id, t.Title, t.SubjectId, t.Subject.Name,
            t.OrderIndex, t.Questions.Count(q => q.IsActive), t.CreatedByUserId, t.CreatedAt);
    }
}

public record CreateTopicCommand(string Title, Guid SubjectId, int OrderIndex, Guid CreatedByUserId) : IRequest<TopicModuleDto>;

public class CreateTopicHandler : IRequestHandler<CreateTopicCommand, TopicModuleDto>
{
    private readonly DbContext _db;
    public CreateTopicHandler(DbContext db) => _db = db;

    public async Task<TopicModuleDto> Handle(CreateTopicCommand request, CancellationToken ct)
    {
        var subject = await _db.Set<Subject>().FindAsync(new object[] { request.SubjectId }, ct)
            ?? throw new NotFoundException("Subject", request.SubjectId);

        var topic = new TopicModule
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            SubjectId = request.SubjectId,
            OrderIndex = request.OrderIndex,
            CreatedByUserId = request.CreatedByUserId
        };

        _db.Set<TopicModule>().Add(topic);
        await _db.SaveChangesAsync(ct);

        return new TopicModuleDto(topic.Id, topic.Title, topic.SubjectId, subject.Name,
            topic.OrderIndex, 0, topic.CreatedByUserId, topic.CreatedAt);
    }
}

public record UpdateTopicCommand(Guid Id, string? Title, Guid? SubjectId, int? OrderIndex) : IRequest<TopicModuleDto>;

public class UpdateTopicHandler : IRequestHandler<UpdateTopicCommand, TopicModuleDto>
{
    private readonly DbContext _db;
    public UpdateTopicHandler(DbContext db) => _db = db;

    public async Task<TopicModuleDto> Handle(UpdateTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Set<TopicModule>().Include(t => t.Subject).Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("TopicModule", request.Id);

        if (request.Title != null) topic.Title = request.Title;
        if (request.SubjectId.HasValue) topic.SubjectId = request.SubjectId.Value;
        if (request.OrderIndex.HasValue) topic.OrderIndex = request.OrderIndex.Value;

        await _db.SaveChangesAsync(ct);

        return new TopicModuleDto(topic.Id, topic.Title, topic.SubjectId, topic.Subject.Name,
            topic.OrderIndex, topic.Questions.Count(q => q.IsActive), topic.CreatedByUserId, topic.CreatedAt);
    }
}

public record DeleteTopicCommand(Guid Id) : IRequest;

public class DeleteTopicHandler : IRequestHandler<DeleteTopicCommand>
{
    private readonly DbContext _db;
    public DeleteTopicHandler(DbContext db) => _db = db;

    public async Task Handle(DeleteTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Set<TopicModule>().FindAsync(new object[] { request.Id }, ct)
            ?? throw new NotFoundException("TopicModule", request.Id);
        _db.Set<TopicModule>().Remove(topic);
        await _db.SaveChangesAsync(ct);
    }
}
