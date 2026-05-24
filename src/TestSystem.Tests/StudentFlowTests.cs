using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.Features.Groups;
using TestSystem.Services.Features.Testing;
using TestSystem.Services.Features.Tests;

namespace TestSystem.Tests;

/// <summary>
/// Student flow: приєднання до групи, проходження тесту, перегляд результатів.
/// </summary>
public class StudentFlowTests
{
    // ===== Join Group =====

    [Fact]
    public async Task JoinGroup_ValidCode_JoinsGroup()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);
        var student = TestDbHelper.SeedStudent(db);

        var handler = new JoinGroupByCodeHandler(db);
        var result = await handler.Handle(
            new JoinGroupByCodeCommand(group.InviteCode, student.Id),
            CancellationToken.None);

        result.Name.Should().Be("Група 11-А");
        result.MemberCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task JoinGroup_InvalidCode_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var student = TestDbHelper.SeedStudent(db);

        var handler = new JoinGroupByCodeHandler(db);
        var act = () => handler.Handle(
            new JoinGroupByCodeCommand("INVALID1", student.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*не знайдена*");
    }

    [Fact]
    public async Task JoinGroup_AlreadyMember_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);
        var student = TestDbHelper.SeedStudent(db);
        TestDbHelper.SeedGroupMember(db, group.Id, student.Id);

        var handler = new JoinGroupByCodeHandler(db);
        var act = () => handler.Handle(
            new JoinGroupByCodeCommand(group.InviteCode, student.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*вже є учасником*");
    }

    [Fact]
    public async Task JoinGroup_InactiveGroup_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);
        group.IsActive = false;
        db.SaveChanges();
        var student = TestDbHelper.SeedStudent(db);

        var handler = new JoinGroupByCodeHandler(db);
        var act = () => handler.Handle(
            new JoinGroupByCodeCommand(group.InviteCode, student.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task LeaveGroup_AsMember_LeavesSuccessfully()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);
        var student = TestDbHelper.SeedStudent(db);
        TestDbHelper.SeedGroupMember(db, group.Id, student.Id);

        var handler = new LeaveGroupHandler(db);
        await handler.Handle(new LeaveGroupCommand(group.Id, student.Id), CancellationToken.None);

        db.GroupMembers.Any(gm => gm.StudentId == student.Id && gm.GroupId == group.Id).Should().BeFalse();
    }

    [Fact]
    public async Task LeaveGroup_NotMember_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);
        var student = TestDbHelper.SeedStudent(db);

        var handler = new LeaveGroupHandler(db);
        var act = () => handler.Handle(
            new LeaveGroupCommand(group.Id, student.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task GetMyGroupsAsStudent_ReturnsOnlyMyGroups()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var g1 = TestDbHelper.SeedGroup(db, teacher.Id, "Група 1");
        var g2 = TestDbHelper.SeedGroup(db, teacher.Id, "Група 2");
        TestDbHelper.SeedGroup(db, teacher.Id, "Група 3"); // not joined
        var student = TestDbHelper.SeedStudent(db);
        TestDbHelper.SeedGroupMember(db, g1.Id, student.Id);
        TestDbHelper.SeedGroupMember(db, g2.Id, student.Id);

        var handler = new GetMyGroupsAsStudentHandler(db);
        var result = await handler.Handle(
            new GetMyGroupsAsStudentQuery(student.Id), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    // ===== Assigned Tests =====

    [Fact]
    public async Task GetAssignedTests_ReturnActiveTestsFromMyGroups()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new GetAssignedTestsHandler(db);
        var result = await handler.Handle(
            new GetAssignedTestsQuery(scenario.Student.Id), CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Тестовий тест");
    }

    [Fact]
    public async Task GetAssignedTestsByGroup_AsMember_ReturnsList()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new GetAssignedTestsByGroupHandler(db);
        var result = await handler.Handle(
            new GetAssignedTestsByGroupQuery(scenario.Group.Id, scenario.Student.Id),
            CancellationToken.None);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAssignedTestsByGroup_NotMember_ThrowsForbidden()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var outsider = TestDbHelper.SeedStudent(db, "outsider@test.com");

        var handler = new GetAssignedTestsByGroupHandler(db);
        var act = () => handler.Handle(
            new GetAssignedTestsByGroupQuery(scenario.Group.Id, outsider.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ===== Start Test =====

    [Fact]
    public async Task StartTest_AsMember_CreatesSession()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new StartTestHandler(db);
        var result = await handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id),
            CancellationToken.None);

        result.Status.Should().Be("InProgress");
        result.CurrentDifficulty.Should().Be("Easy"); // starts at Medium
        result.TotalQuestions.Should().Be(0);
    }

    [Fact]
    public async Task StartTest_NotMember_ThrowsForbidden()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var outsider = TestDbHelper.SeedStudent(db, "outsider@test.com");

        var handler = new StartTestHandler(db);
        var act = () => handler.Handle(
            new StartTestCommand(outsider.Id, scenario.Test.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task StartTest_InactiveTest_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        scenario.Test.IsActive = false;
        db.SaveChanges();

        var handler = new StartTestHandler(db);
        var act = () => handler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ===== Get Next Question =====

    [Fact]
    public async Task GetNextQuestion_ActiveSession_ReturnsQuestion()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);

        var handler = new GetNextQuestionHandler(db);
        var result = await handler.Handle(
            new GetNextQuestionQuery(session.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Options.Should().HaveCountGreaterThan(0);
        result.QuestionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetNextQuestion_CompletedSession_ReturnsNull()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.Status = TestSessionStatus.Completed;
        db.SaveChanges();

        var handler = new GetNextQuestionHandler(db);
        var result = await handler.Handle(
            new GetNextQuestionQuery(session.Id), CancellationToken.None);

        result.Should().BeNull();
    }

    // ===== Full Test Flow =====

    [Fact]
    public async Task FullTestFlow_StartAnswerFinish()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        // 1. Start test
        var startHandler = new StartTestHandler(db);
        var sessionDto = await startHandler.Handle(
            new StartTestCommand(scenario.Student.Id, scenario.Test.Id),
            CancellationToken.None);

        var sessionId = sessionDto.Id;

        // 2. Get and answer 5 questions
        var nextHandler = new GetNextQuestionHandler(db);
        var submitHandler = new SubmitAnswerHandler(db);

        for (int i = 0; i < 5; i++)
        {
            var question = await nextHandler.Handle(new GetNextQuestionQuery(sessionId), CancellationToken.None);
            question.Should().NotBeNull();

            // Answer the first option (may or may not be correct)
            var answer = await submitHandler.Handle(
                new SubmitAnswerCommand(sessionId, question!.QuestionId, new List<Guid> { question.Options.First().Id }),
                CancellationToken.None);

            answer.QuestionNumber.Should().Be(i + 1);
        }

        // 3. Finish test
        var finishHandler = new FinishTestHandler(db);
        var result = await finishHandler.Handle(
            new FinishTestCommand(sessionId, scenario.Student.Id),
            CancellationToken.None);

        result.Status.Should().Be("Completed");
        result.TotalQuestions.Should().Be(5);
    }

    // ===== Test History =====

    [Fact]
    public async Task GetTestHistory_ReturnsCompletedSessions()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);
        session.Status = TestSessionStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;
        session.TotalQuestions = 5;
        session.TotalScore = 8;
        session.ScorePercentage = 80.0;
        db.SaveChanges();

        var handler = new GetTestHistoryHandler(db);
        var result = await handler.Handle(
            new GetTestHistoryQuery(scenario.Student.Id), CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().TotalScore.Should().Be(8);
    }

    // ===== Test Result =====

    [Fact]
    public async Task GetTestResult_WithAnswers_ReturnsDetails()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var session = CreateSession(db, scenario);

        // Submit one answer
        var q = scenario.Questions.First();
        var correctOption = q.AnswerOptions.First(a => a.IsCorrect);
        var submitHandler = new SubmitAnswerHandler(db);
        await submitHandler.Handle(
            new SubmitAnswerCommand(session.Id, q.Id, new List<Guid> { correctOption.Id }),
            CancellationToken.None);

        var handler = new GetTestResultHandler(db);
        var result = await handler.Handle(
            new GetTestResultQuery(session.Id), CancellationToken.None);

        result.Answers.Should().HaveCount(1);
        result.Answers.First().IsCorrect.Should().BeTrue();
        result.TopicTitle.Should().Be("Алгоритми");
    }

    [Fact]
    public async Task FullFlow_JoinGroupThenTakeTest()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var student = TestDbHelper.SeedStudent(db);
        var admin = TestDbHelper.SeedAdmin(db);
        var subject = TestDbHelper.SeedSubject(db);
        var topic = TestDbHelper.SeedTopic(db, subject.Id, admin.Id);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        // 1. Student joins group
        var joinHandler = new JoinGroupByCodeHandler(db);
        await joinHandler.Handle(
            new JoinGroupByCodeCommand(group.InviteCode, student.Id),
            CancellationToken.None);

        // 2. Teacher creates test with questions
        var questions = new List<Question>();
        foreach (var diff in new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard })
        {
            for (int i = 0; i < 3; i++)
                questions.Add(TestDbHelper.SeedQuestion(db, topic.Id, diff));
        }
        var test = TestDbHelper.SeedTest(db, group.Id, teacher.Id, topic.Id,
            questions.Select(q => q.Id).ToList());

        // 3. Student sees assigned tests
        var assignedHandler = new GetAssignedTestsHandler(db);
        var assigned = await assignedHandler.Handle(
            new GetAssignedTestsQuery(student.Id), CancellationToken.None);
        assigned.Should().HaveCount(1);

        // 4. Student starts test
        var startHandler = new StartTestHandler(db);
        var sessionDto = await startHandler.Handle(
            new StartTestCommand(student.Id, test.Id), CancellationToken.None);

        // 5. Answer 5 questions
        var nextHandler = new GetNextQuestionHandler(db);
        var submitHandler = new SubmitAnswerHandler(db);

        for (int i = 0; i < 5; i++)
        {
            var q = await nextHandler.Handle(new GetNextQuestionQuery(sessionDto.Id), CancellationToken.None);
            q.Should().NotBeNull();
            await submitHandler.Handle(
                new SubmitAnswerCommand(sessionDto.Id, q!.QuestionId, new List<Guid> { q.Options.First().Id }),
                CancellationToken.None);
        }

        // 6. Finish test
        var finishHandler = new FinishTestHandler(db);
        var finished = await finishHandler.Handle(
            new FinishTestCommand(sessionDto.Id, student.Id),
            CancellationToken.None);
        finished.Status.Should().Be("Completed");

        // 7. View history
        var historyHandler = new GetTestHistoryHandler(db);
        var history = await historyHandler.Handle(
            new GetTestHistoryQuery(student.Id), CancellationToken.None);
        history.Should().HaveCount(1);
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
