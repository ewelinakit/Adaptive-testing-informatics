using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Questions;
using TestSystem.Services.Features.Questions;
using TestSystem.Services.Features.Testing;

namespace TestSystem.Tests;

public class OpenAnswerTests
{
    // ===== Create Open Answer Question =====

    [Fact]
    public async Task CreateQuestion_OpenAnswer_SetsIsOpenAnswer()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var result = await handler.Handle(new CreateQuestionCommand(
            "Яка столиця України?", null, topic.Id, DifficultyLevel.Easy, null,
            null, true, "Київ"), CancellationToken.None);

        result.IsOpenAnswer.Should().BeTrue();
        result.IsMultipleChoice.Should().BeFalse();
        result.CorrectAnswerText.Should().Be("Київ");
        result.AnswerOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateQuestion_OpenAnswer_WithCustomPoints()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var result = await handler.Handle(new CreateQuestionCommand(
            "Яка столиця?", null, topic.Id, DifficultyLevel.Medium, 5,
            null, true, "Київ"), CancellationToken.None);

        result.Points.Should().Be(5);
        result.IsOpenAnswer.Should().BeTrue();
    }

    [Fact]
    public async Task CreateQuestion_OpenAnswer_NoCorrectText_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var act = () => handler.Handle(new CreateQuestionCommand(
            "Питання?", null, topic.Id, DifficultyLevel.Easy, null,
            null, true, null), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*правильну відповідь*");
    }

    [Fact]
    public async Task CreateQuestion_OpenAnswer_EmptyCorrectText_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var act = () => handler.Handle(new CreateQuestionCommand(
            "Питання?", null, topic.Id, DifficultyLevel.Easy, null,
            null, true, "   "), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*правильну відповідь*");
    }

    [Fact]
    public async Task CreateQuestion_NotOpenAnswer_NoAnswerOptions_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var act = () => handler.Handle(new CreateQuestionCommand(
            "Питання?", null, topic.Id, DifficultyLevel.Easy, null,
            null, false, null), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*правильна*");
    }

    // ===== Submit Open Answer =====

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_ExactMatch_IsCorrect()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Київ", 3);

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Київ"),
            CancellationToken.None);

        result.IsCorrect.Should().BeTrue();
        result.PointsAwarded.Should().Be(3);
    }

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_CaseInsensitive_IsCorrect()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "київ"),
            CancellationToken.None);

        result.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_WithWhitespace_IsCorrect()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "  Київ  "),
            CancellationToken.None);

        result.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_LatinAndCyrillicLookalikes_AreEquivalent()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(
            db,
            scenario.Topic.Id,
            DifficultyLevel.Easy,
            "\u0042");

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "\u0412"),
            CancellationToken.None);

        result.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_CaseSensitiveMode_RequiresExactCase()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(
            db,
            scenario.Topic.Id,
            DifficultyLevel.Easy,
            "\u0042");

        openQuestion.IgnoreCase = false;
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "\u0062"),
            CancellationToken.None);

        result.IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_SimilarLettersSensitiveMode_RequiresSameAlphabet()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(
            db,
            scenario.Topic.Id,
            DifficultyLevel.Easy,
            "\u0042");

        openQuestion.IgnoreSimilarLetters = false;
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "\u0412"),
            CancellationToken.None);

        result.IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_WrongText_IsIncorrect()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Київ", 2);

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Львів"),
            CancellationToken.None);

        result.IsCorrect.Should().BeFalse();
        result.PointsAwarded.Should().Be(0);
    }

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_EmptyText_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);
        var act = () => handler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, ""),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*ввести*");
    }

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_NullText_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);
        var act = () => handler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*ввести*");
    }

    // ===== GetNextQuestion for Open Answer =====

    [Fact]
    public async Task GetNextQuestion_OpenAnswer_ReturnsIsOpenAnswer()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Київ");

        var test = TestDbHelper.SeedTest(db, scenario.Group.Id, scenario.Teacher.Id, scenario.Topic.Id,
            new List<Guid> { openQuestion.Id }, "Open Answer Test");

        var session = new TestSession
        {
            Id = Guid.NewGuid(),
            StudentId = scenario.Student.Id,
            TopicModuleId = scenario.Topic.Id,
            TestId = test.Id,
            Status = TestSessionStatus.InProgress,
            CurrentDifficulty = DifficultyLevel.Medium,
            StartedAt = DateTime.UtcNow
        };
        db.TestSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = new GetNextQuestionHandler(db);
        var result = await handler.Handle(new GetNextQuestionQuery(session.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.IsOpenAnswer.Should().BeTrue();
        result.Options.Should().BeEmpty();
    }

    // ===== GetTestResult for Open Answer =====

    [Fact]
    public async Task GetTestResult_OpenAnswer_ReturnsTextAnswerAndCorrectText()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Київ");

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);

        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Львів"),
            CancellationToken.None);

        var resultHandler = new GetTestResultHandler(db);
        var result = await resultHandler.Handle(new GetTestResultQuery(session.Id), CancellationToken.None);

        var openAnswer = result.Answers.First(a => a.QuestionId == openQuestion.Id);
        openAnswer.IsOpenAnswer.Should().BeTrue();
        openAnswer.TextAnswer.Should().Be("Львів");
        openAnswer.CorrectAnswerText.Should().Be("Київ");
        openAnswer.IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task GetTestResult_OpenAnswer_CorrectAnswer_ShowsDetails()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);

        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Київ"),
            CancellationToken.None);

        var resultHandler = new GetTestResultHandler(db);
        var result = await resultHandler.Handle(new GetTestResultQuery(session.Id), CancellationToken.None);

        var openAnswer = result.Answers.First(a => a.QuestionId == openQuestion.Id);
        openAnswer.IsCorrect.Should().BeTrue();
        openAnswer.TextAnswer.Should().Be("Київ");
        openAnswer.CorrectAnswerText.Should().Be("Київ");
    }

    // ===== Update Question to/from Open Answer =====

    [Fact]
    public async Task UpdateQuestion_SwitchToOpenAnswer_RemovesOptions()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid questionId;

        using (var db1 = TestDbHelper.CreateDb(dbName))
        {
            var admin = TestDbHelper.SeedAdmin(db1);
            var subject = TestDbHelper.SeedSubject(db1);
            var topic = TestDbHelper.SeedTopic(db1, subject.Id, admin.Id);

            var createHandler = new CreateQuestionHandler(db1);
            var created = await createHandler.Handle(new CreateQuestionCommand(
                "Питання?", null, topic.Id, DifficultyLevel.Easy, null,
                new List<CreateAnswerOptionRequest>
                {
                    new("Правильна", true, 0),
                    new("Неправильна", false, 1)
                }), CancellationToken.None);

            created.IsOpenAnswer.Should().BeFalse();
            created.AnswerOptions.Should().HaveCount(2);
            questionId = created.Id;
        }

        using (var db2 = TestDbHelper.CreateDb(dbName))
        {
            var handler = new UpdateQuestionHandler(db2);
            var result = await handler.Handle(
                new UpdateQuestionCommand(questionId, null, null, null, null, null, null,
                    true, "Київ"),
                CancellationToken.None);

            result.IsOpenAnswer.Should().BeTrue();
            result.CorrectAnswerText.Should().Be("Київ");
            result.IsMultipleChoice.Should().BeFalse();
        }
    }

    // ===== Adaptive difficulty works with open answers =====

    [Fact]
    public async Task SubmitAnswer_OpenAnswer_AdaptsDifficulty()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var q1 = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Відповідь1");
        var q2 = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Відповідь2", text: "Друге відкрите?");

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = q1.Id, OrderIndex = 98 });
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = q2.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);

        // Two correct answers in a row should increase difficulty
        var r1 = await handler.Handle(
            new SubmitAnswerCommand(session.Id, q1.Id, null, "Відповідь1"),
            CancellationToken.None);
        r1.IsCorrect.Should().BeTrue();

        var r2 = await handler.Handle(
            new SubmitAnswerCommand(session.Id, q2.Id, null, "Відповідь2"),
            CancellationToken.None);
        r2.IsCorrect.Should().BeTrue();
        r2.NewDifficulty.Should().Be("Hard"); // Medium -> Hard after 2 correct
    }

    private static TestSession CreateSession(Data.Data.ApplicationDbContext db, TestDbHelper.FullScenario scenario)
    {
        var session = new TestSession
        {
            Id = Guid.NewGuid(),
            StudentId = scenario.Student.Id,
            TopicModuleId = scenario.Topic.Id,
            TestId = scenario.Test.Id,
            Status = TestSessionStatus.InProgress,
            CurrentDifficulty = DifficultyLevel.Medium,
            StartedAt = DateTime.UtcNow
        };
        db.TestSessions.Add(session);
        db.SaveChanges();
        return session;
    }
}
