using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Questions;
using TestSystem.Services.Features.Questions;
using TestSystem.Services.Features.Testing;

namespace TestSystem.Tests;

public class CustomPointsAndMultiChoiceTests
{
    // ===== Custom Points =====

    [Fact]
    public async Task CreateQuestion_CustomPoints_OverridesDefault()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var result = await handler.Handle(new CreateQuestionCommand(
            "Питання?", null, topic.Id, DifficultyLevel.Easy, 5,
            new List<CreateAnswerOptionRequest>
            {
                new("Так", true, 0),
                new("Ні", false, 1)
            }), CancellationToken.None);

        result.Points.Should().Be(5);
    }

    [Fact]
    public async Task CreateQuestion_NullPoints_UsesDefault()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var result = await handler.Handle(new CreateQuestionCommand(
            "Питання?", null, topic.Id, DifficultyLevel.Hard, null,
            new List<CreateAnswerOptionRequest>
            {
                new("Так", true, 0),
                new("Ні", false, 1)
            }), CancellationToken.None);

        result.Points.Should().Be(3); // Hard default
    }

    [Fact]
    public async Task CreateQuestion_ZeroPoints_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var act = () => handler.Handle(new CreateQuestionCommand(
            "Питання?", null, topic.Id, DifficultyLevel.Easy, 0,
            new List<CreateAnswerOptionRequest>
            {
                new("Так", true, 0),
                new("Ні", false, 1)
            }), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*бал*");
    }

    [Fact]
    public async Task UpdateQuestion_CustomPoints_OverridesDefault()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);
        var question = TestDbHelper.SeedQuestion(db, topic.Id, DifficultyLevel.Easy);

        var handler = new UpdateQuestionHandler(db);
        var result = await handler.Handle(
            new UpdateQuestionCommand(question.Id, null, null, null, 10, null, null),
            CancellationToken.None);

        result.Points.Should().Be(10);
    }

    [Fact]
    public async Task UpdateQuestion_ChangeDifficultyWithCustomPoints_KeepsCustomPoints()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);
        var question = TestDbHelper.SeedQuestion(db, topic.Id, DifficultyLevel.Easy);

        var handler = new UpdateQuestionHandler(db);
        var result = await handler.Handle(
            new UpdateQuestionCommand(question.Id, null, null, DifficultyLevel.Hard, 7, null, null),
            CancellationToken.None);

        result.DifficultyLevel.Should().Be("Hard");
        result.Points.Should().Be(7);
    }

    [Fact]
    public async Task UpdateQuestion_ChangeDifficultyWithoutPoints_AutoSetsPoints()
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
        result.Points.Should().Be(3); // auto from Hard
    }

    [Fact]
    public async Task SubmitAnswer_CustomPoints_AwardsCorrectPoints()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        // Update a question to have custom points
        var question = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Medium);
        question.Points = 10;
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var correctOption = question.AnswerOptions.First(a => a.IsCorrect);

        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { correctOption.Id }),
            CancellationToken.None);

        result.IsCorrect.Should().BeTrue();
        result.PointsAwarded.Should().Be(10);
        result.TotalScore.Should().Be(10);
    }

    // ===== Multiple Choice =====

    [Fact]
    public async Task CreateQuestion_MultipleCorrectAnswers_SetsIsMultipleChoice()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var result = await handler.Handle(new CreateQuestionCommand(
            "Оберіть правильні?", null, topic.Id, DifficultyLevel.Medium, null,
            new List<CreateAnswerOptionRequest>
            {
                new("Правильна 1", true, 0),
                new("Правильна 2", true, 1),
                new("Неправильна", false, 2)
            }), CancellationToken.None);

        result.IsMultipleChoice.Should().BeTrue();
    }

    [Fact]
    public async Task CreateQuestion_SingleCorrectAnswer_NotMultipleChoice()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);

        var handler = new CreateQuestionHandler(db);
        var result = await handler.Handle(new CreateQuestionCommand(
            "Оберіть?", null, topic.Id, DifficultyLevel.Easy, null,
            new List<CreateAnswerOptionRequest>
            {
                new("Правильна", true, 0),
                new("Неправильна", false, 1)
            }), CancellationToken.None);

        result.IsMultipleChoice.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitAnswer_MultipleChoice_AllCorrect_IsCorrect()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var mcQuestion = TestDbHelper.SeedMultipleChoiceQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, 5);

        // Add to test
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = mcQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var correctIds = mcQuestion.AnswerOptions.Where(a => a.IsCorrect).Select(a => a.Id).ToList();

        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, mcQuestion.Id, correctIds),
            CancellationToken.None);

        result.IsCorrect.Should().BeTrue();
        result.PointsAwarded.Should().Be(5);
    }

    [Fact]
    public async Task SubmitAnswer_MultipleChoice_PartialSelection_IsIncorrect()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var mcQuestion = TestDbHelper.SeedMultipleChoiceQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, 5);

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = mcQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        // Select only one of two correct answers
        var oneCorrectId = mcQuestion.AnswerOptions.First(a => a.IsCorrect).Id;

        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, mcQuestion.Id, new List<Guid> { oneCorrectId }),
            CancellationToken.None);

        result.IsCorrect.Should().BeFalse();
        result.PointsAwarded.Should().Be(0);
    }

    [Fact]
    public async Task SubmitAnswer_MultipleChoice_ExtraWrongSelection_IsIncorrect()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var mcQuestion = TestDbHelper.SeedMultipleChoiceQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, 5);

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = mcQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        // Select all correct + one wrong
        var allCorrectIds = mcQuestion.AnswerOptions.Where(a => a.IsCorrect).Select(a => a.Id).ToList();
        var oneWrongId = mcQuestion.AnswerOptions.First(a => !a.IsCorrect).Id;
        allCorrectIds.Add(oneWrongId);

        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, mcQuestion.Id, allCorrectIds),
            CancellationToken.None);

        result.IsCorrect.Should().BeFalse();
        result.PointsAwarded.Should().Be(0);
    }

    [Fact]
    public async Task SubmitAnswer_InvalidOptionId_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        var question = scenario.Questions.First();

        var handler = new SubmitAnswerHandler(db);
        var act = () => handler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { Guid.NewGuid() }),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*варіант*");
    }

    [Fact]
    public async Task SubmitAnswer_EmptySelection_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        var question = scenario.Questions.First();

        var handler = new SubmitAnswerHandler(db);
        var act = () => handler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid>()),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*обрати*");
    }

    [Fact]
    public async Task GetNextQuestion_ReturnsIsMultipleChoice()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var mcQuestion = TestDbHelper.SeedMultipleChoiceQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, 5);

        // Create a test with only the MC question
        var test = TestDbHelper.SeedTest(db, scenario.Group.Id, scenario.Teacher.Id, scenario.Topic.Id,
            new List<Guid> { mcQuestion.Id }, "MC Test");

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
        result!.IsMultipleChoice.Should().BeTrue();
    }

    [Fact]
    public async Task GetTestResult_MultipleChoice_ReturnsAllSelectedOptions()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var mcQuestion = TestDbHelper.SeedMultipleChoiceQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, 5);

        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = mcQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var correctIds = mcQuestion.AnswerOptions.Where(a => a.IsCorrect).Select(a => a.Id).ToList();

        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, mcQuestion.Id, correctIds),
            CancellationToken.None);

        var resultHandler = new GetTestResultHandler(db);
        var result = await resultHandler.Handle(new GetTestResultQuery(session.Id), CancellationToken.None);

        var mcAnswer = result.Answers.First(a => a.QuestionId == mcQuestion.Id);
        mcAnswer.IsCorrect.Should().BeTrue();
        mcAnswer.SelectedOptionIds.Should().HaveCount(2);
        mcAnswer.SelectedOptionTexts.Should().Contain("Правильна 1");
        mcAnswer.SelectedOptionTexts.Should().Contain("Правильна 2");
        mcAnswer.CorrectOptionTexts.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateQuestion_ChangeAnswersToMultipleCorrect_UpdatesIsMultipleChoice()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid questionId;

        // Create in one context
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

            created.IsMultipleChoice.Should().BeFalse();
            questionId = created.Id;
        }

        // Update in a fresh context
        using (var db2 = TestDbHelper.CreateDb(dbName))
        {
            var handler = new UpdateQuestionHandler(db2);
            var result = await handler.Handle(
                new UpdateQuestionCommand(questionId, null, null, null, null, null,
                    new List<UpdateAnswerOptionRequest>
                    {
                        new(null, "Правильна 1", true, 0),
                        new(null, "Правильна 2", true, 1),
                        new(null, "Неправильна", false, 2)
                    }),
                CancellationToken.None);

            result.IsMultipleChoice.Should().BeTrue();
        }
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
