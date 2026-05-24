using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;
using TestSystem.Services.Features.Tests;
using TestSystem.Services.Features.Testing;
using TestSystem.Services.DTOs.Questions;
using TestSystem.Services.Features.Questions;

namespace TestSystem.Tests;

public class MultiTopicTestTests
{
    [Fact]
    public async Task CreateTest_MultipleTopics_TopicNamesContainsAll()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var teacher = TestDbHelper.SeedTeacher(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic1 = TestDbHelper.SeedTopic(db, subject.Id, admin.Id, "Алгоритми");
        var topic2 = TestDbHelper.SeedTopic(db, subject.Id, admin.Id, "Структури даних");
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        var q1 = TestDbHelper.SeedQuestion(db, topic1.Id, DifficultyLevel.Easy, "Питання з алгоритмів");
        var q2 = TestDbHelper.SeedQuestion(db, topic2.Id, DifficultyLevel.Medium, "Питання зі структур");

        var handler = new CreateTestHandler(db);
        var result = await handler.Handle(new CreateTestCommand(
            "Комплексний тест", null, group.Id, null,
            new List<Guid> { q1.Id, q2.Id }, teacher.Id), CancellationToken.None);

        result.TopicModuleId.Should().BeNull();
        result.TopicNames.Should().HaveCount(2);
        result.TopicNames.Should().Contain("Алгоритми");
        result.TopicNames.Should().Contain("Структури даних");
        result.QuestionCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateTest_SingleTopic_TopicNamesContainsOne()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var teacher = TestDbHelper.SeedTeacher(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        var q1 = TestDbHelper.SeedQuestion(db, topic.Id, DifficultyLevel.Easy);
        var q2 = TestDbHelper.SeedQuestion(db, topic.Id, DifficultyLevel.Medium, "Друге питання");

        var handler = new CreateTestHandler(db);
        var result = await handler.Handle(new CreateTestCommand(
            "Тест", null, group.Id, topic.Id,
            new List<Guid> { q1.Id, q2.Id }, teacher.Id), CancellationToken.None);

        result.TopicModuleId.Should().Be(topic.Id);
        result.TopicNames.Should().HaveCount(1);
        result.TopicNames.Should().Contain("Алгоритми");
    }

    [Fact]
    public async Task GetTestsByGroup_MultiTopic_ReturnsTopicNames()
    {
        using var db = TestDbHelper.CreateDb();
        var admin = TestDbHelper.SeedAdmin(db);
        var teacher = TestDbHelper.SeedTeacher(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic1 = TestDbHelper.SeedTopic(db, subject.Id, admin.Id, "Тема А");
        var topic2 = TestDbHelper.SeedTopic(db, subject.Id, admin.Id, "Тема Б");
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        var q1 = TestDbHelper.SeedQuestion(db, topic1.Id, DifficultyLevel.Easy);
        var q2 = TestDbHelper.SeedQuestion(db, topic2.Id, DifficultyLevel.Easy, "Друге");

        TestDbHelper.SeedTest(db, group.Id, teacher.Id, null, new List<Guid> { q1.Id, q2.Id }, "Мікс тест");

        var handler = new GetTestsByGroupHandler(db);
        var result = await handler.Handle(new GetTestsByGroupQuery(group.Id), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].TopicNames.Should().HaveCount(2);
        result[0].TopicName.Should().BeNull();
    }

    [Fact]
    public async Task StartTest_MultiTopic_SessionTopicModuleIdIsNull()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        // Create second topic with questions
        var topic2 = TestDbHelper.SeedTopic(db, scenario.Subject.Id, scenario.Admin.Id, "Друга тема");
        var q2 = TestDbHelper.SeedQuestion(db, topic2.Id, DifficultyLevel.Easy, "Питання з другої теми");

        // Create multi-topic test
        var multiTest = TestDbHelper.SeedTest(db, scenario.Group.Id, scenario.Teacher.Id, null,
            new List<Guid> { scenario.Questions.First().Id, q2.Id }, "Комплексний");

        var handler = new StartTestHandler(db);
        var result = await handler.Handle(
            new StartTestCommand(scenario.Student.Id, multiTest.Id), CancellationToken.None);

        result.TopicModuleId.Should().BeNull();
        result.TopicTitle.Should().Be("Комплексний тест");
    }

    [Fact]
    public async Task StartTest_SingleTopicNoExplicitTopic_SessionGetsTopicId()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        // Create test without explicit TopicModuleId, but all questions from same topic
        var sameTopicQuestions = scenario.Questions.Take(2).Select(q => q.Id).ToList();
        var test = TestDbHelper.SeedTest(db, scenario.Group.Id, scenario.Teacher.Id, null,
            sameTopicQuestions, "Одна тема без явного вказання");

        var handler = new StartTestHandler(db);
        var result = await handler.Handle(
            new StartTestCommand(scenario.Student.Id, test.Id), CancellationToken.None);

        // Should auto-detect that all questions are from same topic
        result.TopicModuleId.Should().Be(scenario.Topic.Id);
    }

    [Fact]
    public async Task GetNextQuestion_MultiTopicTest_ReturnsQuestionsFromDifferentTopics()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var topic2 = TestDbHelper.SeedTopic(db, scenario.Subject.Id, scenario.Admin.Id, "Друга тема");
        var q1 = TestDbHelper.SeedQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "З першої теми");
        var q2 = TestDbHelper.SeedQuestion(db, topic2.Id, DifficultyLevel.Medium, "З другої теми");

        var test = TestDbHelper.SeedTest(db, scenario.Group.Id, scenario.Teacher.Id, null,
            new List<Guid> { q1.Id, q2.Id }, "Мікс");

        var session = new TestSession
        {
            Id = Guid.NewGuid(),
            StudentId = scenario.Student.Id,
            TopicModuleId = null,
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
        result!.MaxQuestions.Should().Be(2);
    }

    [Fact]
    public async Task FullFlow_MultiTopicTest_SubmitAndFinish()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var topic2 = TestDbHelper.SeedTopic(db, scenario.Subject.Id, scenario.Admin.Id, "Друга тема");

        // Create questions from two topics
        var questions = new List<Question>();
        for (int i = 0; i < 3; i++)
            questions.Add(TestDbHelper.SeedQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, $"Тема1 Q{i}"));
        for (int i = 0; i < 3; i++)
            questions.Add(TestDbHelper.SeedQuestion(db, topic2.Id, DifficultyLevel.Medium, $"Тема2 Q{i}"));

        var test = TestDbHelper.SeedTest(db, scenario.Group.Id, scenario.Teacher.Id, null,
            questions.Select(q => q.Id).ToList(), "Комплексний тест");

        // Start
        var startHandler = new StartTestHandler(db);
        var session = await startHandler.Handle(
            new StartTestCommand(scenario.Student.Id, test.Id), CancellationToken.None);

        session.TopicModuleId.Should().BeNull();

        // Answer 5 questions to be able to finish
        var submitHandler = new SubmitAnswerHandler(db);
        var nextHandler = new GetNextQuestionHandler(db);

        for (int i = 0; i < 5; i++)
        {
            var question = await nextHandler.Handle(new GetNextQuestionQuery(session.Id), CancellationToken.None);
            question.Should().NotBeNull();

            var dbQ = await db.Questions.Include(q => q.AnswerOptions)
                .FirstAsync(q => q.Id == question!.QuestionId);
            var correctId = dbQ.AnswerOptions.First(a => a.IsCorrect).Id;

            await submitHandler.Handle(
                new SubmitAnswerCommand(session.Id, question!.QuestionId, new List<Guid> { correctId }),
                CancellationToken.None);
        }

        // Finish
        var finishHandler = new FinishTestHandler(db);
        var finished = await finishHandler.Handle(
            new FinishTestCommand(session.Id, scenario.Student.Id), CancellationToken.None);

        finished.Status.Should().Be("Completed");
        finished.TotalQuestions.Should().Be(5);
        finished.CorrectAnswers.Should().Be(5);

        // Check result
        var resultHandler = new GetTestResultHandler(db);
        var result = await resultHandler.Handle(new GetTestResultQuery(session.Id), CancellationToken.None);

        result.Answers.Should().HaveCount(5);
        result.TopicTitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetTestResult_MultiTopic_ShowsAllTopicNames()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var topic2 = TestDbHelper.SeedTopic(db, scenario.Subject.Id, scenario.Admin.Id, "Структури даних");

        var q1 = TestDbHelper.SeedQuestion(db, scenario.Topic.Id, DifficultyLevel.Medium, "Q from topic1");
        var q2 = TestDbHelper.SeedQuestion(db, topic2.Id, DifficultyLevel.Medium, "Q from topic2");

        var test = TestDbHelper.SeedTest(db, scenario.Group.Id, scenario.Teacher.Id, null,
            new List<Guid> { q1.Id, q2.Id }, "Mix");

        var session = new TestSession
        {
            Id = Guid.NewGuid(),
            StudentId = scenario.Student.Id,
            TopicModuleId = null,
            TestId = test.Id,
            Status = TestSessionStatus.InProgress,
            CurrentDifficulty = DifficultyLevel.Medium,
            StartedAt = DateTime.UtcNow
        };
        db.TestSessions.Add(session);
        await db.SaveChangesAsync();

        // Answer both questions
        var submitHandler = new SubmitAnswerHandler(db);
        var correctId1 = q1.AnswerOptions.First(a => a.IsCorrect).Id;
        await submitHandler.Handle(new SubmitAnswerCommand(session.Id, q1.Id, new List<Guid> { correctId1 }), CancellationToken.None);
        var correctId2 = q2.AnswerOptions.First(a => a.IsCorrect).Id;
        await submitHandler.Handle(new SubmitAnswerCommand(session.Id, q2.Id, new List<Guid> { correctId2 }), CancellationToken.None);

        var resultHandler = new GetTestResultHandler(db);
        var result = await resultHandler.Handle(new GetTestResultQuery(session.Id), CancellationToken.None);

        // TopicTitle should contain both topics
        result.TopicTitle.Should().Contain("Алгоритми");
        result.TopicTitle.Should().Contain("Структури даних");
    }

    [Fact]
    public async Task GetAssignedTests_MultiTopic_ReturnsTopicNames()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var topic2 = TestDbHelper.SeedTopic(db, scenario.Subject.Id, scenario.Admin.Id, "Мережі");
        var q1 = TestDbHelper.SeedQuestion(db, scenario.Topic.Id, DifficultyLevel.Easy);
        var q2 = TestDbHelper.SeedQuestion(db, topic2.Id, DifficultyLevel.Easy, "Мережеве Q");

        TestDbHelper.SeedTest(db, scenario.Group.Id, scenario.Teacher.Id, null,
            new List<Guid> { q1.Id, q2.Id }, "Комплексний");

        var handler = new GetAssignedTestsByGroupHandler(db);
        var result = await handler.Handle(
            new GetAssignedTestsByGroupQuery(scenario.Group.Id, scenario.Student.Id), CancellationToken.None);

        var multiTest = result.First(t => t.Title == "Комплексний");
        multiTest.TopicNames.Should().HaveCount(2);
        multiTest.TopicName.Should().BeNull();
    }
}
