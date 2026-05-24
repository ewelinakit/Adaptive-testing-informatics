using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;
using TestSystem.Services.Features.Testing;

namespace TestSystem.Tests;

/// <summary>
/// Тести адаптивного алгоритму:
/// - Правильна відповідь → ConsecutiveCorrect++
/// - Неправильна → ConsecutiveWrong++
/// - 2 правильні підряд → рівень складності ↑
/// - 2 неправильні підряд → рівень складності ↓
/// - Не вище Hard, не нижче Easy
/// - Автозавершення коли всі питання відповідно
/// </summary>
public class AdaptiveAlgorithmTests
{
    [Fact]
    public async Task SubmitAnswer_Correct_IncrementsConsecutiveCorrect()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        var question = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Medium);
        var correctOption = question.AnswerOptions.First(a => a.IsCorrect);

        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { correctOption.Id }), CancellationToken.None);

        result.IsCorrect.Should().BeTrue();
        result.PointsAwarded.Should().Be(question.Points);

        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.ConsecutiveCorrect.Should().Be(1);
        updated.ConsecutiveWrong.Should().Be(0);
        updated.CorrectAnswers.Should().Be(1);
        updated.TotalQuestions.Should().Be(1);
    }

    [Fact]
    public async Task SubmitAnswer_Wrong_IncrementsConsecutiveWrong()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        var question = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Medium);
        var wrongOption = question.AnswerOptions.First(a => !a.IsCorrect);

        var handler = new SubmitAnswerHandler(db);
        var result = await handler.Handle(
            new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { wrongOption.Id }), CancellationToken.None);

        result.IsCorrect.Should().BeFalse();
        result.PointsAwarded.Should().Be(0);

        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.ConsecutiveWrong.Should().Be(1);
        updated.ConsecutiveCorrect.Should().Be(0);
        updated.CorrectAnswers.Should().Be(0);
    }

    [Fact]
    public async Task TwoCorrectInRow_IncreaseDifficulty_FromMediumToHard()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.CurrentDifficulty = DifficultyLevel.Medium;
        await db.SaveChangesAsync();

        var handler = new SubmitAnswerHandler(db);

        // Answer 1 - correct
        var q1 = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Medium);
        var correct1 = q1.AnswerOptions.First(a => a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { correct1.Id }), CancellationToken.None);

        // Answer 2 - correct → should increase to Hard
        var q2 = scenario.Questions.Where(q => q.DifficultyLevel == DifficultyLevel.Medium).Skip(1).First();
        var correct2 = q2.AnswerOptions.First(a => a.IsCorrect);
        var result = await handler.Handle(new SubmitAnswerCommand(session.Id, q2.Id, new List<Guid> { correct2.Id }), CancellationToken.None);

        result.NewDifficulty.Should().Be(DifficultyLevel.Hard.ToString());
        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.CurrentDifficulty.Should().Be(DifficultyLevel.Hard);
        updated.ConsecutiveCorrect.Should().Be(0); // reset after level change
    }

    [Fact]
    public async Task TwoCorrectInRow_IncreaseDifficulty_FromEasyToMedium()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.CurrentDifficulty = DifficultyLevel.Easy;
        await db.SaveChangesAsync();

        var handler = new SubmitAnswerHandler(db);

        var q1 = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Easy);
        var correct1 = q1.AnswerOptions.First(a => a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { correct1.Id }), CancellationToken.None);

        var q2 = scenario.Questions.Where(q => q.DifficultyLevel == DifficultyLevel.Easy).Skip(1).First();
        var correct2 = q2.AnswerOptions.First(a => a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q2.Id, new List<Guid> { correct2.Id }), CancellationToken.None);

        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.CurrentDifficulty.Should().Be(DifficultyLevel.Medium);
    }

    [Fact]
    public async Task TwoWrongInRow_DecreaseDifficulty_FromMediumToEasy()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.CurrentDifficulty = DifficultyLevel.Medium;
        await db.SaveChangesAsync();

        var handler = new SubmitAnswerHandler(db);

        var q1 = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Medium);
        var wrong1 = q1.AnswerOptions.First(a => !a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { wrong1.Id }), CancellationToken.None);

        var q2 = scenario.Questions.Where(q => q.DifficultyLevel == DifficultyLevel.Medium).Skip(1).First();
        var wrong2 = q2.AnswerOptions.First(a => !a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q2.Id, new List<Guid> { wrong2.Id }), CancellationToken.None);

        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.CurrentDifficulty.Should().Be(DifficultyLevel.Easy);
        updated.ConsecutiveWrong.Should().Be(0); // reset after level change
    }

    [Fact]
    public async Task TwoWrongInRow_DecreaseDifficulty_FromHardToMedium()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.CurrentDifficulty = DifficultyLevel.Hard;
        await db.SaveChangesAsync();

        var handler = new SubmitAnswerHandler(db);

        var q1 = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Hard);
        var wrong1 = q1.AnswerOptions.First(a => !a.IsCorrect);
        var result = await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { wrong1.Id }), CancellationToken.None);

        result.NewDifficulty.Should().Be(DifficultyLevel.Medium.ToString());
        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.CurrentDifficulty.Should().Be(DifficultyLevel.Medium);
        updated.ConsecutiveWrong.Should().Be(0);
    }
    [Fact]
    public async Task OneWrong_FromMedium_StaysMedium()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.CurrentDifficulty = DifficultyLevel.Medium;
        await db.SaveChangesAsync();

        var handler = new SubmitAnswerHandler(db);

        var q1 = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Medium);
        var wrong1 = q1.AnswerOptions.First(a => !a.IsCorrect);
        var result = await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { wrong1.Id }), CancellationToken.None);

        result.NewDifficulty.Should().Be(DifficultyLevel.Medium.ToString());
        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.CurrentDifficulty.Should().Be(DifficultyLevel.Medium);
        updated.ConsecutiveWrong.Should().Be(1);
    }

    [Fact]
    public async Task GetNextQuestion_NoShuffle_ReturnsCurrentDifficultyBeforeOrderIndex()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.ShuffleQuestions = false;
        var session = CreateSession(db, scenario);
        session.CurrentDifficulty = DifficultyLevel.Medium;
        await db.SaveChangesAsync();

        var handler = new GetNextQuestionHandler(db);
        var result = await handler.Handle(new GetNextQuestionQuery(session.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.CurrentDifficulty.Should().Be(DifficultyLevel.Medium.ToString());
        scenario.Questions.First(q => q.Id == result.QuestionId).DifficultyLevel.Should().Be(DifficultyLevel.Medium);
    }

    [Fact]
    public async Task TwoCorrectAtHard_StaysAtHard()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.CurrentDifficulty = DifficultyLevel.Hard;
        await db.SaveChangesAsync();

        var handler = new SubmitAnswerHandler(db);

        var q1 = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Hard);
        var correct1 = q1.AnswerOptions.First(a => a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { correct1.Id }), CancellationToken.None);

        var q2 = scenario.Questions.Where(q => q.DifficultyLevel == DifficultyLevel.Hard).Skip(1).First();
        var correct2 = q2.AnswerOptions.First(a => a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q2.Id, new List<Guid> { correct2.Id }), CancellationToken.None);

        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.CurrentDifficulty.Should().Be(DifficultyLevel.Hard); // can't go higher
    }

    [Fact]
    public async Task TwoWrongAtEasy_StaysAtEasy()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.CurrentDifficulty = DifficultyLevel.Easy;
        await db.SaveChangesAsync();

        var handler = new SubmitAnswerHandler(db);

        var q1 = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Easy);
        var wrong1 = q1.AnswerOptions.First(a => !a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { wrong1.Id }), CancellationToken.None);

        var q2 = scenario.Questions.Where(q => q.DifficultyLevel == DifficultyLevel.Easy).Skip(1).First();
        var wrong2 = q2.AnswerOptions.First(a => !a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q2.Id, new List<Guid> { wrong2.Id }), CancellationToken.None);

        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.CurrentDifficulty.Should().Be(DifficultyLevel.Easy); // can't go lower
    }

    [Fact]
    public async Task CorrectThenWrong_ResetsConsecutiveCorrect()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);

        var handler = new SubmitAnswerHandler(db);

        // Correct answer
        var q1 = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Medium);
        var correct1 = q1.AnswerOptions.First(a => a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { correct1.Id }), CancellationToken.None);

        var afterCorrect = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        afterCorrect.ConsecutiveCorrect.Should().Be(1);

        // Wrong answer — resets consecutive correct
        var q2 = scenario.Questions.Where(q => q.DifficultyLevel == DifficultyLevel.Medium).Skip(1).First();
        var wrong2 = q2.AnswerOptions.First(a => !a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q2.Id, new List<Guid> { wrong2.Id }), CancellationToken.None);

        var afterWrong = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        afterWrong.ConsecutiveCorrect.Should().Be(0);
        afterWrong.ConsecutiveWrong.Should().Be(1);
    }

    [Fact]
    public async Task ScorePercentage_CalculatedCorrectly()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);

        var handler = new SubmitAnswerHandler(db);

        // Answer 1 correct (Medium, 2 points)
        var q1 = scenario.Questions.First(q => q.DifficultyLevel == DifficultyLevel.Medium);
        var correct1 = q1.AnswerOptions.First(a => a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { correct1.Id }), CancellationToken.None);

        // Answer 2 wrong (Medium, 0 of 2 points)
        var q2 = scenario.Questions.Where(q => q.DifficultyLevel == DifficultyLevel.Medium).Skip(1).First();
        var wrong2 = q2.AnswerOptions.First(a => !a.IsCorrect);
        await handler.Handle(new SubmitAnswerCommand(session.Id, q2.Id, new List<Guid> { wrong2.Id }), CancellationToken.None);

        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.TotalScore.Should().Be(2); // 2 points from correct medium
        updated.MaxPossibleScore.Should().Be(4); // 2 + 2
        updated.ScorePercentage.Should().Be(50.0);
    }

    [Fact]
    public async Task AutoFinish_WhenAllQuestionsAnswered()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db); // 9 questions (3 per difficulty)

        var session = CreateSession(db, scenario);
        var handler = new SubmitAnswerHandler(db);

        // Answer all 9 questions
        foreach (var question in scenario.Questions)
        {
            var correct = question.AnswerOptions.First(a => a.IsCorrect);
            var result = await handler.Handle(
                new SubmitAnswerCommand(session.Id, question.Id, new List<Guid> { correct.Id }), CancellationToken.None);

            if (question == scenario.Questions.Last())
            {
                result.IsFinished.Should().BeTrue();
            }
        }

        var updated = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        updated.Status.Should().Be(TestSessionStatus.Completed);
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FinishTest_ManuallyBefore5Questions_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);

        // Answer only 3 questions
        var handler = new SubmitAnswerHandler(db);
        for (int i = 0; i < 3; i++)
        {
            var q = scenario.Questions[i];
            var correct = q.AnswerOptions.First(a => a.IsCorrect);
            await handler.Handle(new SubmitAnswerCommand(session.Id, q.Id, new List<Guid> { correct.Id }), CancellationToken.None);
        }

        var finishHandler = new FinishTestHandler(db);
        var act = () => finishHandler.Handle(
            new FinishTestCommand(session.Id, scenario.Student.Id), CancellationToken.None);

        await act.Should().ThrowAsync<Services.Common.Exceptions.BadRequestException>()
            .WithMessage("*мінімум*5*");
    }

    [Fact]
    public async Task FinishTest_After5Questions_Succeeds()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);

        var submitHandler = new SubmitAnswerHandler(db);
        for (int i = 0; i < 5; i++)
        {
            var q = scenario.Questions[i];
            var correct = q.AnswerOptions.First(a => a.IsCorrect);
            await submitHandler.Handle(new SubmitAnswerCommand(session.Id, q.Id, new List<Guid> { correct.Id }), CancellationToken.None);
        }

        var finishHandler = new FinishTestHandler(db);
        var result = await finishHandler.Handle(
            new FinishTestCommand(session.Id, scenario.Student.Id), CancellationToken.None);

        result.Status.Should().Be(TestSessionStatus.Completed.ToString());
    }

    [Fact]
    public async Task SubmitAnswer_CompletedSession_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.Status = TestSessionStatus.Completed;
        await db.SaveChangesAsync();

        var handler = new SubmitAnswerHandler(db);
        var q = scenario.Questions.First();
        var correct = q.AnswerOptions.First(a => a.IsCorrect);

        var act = () => handler.Handle(
            new SubmitAnswerCommand(session.Id, q.Id, new List<Guid> { correct.Id }), CancellationToken.None);

        await act.Should().ThrowAsync<Services.Common.Exceptions.BadRequestException>();
    }

    [Fact]
    public async Task FullAdaptiveFlow_EasyToHardToMedium()
    {
        // Simulate: start at Medium, 2 correct → Hard, 2 wrong → Medium
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);

        var handler = new SubmitAnswerHandler(db);

        // 2 correct → Medium → Hard
        var q1 = scenario.Questions[3]; // Medium #1
        await handler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { q1.AnswerOptions.First(a => a.IsCorrect).Id }), CancellationToken.None);
        var q2 = scenario.Questions[4]; // Medium #2
        await handler.Handle(new SubmitAnswerCommand(session.Id, q2.Id, new List<Guid> { q2.AnswerOptions.First(a => a.IsCorrect).Id }), CancellationToken.None);

        var afterUp = await db.TestSessions.FirstAsync(s => s.Id == session.Id);
        afterUp.CurrentDifficulty.Should().Be(DifficultyLevel.Hard);

        // 2 wrong → Hard → Medium
        var q3 = scenario.Questions[6]; // Hard #1
        await handler.Handle(new SubmitAnswerCommand(session.Id, q3.Id, new List<Guid> { q3.AnswerOptions.First(a => !a.IsCorrect).Id }), CancellationToken.None);
        var afterDown = await db.TestSessions.FirstAsync(s => s.Id == session.Id);

        afterDown.CurrentDifficulty.Should().Be(DifficultyLevel.Medium);
    }

    // Helper to create a test session directly in DB
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
