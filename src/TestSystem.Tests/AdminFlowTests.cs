using FluentAssertions;
using TestSystem.Models.Enums;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Questions;
using TestSystem.Services.Features.Questions;
using TestSystem.Services.Features.Subjects;
using TestSystem.Services.Features.Topics;
using TestSystem.Services.Features.Users;

namespace TestSystem.Tests;

/// <summary>
/// Admin flow: управління користувачами, предметами, темами, питаннями.
/// </summary>
public class AdminFlowTests
{
    // ===== Users =====

    [Fact]
    public async Task GetUsers_ReturnsAllUsers()
    {
        using var db = TestDbHelper.CreateDb();
        TestDbHelper.SeedAdmin(db);
        TestDbHelper.SeedTeacher(db);
        TestDbHelper.SeedStudent(db);

        var handler = new GetUsersHandler(db);
        var result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetUsers_FilterByRole_ReturnsFiltered()
    {
        using var db = TestDbHelper.CreateDb();
        TestDbHelper.SeedAdmin(db);
        TestDbHelper.SeedTeacher(db);
        TestDbHelper.SeedStudent(db);

        var handler = new GetUsersHandler(db);
        var result = await handler.Handle(new GetUsersQuery(UserRole.Teacher), CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().Role.Should().Be("Teacher");
    }

    [Fact]
    public async Task GetUserById_ExistingUser_ReturnsDto()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);

        var handler = new GetUserByIdHandler(db);
        var result = await handler.Handle(new GetUserByIdQuery(admin.Id), CancellationToken.None);

        result.Email.Should().Be("admin@test.com");
    }

    [Fact]
    public async Task GetUserById_NonExistent_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();

        var handler = new GetUserByIdHandler(db);
        var act = () => handler.Handle(new GetUserByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateUser_ChangeRole_UpdatesSuccessfully()
    {
        using var db = TestDbHelper.CreateDb();
        var student = TestDbHelper.SeedStudent(db);

        var handler = new UpdateUserHandler(db);
        var result = await handler.Handle(
            new UpdateUserCommand(student.Id, null, null, UserRole.Teacher, null),
            CancellationToken.None);

        result.Role.Should().Be("Teacher");
    }

    [Fact]
    public async Task UpdateUser_Deactivate_SetsInactive()
    {
        using var db = TestDbHelper.CreateDb();
        var student = TestDbHelper.SeedStudent(db);

        var handler = new UpdateUserHandler(db);
        var result = await handler.Handle(
            new UpdateUserCommand(student.Id, null, null, null, false),
            CancellationToken.None);

        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUser_ExistingUser_Removes()
    {
        using var db = TestDbHelper.CreateDb();
        var student = TestDbHelper.SeedStudent(db);

        var handler = new DeleteUserHandler(db);
        await handler.Handle(new DeleteUserCommand(student.Id), CancellationToken.None);

        db.Users.Any(u => u.Id == student.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUser_NonExistent_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();

        var handler = new DeleteUserHandler(db);
        var act = () => handler.Handle(new DeleteUserCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ===== Subjects =====

    [Fact]
    public async Task CreateSubject_NewName_ReturnsDto()
    {
        using var db = TestDbHelper.CreateDb();

        var handler = new CreateSubjectHandler(db);
        var result = await handler.Handle(
            new CreateSubjectCommand("Математика", "Алгебра і геометрія"),
            CancellationToken.None);

        result.Name.Should().Be("Математика");
        result.TopicCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateSubject_DuplicateName_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        TestDbHelper.SeedSubject(db, "Математика");

        var handler = new CreateSubjectHandler(db);
        var act = () => handler.Handle(
            new CreateSubjectCommand("Математика", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*існує*");
    }

    [Fact]
    public async Task UpdateSubject_ChangeName_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var subject = TestDbHelper.SeedSubject(db, "Старе");

        var handler = new UpdateSubjectHandler(db);
        var result = await handler.Handle(
            new UpdateSubjectCommand(subject.Id, "Нове", null),
            CancellationToken.None);

        result.Name.Should().Be("Нове");
    }

    [Fact]
    public async Task DeleteSubject_ExistingSubject_Removes()
    {
        using var db = TestDbHelper.CreateDb();
        var subject = TestDbHelper.SeedSubject(db);

        var handler = new DeleteSubjectHandler(db);
        await handler.Handle(new DeleteSubjectCommand(subject.Id), CancellationToken.None);

        db.Subjects.Any(s => s.Id == subject.Id).Should().BeFalse();
    }

    [Fact]
    public async Task GetSubjects_ReturnsList()
    {
        using var db = TestDbHelper.CreateDb();
        TestDbHelper.SeedSubject(db, "Математика");
        TestDbHelper.SeedSubject(db, "Фізика");

        var handler = new GetSubjectsHandler(db);
        var result = await handler.Handle(new GetSubjectsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    // ===== Topics =====

    [Fact]
    public async Task CreateTopic_ValidSubject_ReturnsDto()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);

        var handler = new CreateTopicHandler(db);
        var result = await handler.Handle(
            new CreateTopicCommand("Алгоритми сортування", subject.Id, 0, admin.Id),
            CancellationToken.None);

        result.Title.Should().Be("Алгоритми сортування");
        result.SubjectName.Should().Be("Інформатика");
    }

    [Fact]
    public async Task CreateTopic_NonExistentSubject_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);

        var handler = new CreateTopicHandler(db);
        var act = () => handler.Handle(
            new CreateTopicCommand("Тест", Guid.NewGuid(), 0, admin.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateTopic_ChangeTitle_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id, "Старий заголовок");

        var handler = new UpdateTopicHandler(db);
        var result = await handler.Handle(
            new UpdateTopicCommand(topic.Id, "Новий заголовок", null, null),
            CancellationToken.None);

        result.Title.Should().Be("Новий заголовок");
    }

    [Fact]
    public async Task DeleteTopic_Existing_Removes()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new DeleteTopicHandler(db);
        await handler.Handle(new DeleteTopicCommand(topic.Id), CancellationToken.None);

        db.TopicModules.Any(t => t.Id == topic.Id).Should().BeFalse();
    }

    [Fact]
    public async Task GetTopics_FilterBySubject_ReturnsFiltered()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var s1 = TestDbHelper.SeedSubject(db, "Математика");
        var s2 = TestDbHelper.SeedSubject(db, "Фізика");
        TestDbHelper.SeedTopic(db, s1.Id, admin.Id, "Алгебра");
        TestDbHelper.SeedTopic(db, s1.Id, admin.Id, "Геометрія");
        TestDbHelper.SeedTopic(db, s2.Id, admin.Id, "Механіка");

        var handler = new GetTopicsHandler(db);
        var result = await handler.Handle(new GetTopicsQuery(s1.Id), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    // ===== Questions =====

    [Fact]
    public async Task CreateQuestion_ValidData_ReturnsDto()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var result = await handler.Handle(new CreateQuestionCommand(
            "Що таке масив?", "Пояснення", topic.Id, DifficultyLevel.Easy, null,
            new List<CreateAnswerOptionRequest>
            {
                new("Структура даних", true, 0),
                new("Функція", false, 1)
            }), CancellationToken.None);

        result.Text.Should().Be("Що таке масив?");
        result.Points.Should().Be(1); // Easy = 1 point
        result.AnswerOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateQuestion_MediumDifficulty_2Points()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var result = await handler.Handle(new CreateQuestionCommand(
            "Питання?", null, topic.Id, DifficultyLevel.Medium, null,
            new List<CreateAnswerOptionRequest>
            {
                new("Так", true, 0),
                new("Ні", false, 1)
            }), CancellationToken.None);

        result.Points.Should().Be(2);
    }

    [Fact]
    public async Task CreateQuestion_HardDifficulty_3Points()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var result = await handler.Handle(new CreateQuestionCommand(
            "Складне питання?", null, topic.Id, DifficultyLevel.Hard, null,
            new List<CreateAnswerOptionRequest>
            {
                new("Правильно", true, 0),
                new("Неправильно", false, 1)
            }), CancellationToken.None);

        result.Points.Should().Be(3);
    }

    [Fact]
    public async Task CreateQuestion_NoCorrectAnswer_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var act = () => handler.Handle(new CreateQuestionCommand(
            "Питання?", null, topic.Id, DifficultyLevel.Easy, null,
            new List<CreateAnswerOptionRequest>
            {
                new("Ні 1", false, 0),
                new("Ні 2", false, 1)
            }), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*правильна*");
    }

    [Fact]
    public async Task UpdateQuestion_ChangeDifficulty_UpdatesPoints()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);
        var question = TestDbHelper.SeedQuestion(db, topic.Id, DifficultyLevel.Easy);

        var handler = new UpdateQuestionHandler(db);
        var result = await handler.Handle(
            new UpdateQuestionCommand(question.Id, null, null, DifficultyLevel.Hard, null, null, null),
            CancellationToken.None);

        result.DifficultyLevel.Should().Be("Hard");
        result.Points.Should().Be(3);
    }

    [Fact]
    public async Task DeleteQuestion_Existing_Removes()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);
        var question = TestDbHelper.SeedQuestion(db, topic.Id, DifficultyLevel.Easy);

        var handler = new DeleteQuestionHandler(db);
        await handler.Handle(new DeleteQuestionCommand(question.Id), CancellationToken.None);

        db.Questions.Any(q => q.Id == question.Id).Should().BeFalse();
    }

    [Fact]
    public async Task GetQuestions_FilterByDifficulty_ReturnsFiltered()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);
        TestDbHelper.SeedQuestion(db, topic.Id, DifficultyLevel.Easy, "Легке");
        TestDbHelper.SeedQuestion(db, topic.Id, DifficultyLevel.Hard, "Складне");

        var handler = new GetQuestionsHandler(db);
        var result = await handler.Handle(
            new GetQuestionsQuery(Difficulty: DifficultyLevel.Easy),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().DifficultyLevel.Should().Be("Easy");
    }

    [Fact]
    public async Task UpdateUser_ChangeName_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);

        var handler = new UpdateUserHandler(db);
        var result = await handler.Handle(
            new UpdateUserCommand(teacher.Id, "Нове Ім'я", "Нове Прізвище", null, null),
            CancellationToken.None);

        result.FirstName.Should().Be("Нове Ім'я");
        result.LastName.Should().Be("Нове Прізвище");
    }
}
