using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.Features.Testing;
using TestSystem.Services.Features.Tests;

namespace TestSystem.Tests;

/// <summary>
/// Tests for new features:
/// - Time limit & deadline
/// - Max attempts
/// - Shuffle questions/answers
/// - Schedule availability (AvailableFrom/AvailableTo)
/// - Auto-abandon inactive sessions
/// - ShowCorrectAnswers
/// - Auto-deactivate expired tests
/// - Update test with new fields
/// - Teacher review of open answers
/// </summary>
public class NewFeaturesTests
{
    // ===========================
    // Feature 1: Time Limit
    // ===========================

    [Fact]
    public async Task CreateTest_WithTimeLimit_SetsTimeLimitMinutes()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new CreateTestHandler(db);
        var result = await handler.Handle(new CreateTestCommand(
            "Тест з лімітом", null, scenario.Group.Id, scenario.Topic.Id,
            scenario.Questions.Select(q => q.Id).ToList(), scenario.Teacher.Id,
            TimeLimitMinutes: 30), CancellationToken.None);

        result.TimeLimitMinutes.Should().Be(30);
    }

    [Fact]
    public async Task StartTest_WithTimeLimit_SetsDeadline()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.TimeLimitMinutes = 15;
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var result = await handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        result.DeadlineAt.Should().NotBeNull();
        result.DeadlineAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartTest_WithoutTimeLimit_NoDeadline()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.TimeLimitMinutes = null;
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var result = await handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        result.DeadlineAt.Should().BeNull();
    }

    [Fact]
    public async Task SubmitAnswer_AfterDeadline_ThrowsAndCompletesSession()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.DeadlineAt = DateTime.UtcNow.AddMinutes(-1); // already expired
        await db.SaveChangesAsync();

        var question = scenario.Questions.First();
        var correctOption = question.AnswerOptions.First(a => a.IsCorrect);

        var handler = new SubmitAnswerHandler(db);
        var act = () => handler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { correctOption.Id }),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*час*вичерпано*");

        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.Should().Be(TestSessionStatus.Completed);
    }

    [Fact]
    public async Task GetNextQuestion_AfterDeadline_ReturnsNull_CompletesSession()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.DeadlineAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var handler = new GetNextQuestionHandler(db);
        var result = await handler.Handle(new GetNextQuestionQuery(session.Id), CancellationToken.None);

        result.Should().BeNull();
        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.Should().Be(TestSessionStatus.Completed);
        updated.CompletedAt.Should().NotBeNull();
    }

    // ===========================
    // Feature 2: Max Attempts
    // ===========================

    [Fact]
    public async Task StartTest_MaxAttemptsNotReached_Succeeds()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.MaxAttempts = 3;
        await db.SaveChangesAsync();

        // Create 1 completed session
        var oldSession = CreateSession(db, scenario);
        oldSession.Status = TestSessionStatus.Completed;
        oldSession.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var result = await handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        result.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task StartTest_MaxAttemptsReached_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.MaxAttempts = 1;
        await db.SaveChangesAsync();

        // Create 1 completed session — max reached
        var oldSession = CreateSession(db, scenario);
        oldSession.Status = TestSessionStatus.Completed;
        oldSession.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var act = () => handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*спроб*");
    }

    [Fact]
    public async Task StartTest_MaxAttemptsNull_UnlimitedAttempts()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.MaxAttempts = null;
        await db.SaveChangesAsync();

        // Create 5 completed sessions
        for (int i = 0; i < 5; i++)
        {
            var s = CreateSession(db, scenario, $"s{i}@test.com");
            s.StudentId = scenario.Student.Id;
            s.Status = TestSessionStatus.Completed;
            s.CompletedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var result = await handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        result.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task StartTest_InProgressSessionCountsTowardsAttempts()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.MaxAttempts = 1;
        await db.SaveChangesAsync();

        // Create 1 in-progress session (should count)
        CreateSession(db, scenario);

        var handler = new StartTestHandler(db);
        var act = () => handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*спроб*");
    }

    // ===========================
    // Feature 3: Shuffle
    // ===========================

    [Fact]
    public async Task CreateTest_WithShuffle_SetsBothFlags()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new CreateTestHandler(db);
        var result = await handler.Handle(new CreateTestCommand(
            "Тест з shuffle", null, scenario.Group.Id, scenario.Topic.Id,
            scenario.Questions.Select(q => q.Id).ToList(), scenario.Teacher.Id,
            ShuffleQuestions: true, ShuffleAnswers: true), CancellationToken.None);

        result.ShuffleQuestions.Should().BeTrue();
        result.ShuffleAnswers.Should().BeTrue();
    }

    [Fact]
    public async Task GetNextQuestion_NoShuffle_ReturnsInOrderIndex()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.ShuffleQuestions = false;
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var handler = new GetNextQuestionHandler(db);

        // Get first two questions and check they come in OrderIndex order
        var q1 = await handler.Handle(new GetNextQuestionQuery(session.Id), CancellationToken.None);
        q1.Should().NotBeNull();

        // Submit first question to get next
        var submitHandler = new SubmitAnswerHandler(db);
        var dbQ1 = await db.Questions.Include(q => q.AnswerOptions).FirstAsync(q => q.Id == q1!.QuestionId);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, q1!.QuestionId, new List<Guid> { dbQ1.AnswerOptions.First(a => a.IsCorrect).Id }),
            CancellationToken.None);

        var q2 = await handler.Handle(new GetNextQuestionQuery(session.Id), CancellationToken.None);
        q2.Should().NotBeNull();

        // The OrderIndex of q1 should be less than q2
        var orderQ1 = await db.TestQuestions.FirstAsync(tq => tq.TestId == scenario.Test.Id && tq.QuestionId == q1.QuestionId);
        var orderQ2 = await db.TestQuestions.FirstAsync(tq => tq.TestId == scenario.Test.Id && tq.QuestionId == q2!.QuestionId);
        orderQ1.OrderIndex.Should().BeLessThan(orderQ2.OrderIndex);
    }

    // ===========================
    // Feature 4: Schedule Availability
    // ===========================

    [Fact]
    public async Task CreateTest_WithSchedule_SetsAvailability()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var from = DateTime.UtcNow.AddDays(1);
        var to = DateTime.UtcNow.AddDays(7);

        var handler = new CreateTestHandler(db);
        var result = await handler.Handle(new CreateTestCommand(
            "Тест з графіком", null, scenario.Group.Id, scenario.Topic.Id,
            scenario.Questions.Select(q => q.Id).ToList(), scenario.Teacher.Id,
            AvailableFrom: from, AvailableTo: to), CancellationToken.None);

        result.AvailableFrom.Should().BeCloseTo(from, TimeSpan.FromSeconds(1));
        result.AvailableTo.Should().BeCloseTo(to, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StartTest_BeforeAvailableFrom_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.AvailableFrom = DateTime.UtcNow.AddHours(1); // not yet available
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var act = () => handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*не доступний*");
    }

    [Fact]
    public async Task StartTest_AfterAvailableTo_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.AvailableTo = DateTime.UtcNow.AddHours(-1); // already expired
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var act = () => handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*минув*");
    }

    [Fact]
    public async Task StartTest_WithinSchedule_Succeeds()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.AvailableFrom = DateTime.UtcNow.AddHours(-1);
        scenario.Test.AvailableTo = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var result = await handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        result.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task StartTest_NoSchedule_Succeeds()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.AvailableFrom = null;
        scenario.Test.AvailableTo = null;
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var result = await handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id), CancellationToken.None);

        result.Status.Should().Be("InProgress");
    }

    // ===========================
    // Feature 5: Auto-abandon inactive sessions
    // ===========================

    [Fact]
    public async Task GetNextQuestion_InactiveSession_Abandons()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.LastActivityAt = DateTime.UtcNow.AddHours(-3); // inactive > 2h
        await db.SaveChangesAsync();

        var handler = new GetNextQuestionHandler(db);
        var result = await handler.Handle(new GetNextQuestionQuery(session.Id), CancellationToken.None);

        result.Should().BeNull();
        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.Should().Be(TestSessionStatus.Abandoned);
    }

    [Fact]
    public async Task GetNextQuestion_RecentActivity_DoesNotAbandon()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.LastActivityAt = DateTime.UtcNow.AddMinutes(-30); // active
        await db.SaveChangesAsync();

        var handler = new GetNextQuestionHandler(db);
        var result = await handler.Handle(new GetNextQuestionQuery(session.Id), CancellationToken.None);

        result.Should().NotBeNull();
        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.Should().Be(TestSessionStatus.InProgress);
    }

    // ===========================
    // Feature: ShowCorrectAnswers
    // ===========================

    [Fact]
    public async Task CreateTest_ShowCorrectAnswers_DefaultTrue()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new CreateTestHandler(db);
        var result = await handler.Handle(new CreateTestCommand(
            "Тест", null, scenario.Group.Id, scenario.Topic.Id,
            scenario.Questions.Select(q => q.Id).ToList(), scenario.Teacher.Id),
            CancellationToken.None);

        result.ShowCorrectAnswers.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTest_ShowCorrectAnswersFalse_SetsFlag()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new CreateTestHandler(db);
        var result = await handler.Handle(new CreateTestCommand(
            "Тест без відповідей", null, scenario.Group.Id, scenario.Topic.Id,
            scenario.Questions.Select(q => q.Id).ToList(), scenario.Teacher.Id,
            ShowCorrectAnswers: false), CancellationToken.None);

        result.ShowCorrectAnswers.Should().BeFalse();
    }

    [Fact]
    public async Task GetTestResult_ShowCorrectAnswersTrue_ReturnsCorrectTexts()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.ShowCorrectAnswers = true;
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var question = scenario.Questions.First();
        var wrongOption = question.AnswerOptions.First(a => !a.IsCorrect);

        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { wrongOption.Id }),
            CancellationToken.None);

        var resultHandler = new GetTestResultHandler(db);
        var result = await resultHandler.Handle(new GetTestResultQuery(session.Id), CancellationToken.None);

        result.ShowCorrectAnswers.Should().BeTrue();
        result.Answers.First().CorrectOptionTexts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTestResult_ShowCorrectAnswersFalse_HidesCorrectTexts()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.ShowCorrectAnswers = false;
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var question = scenario.Questions.First();
        var wrongOption = question.AnswerOptions.First(a => !a.IsCorrect);

        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { wrongOption.Id }),
            CancellationToken.None);

        var resultHandler = new GetTestResultHandler(db);
        var result = await resultHandler.Handle(new GetTestResultQuery(session.Id), CancellationToken.None);

        result.ShowCorrectAnswers.Should().BeFalse();
        result.Answers.First().CorrectOptionTexts.Should().BeEmpty();
        result.Answers.First().Explanation.Should().BeNull();
    }

    [Fact]
    public async Task GetTestResult_ShowCorrectAnswersFalse_HidesOpenAnswerCorrectText()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.ShowCorrectAnswers = false;
        await db.SaveChangesAsync();

        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");
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
        openAnswer.CorrectAnswerText.Should().BeNull();
    }

    // ===========================
    // Auto-deactivate expired tests
    // ===========================

    [Fact]
    public async Task GetTestsByGroup_ExpiredTest_AutoDeactivates()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.AvailableTo = DateTime.UtcNow.AddHours(-1); // expired
        scenario.Test.IsActive = true;
        await db.SaveChangesAsync();

        var handler = new GetTestsByGroupHandler(db);
        var result = await handler.Handle(
            new GetTestsByGroupQuery(scenario.Group.Id), CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().IsActive.Should().BeFalse(); // auto-deactivated
    }

    [Fact]
    public async Task GetTestsByGroup_NotExpiredTest_StaysActive()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.AvailableTo = DateTime.UtcNow.AddDays(7); // future
        scenario.Test.IsActive = true;
        await db.SaveChangesAsync();

        var handler = new GetTestsByGroupHandler(db);
        var result = await handler.Handle(
            new GetTestsByGroupQuery(scenario.Group.Id), CancellationToken.None);

        result.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetTestsByGroup_NoAvailableTo_StaysActive()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.AvailableTo = null;
        scenario.Test.IsActive = true;
        await db.SaveChangesAsync();

        var handler = new GetTestsByGroupHandler(db);
        var result = await handler.Handle(
            new GetTestsByGroupQuery(scenario.Group.Id), CancellationToken.None);

        result.First().IsActive.Should().BeTrue();
    }

    // ===========================
    // Update Test with new fields
    // ===========================

    [Fact]
    public async Task UpdateTest_SetTimeLimit_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, null, null, null,
                TimeLimitMinutes: 45), CancellationToken.None);

        result.TimeLimitMinutes.Should().Be(45);
    }

    [Fact]
    public async Task UpdateTest_ClearTimeLimit_SetsNull()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.TimeLimitMinutes = 30;
        await db.SaveChangesAsync();

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, null, null, null,
                ClearTimeLimitMinutes: true), CancellationToken.None);

        result.TimeLimitMinutes.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTest_SetMaxAttempts_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, null, null, null,
                MaxAttempts: 5), CancellationToken.None);

        result.MaxAttempts.Should().Be(5);
    }

    [Fact]
    public async Task UpdateTest_ClearMaxAttempts_SetsNull()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.MaxAttempts = 3;
        await db.SaveChangesAsync();

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, null, null, null,
                ClearMaxAttempts: true), CancellationToken.None);

        result.MaxAttempts.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTest_SetShuffle_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, null, null, null,
                ShuffleQuestions: true, ShuffleAnswers: true), CancellationToken.None);

        result.ShuffleQuestions.Should().BeTrue();
        result.ShuffleAnswers.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTest_SetShowCorrectAnswers_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, null, null, null,
                ShowCorrectAnswers: false), CancellationToken.None);

        result.ShowCorrectAnswers.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTest_SetSchedule_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var from = DateTime.UtcNow.AddDays(1);
        var to = DateTime.UtcNow.AddDays(7);

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, null, null, null,
                AvailableFrom: from, AvailableTo: to), CancellationToken.None);

        result.AvailableFrom.Should().BeCloseTo(from, TimeSpan.FromSeconds(1));
        result.AvailableTo.Should().BeCloseTo(to, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateTest_ClearSchedule_SetsNull()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.AvailableFrom = DateTime.UtcNow;
        scenario.Test.AvailableTo = DateTime.UtcNow.AddDays(1);
        await db.SaveChangesAsync();

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, null, null, null,
                ClearAvailableFrom: true, ClearAvailableTo: true), CancellationToken.None);

        result.AvailableFrom.Should().BeNull();
        result.AvailableTo.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTest_WrongTeacher_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var otherTeacher = TestDbHelper.SeedTeacher(db, "other@test.com");

        var handler = new UpdateTestHandler(db);
        var act = () => handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, otherTeacher.Id, "Хак", null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateTest_MultipleFieldsAtOnce_UpdatesAll()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id,
                "Оновлена назва", "Оновлений опис", false,
                TimeLimitMinutes: 60, MaxAttempts: 2,
                ShuffleQuestions: true, ShuffleAnswers: true,
                ShowCorrectAnswers: false), CancellationToken.None);

        result.Title.Should().Be("Оновлена назва");
        result.Description.Should().Be("Оновлений опис");
        result.IsActive.Should().BeFalse();
        result.TimeLimitMinutes.Should().Be(60);
        result.MaxAttempts.Should().Be(2);
        result.ShuffleQuestions.Should().BeTrue();
        result.ShuffleAnswers.Should().BeTrue();
        result.ShowCorrectAnswers.Should().BeFalse();
    }

    // ===========================
    // Feature 6: Teacher Review
    // ===========================

    [Fact]
    public async Task ReviewAnswer_Approve_UpdatesStatusAndPoints()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Київ", 3);
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Столиця"),
            CancellationToken.None);

        var answer = await db.TestSessionAnswers.FirstAsync(a => a.QuestionId == openQuestion.Id && a.TestSessionId == session.Id);

        var reviewHandler = new ReviewAnswerHandler(db);
        var result = await reviewHandler.Handle(
            new ReviewAnswerCommand(answer.Id, scenario.Teacher.Id, "Approved", "Зараховано", null),
            CancellationToken.None);

        result.Status.Should().Be("Approved");
        result.Feedback.Should().Be("Зараховано");
        result.PointsAwarded.Should().Be(3); // full points since approved without override
    }

    [Fact]
    public async Task ReviewAnswer_Reject_SetsZeroPoints()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Київ", 3);
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Невірно"),
            CancellationToken.None);

        var answer = await db.TestSessionAnswers.FirstAsync(a => a.QuestionId == openQuestion.Id && a.TestSessionId == session.Id);

        var reviewHandler = new ReviewAnswerHandler(db);
        var result = await reviewHandler.Handle(
            new ReviewAnswerCommand(answer.Id, scenario.Teacher.Id, "Rejected", "Не зараховано", null),
            CancellationToken.None);

        result.Status.Should().Be("Rejected");
        result.PointsAwarded.Should().Be(0);
    }

    [Fact]
    public async Task ReviewAnswer_OverridePoints_ClampsToMaxPoints()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Київ", 3);
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Частково"),
            CancellationToken.None);

        var answer = await db.TestSessionAnswers.FirstAsync(a => a.QuestionId == openQuestion.Id && a.TestSessionId == session.Id);

        var reviewHandler = new ReviewAnswerHandler(db);
        var result = await reviewHandler.Handle(
            new ReviewAnswerCommand(answer.Id, scenario.Teacher.Id, "Approved", null, 100), // try to give 100 points
            CancellationToken.None);

        result.PointsAwarded.Should().Be(3); // clamped to max (question.Points = 3)
    }

    [Fact]
    public async Task ReviewAnswer_OverridePointsPartial_SetsPartialPoints()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Київ", 3);
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Частково"),
            CancellationToken.None);

        var answer = await db.TestSessionAnswers.FirstAsync(a => a.QuestionId == openQuestion.Id && a.TestSessionId == session.Id);

        var reviewHandler = new ReviewAnswerHandler(db);
        var result = await reviewHandler.Handle(
            new ReviewAnswerCommand(answer.Id, scenario.Teacher.Id, "Approved", "Частково зараховано", 2),
            CancellationToken.None);

        result.PointsAwarded.Should().Be(2);
    }

    [Fact]
    public async Task ReviewAnswer_WrongTeacher_ThrowsForbidden()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var otherTeacher = TestDbHelper.SeedTeacher(db, "other@test.com");
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Відповідь"),
            CancellationToken.None);

        var answer = await db.TestSessionAnswers.FirstAsync(a => a.QuestionId == openQuestion.Id && a.TestSessionId == session.Id);

        var reviewHandler = new ReviewAnswerHandler(db);
        var act = () => reviewHandler.Handle(
            new ReviewAnswerCommand(answer.Id, otherTeacher.Id, "Approved", null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ReviewAnswer_InvalidStatus_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Відповідь"),
            CancellationToken.None);

        var answer = await db.TestSessionAnswers.FirstAsync(a => a.QuestionId == openQuestion.Id && a.TestSessionId == session.Id);

        var reviewHandler = new ReviewAnswerHandler(db);
        var act = () => reviewHandler.Handle(
            new ReviewAnswerCommand(answer.Id, scenario.Teacher.Id, "InvalidStatus", null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task ReviewAnswer_UpdatesSessionScore()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Київ", 3);
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Не Київ"),
            CancellationToken.None);

        var scoreBefore = (await db.TestSessions.FirstAsync(s => s.Id == session.Id)).TotalScore;

        var answer = await db.TestSessionAnswers.FirstAsync(a => a.QuestionId == openQuestion.Id && a.TestSessionId == session.Id);

        var reviewHandler = new ReviewAnswerHandler(db);
        await reviewHandler.Handle(
            new ReviewAnswerCommand(answer.Id, scenario.Teacher.Id, "Approved", null, 2),
            CancellationToken.None);

        var sessionAfter = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        sessionAfter.TotalScore.Should().Be(scoreBefore + 2);
    }

    [Fact]
    public async Task GetPendingReviews_ReturnsOnlyTeachersTests()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var otherTeacher = TestDbHelper.SeedTeacher(db, "other@test.com");
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Відповідь"),
            CancellationToken.None);

        // Teacher who owns the test should see pending reviews
        var handler = new GetPendingReviewsHandler(db);
        var result = await handler.Handle(
            new GetPendingReviewsQuery(scenario.Teacher.Id), CancellationToken.None);
        result.Should().HaveCountGreaterThanOrEqualTo(1);

        // Other teacher should NOT see them
        var otherResult = await handler.Handle(
            new GetPendingReviewsQuery(otherTeacher.Id), CancellationToken.None);
        otherResult.Should().BeEmpty();
    }

    // ===========================
    // Edge cases
    // ===========================

    [Fact]
    public async Task StartTest_NoQuestions_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var student = TestDbHelper.SeedStudent(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);
        TestDbHelper.SeedGroupMember(db, group.Id, student.Id);

        var test = new Test
        {
            Id = Guid.NewGuid(),
            Title = "Порожній тест",
            GroupId = group.Id,
            CreatedByUserId = teacher.Id,
            IsActive = true
        };
        db.Tests.Add(test);
        await db.SaveChangesAsync();

        var handler = new StartTestHandler(db);
        var act = () => handler.Handle(
            new StartTestCommand(student.Id, test.Id), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*немає питань*");
    }

    [Fact]
    public async Task SubmitAnswer_DuplicateQuestion_ThrowsOrHandles()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        var question = scenario.Questions.First();
        var correctOption = question.AnswerOptions.First(a => a.IsCorrect);

        var handler = new SubmitAnswerHandler(db);

        // First submit should work
        await handler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { correctOption.Id }),
            CancellationToken.None);

        // Second submit of same question should throw
        var act = () => handler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { correctOption.Id }),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task GetTestById_NonExistent_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();

        var handler = new GetTestByIdHandler(db);
        var act = () => handler.Handle(
            new GetTestByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ReviewAnswer_ApproveUpdatesCorrectCount()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Не Київ"),
            CancellationToken.None);

        var correctBefore = (await db.TestSessions.FirstAsync(s => s.Id == session.Id)).CorrectAnswers;
        var answer = await db.TestSessionAnswers.FirstAsync(a => a.QuestionId == openQuestion.Id && a.TestSessionId == session.Id);

        var reviewHandler = new ReviewAnswerHandler(db);
        await reviewHandler.Handle(
            new ReviewAnswerCommand(answer.Id, scenario.Teacher.Id, "Approved", null, null),
            CancellationToken.None);

        var sessionAfter = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        sessionAfter.CorrectAnswers.Should().Be(correctBefore + 1);
    }

    [Fact]
    public async Task ReviewAnswer_NegativePoints_ClampsToZero()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var openQuestion = TestDbHelper.SeedOpenAnswerQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy, "Київ");
        db.TestQuestions.Add(new TestQuestion { TestId = scenario.Test.Id, QuestionId = openQuestion.Id, OrderIndex = 99 });
        await db.SaveChangesAsync();

        var session = CreateSession(db, scenario);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, openQuestion.Id, null, "Відповідь"),
            CancellationToken.None);

        var answer = await db.TestSessionAnswers.FirstAsync(a => a.QuestionId == openQuestion.Id && a.TestSessionId == session.Id);

        var reviewHandler = new ReviewAnswerHandler(db);
        var result = await reviewHandler.Handle(
            new ReviewAnswerCommand(answer.Id, scenario.Teacher.Id, "Rejected", null, -5),
            CancellationToken.None);

        result.PointsAwarded.Should().Be(0);
    }

    // ===========================
    // Helpers
    // ===========================

    private static TestSession CreateSession(Data.Data.ApplicationDbContext db, TestDbHelper.FullScenario scenario, string? uniqueKey = null)
    {
        var session = new TestSession
        {
            Id = Guid.NewGuid(),
            StudentId = scenario.Student.Id,
            TopicModuleId = scenario.Topic.Id,
            TestId = scenario.Test.Id,
            Status = TestSessionStatus.InProgress,
            CurrentDifficulty = DifficultyLevel.Medium,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };
        db.TestSessions.Add(session);
        db.SaveChanges();
        return session;
    }
}
