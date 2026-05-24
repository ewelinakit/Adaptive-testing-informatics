using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Subjects;
using TestSystem.Models.Entities;

namespace TestSystem.Services.Features.Subjects;

public record GetSubjectsQuery() : IRequest<List<SubjectDto>>;

public class GetSubjectsHandler : IRequestHandler<GetSubjectsQuery, List<SubjectDto>>
{
    private readonly DbContext _db;
    public GetSubjectsHandler(DbContext db) => _db = db;

    public async Task<List<SubjectDto>> Handle(GetSubjectsQuery request, CancellationToken ct)
    {
        return await _db.Set<Subject>()
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.Description, s.TopicModules.Count))
            .ToListAsync(ct);
    }
}

public record CreateSubjectCommand(string Name, string? Description) : IRequest<SubjectDto>;

public class CreateSubjectHandler : IRequestHandler<CreateSubjectCommand, SubjectDto>
{
    private readonly DbContext _db;
    public CreateSubjectHandler(DbContext db) => _db = db;

    public async Task<SubjectDto> Handle(CreateSubjectCommand request, CancellationToken ct)
    {
        if (await _db.Set<Subject>().AnyAsync(s => s.Name == request.Name, ct))
            throw new BadRequestException($"Предмет '{request.Name}' вже існує");

        var subject = new Subject { Id = Guid.NewGuid(), Name = request.Name, Description = request.Description };
        _db.Set<Subject>().Add(subject);
        await _db.SaveChangesAsync(ct);
        return new SubjectDto(subject.Id, subject.Name, subject.Description, 0);
    }
}

public record UpdateSubjectCommand(Guid Id, string? Name, string? Description) : IRequest<SubjectDto>;

public class UpdateSubjectHandler : IRequestHandler<UpdateSubjectCommand, SubjectDto>
{
    private readonly DbContext _db;
    public UpdateSubjectHandler(DbContext db) => _db = db;

    public async Task<SubjectDto> Handle(UpdateSubjectCommand request, CancellationToken ct)
    {
        var subject = await _db.Set<Subject>().Include(s => s.TopicModules)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new NotFoundException("Subject", request.Id);

        if (request.Name != null) subject.Name = request.Name;
        if (request.Description != null) subject.Description = request.Description;
        await _db.SaveChangesAsync(ct);
        return new SubjectDto(subject.Id, subject.Name, subject.Description, subject.TopicModules.Count);
    }
}

public record DeleteSubjectCommand(Guid Id) : IRequest;

public class DeleteSubjectHandler : IRequestHandler<DeleteSubjectCommand>
{
    private readonly DbContext _db;
    public DeleteSubjectHandler(DbContext db) => _db = db;

    public async Task Handle(DeleteSubjectCommand request, CancellationToken ct)
    {
        var subject = await _db.Set<Subject>().FindAsync(new object[] { request.Id }, ct)
            ?? throw new NotFoundException("Subject", request.Id);
        _db.Set<Subject>().Remove(subject);
        await _db.SaveChangesAsync(ct);
    }
}
