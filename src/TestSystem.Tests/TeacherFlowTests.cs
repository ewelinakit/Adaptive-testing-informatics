using FluentAssertions;
using TestSystem.Models.Enums;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.Features.Groups;
using TestSystem.Services.Features.Tests;

namespace TestSystem.Tests;

/// <summary>
/// Teacher flow: групи, тести, учасники, результати.
/// </summary>
public class TeacherFlowTests
{
    // ===== Groups =====

    [Fact]
    public async Task CreateGroup_ValidTeacher_ReturnsDto()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);

        var handler = new CreateGroupHandler(db);
        var result = await handler.Handle(
            new CreateGroupCommand("Група 11-А", "Опис", teacher.Id),
            CancellationToken.None);

        result.Name.Should().Be("Група 11-А");
        result.InviteCode.Should().NotBeNullOrEmpty();
        result.InviteCode.Should().HaveLength(8);
        result.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMyGroups_ReturnsOnlyTeachersGroups()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher1 = TestDbHelper.SeedTeacher(db, "teacher1@test.com");
        var teacher2 = TestDbHelper.SeedTeacher(db, "teacher2@test.com");
        TestDbHelper.SeedGroup(db, teacher1.Id, "Група 1");
        TestDbHelper.SeedGroup(db, teacher1.Id, "Група 2");
        TestDbHelper.SeedGroup(db, teacher2.Id, "Чужа група");

        var handler = new GetMyGroupsHandler(db);
        var result = await handler.Handle(new GetMyGroupsQuery(teacher1.Id), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(g => g.TeacherName.Contains("Вчитель"));
    }

    [Fact]
    public async Task UpdateGroup_OwnerTeacher_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        var handler = new UpdateGroupHandler(db);
        var result = await handler.Handle(
            new UpdateGroupCommand(group.Id, teacher.Id, "Нова назва", null, null),
            CancellationToken.None);

        result.Name.Should().Be("Нова назва");
    }

    [Fact]
    public async Task UpdateGroup_WrongTeacher_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var otherTeacher = TestDbHelper.SeedTeacher(db, "other@test.com");
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        var handler = new UpdateGroupHandler(db);
        var act = () => handler.Handle(
            new UpdateGroupCommand(group.Id, otherTeacher.Id, "Хак", null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteGroup_OwnerTeacher_Deletes()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        var handler = new DeleteGroupHandler(db);
        await handler.Handle(new DeleteGroupCommand(group.Id, teacher.Id), CancellationToken.None);

        db.Groups.Any(g => g.Id == group.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteGroup_WrongTeacher_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var otherTeacher = TestDbHelper.SeedTeacher(db, "other@test.com");
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        var handler = new DeleteGroupHandler(db);
        var act = () => handler.Handle(
            new DeleteGroupCommand(group.Id, otherTeacher.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetGroupMembers_ReturnsList()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);
        var s1 = TestDbHelper.SeedStudent(db, "s1@test.com");
        var s2 = TestDbHelper.SeedStudent(db, "s2@test.com");
        TestDbHelper.SeedGroupMember(db, group.Id, s1.Id);
        TestDbHelper.SeedGroupMember(db, group.Id, s2.Id);

        var handler = new GetGroupMembersHandler(db);
        var result = await handler.Handle(new GetGroupMembersQuery(group.Id), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task RemoveGroupMember_OwnerTeacher_Removes()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);
        var student = TestDbHelper.SeedStudent(db);
        var member = TestDbHelper.SeedGroupMember(db, group.Id, student.Id);

        var handler = new RemoveGroupMemberHandler(db);
        await handler.Handle(
            new RemoveGroupMemberCommand(group.Id, member.Id, teacher.Id),
            CancellationToken.None);

        db.GroupMembers.Any(gm => gm.Id == member.Id).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveGroupMember_WrongTeacher_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var otherTeacher = TestDbHelper.SeedTeacher(db, "other@test.com");
        var group = TestDbHelper.SeedGroup(db, teacher.Id);
        var student = TestDbHelper.SeedStudent(db);
        var member = TestDbHelper.SeedGroupMember(db, group.Id, student.Id);

        var handler = new RemoveGroupMemberHandler(db);
        var act = () => handler.Handle(
            new RemoveGroupMemberCommand(group.Id, member.Id, otherTeacher.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ===== Tests =====

    [Fact]
    public async Task CreateTest_ValidData_ReturnsDto()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new CreateTestHandler(db);
        var result = await handler.Handle(
            new CreateTestCommand("Новий тест", "Опис", scenario.Group.Id,
                scenario.Topic.Id, scenario.Questions.Select(q => q.Id).ToList(),
                scenario.Teacher.Id),
            CancellationToken.None);

        result.Title.Should().Be("Новий тест");
        result.QuestionCount.Should().Be(9);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateTest_WrongTeacher_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var otherTeacher = TestDbHelper.SeedTeacher(db, "other@test.com");

        var handler = new CreateTestHandler(db);
        var act = () => handler.Handle(
            new CreateTestCommand("Тест", null, scenario.Group.Id,
                scenario.Topic.Id, scenario.Questions.Select(q => q.Id).ToList(),
                otherTeacher.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetTestsByGroup_ReturnsList()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new GetTestsByGroupHandler(db);
        var result = await handler.Handle(
            new GetTestsByGroupQuery(scenario.Group.Id), CancellationToken.None);

        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Тестовий тест");
    }

    [Fact]
    public async Task GetTestById_ReturnsDto()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new GetTestByIdHandler(db);
        var result = await handler.Handle(
            new GetTestByIdQuery(scenario.Test.Id), CancellationToken.None);

        result.Title.Should().Be("Тестовий тест");
        result.QuestionCount.Should().Be(9);
    }

    [Fact]
    public async Task UpdateTest_OwnerTeacher_Updates()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, "Оновлений тест", null, null),
            CancellationToken.None);

        result.Title.Should().Be("Оновлений тест");
    }

    [Fact]
    public async Task UpdateTest_DeactivateTest()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new UpdateTestHandler(db);
        var result = await handler.Handle(
            new UpdateTestCommand(scenario.Test.Id, scenario.Teacher.Id, null, null, false),
            CancellationToken.None);

        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTest_OwnerTeacher_Deletes()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);

        var handler = new DeleteTestHandler(db);
        await handler.Handle(
            new DeleteTestCommand(scenario.Test.Id, scenario.Teacher.Id),
            CancellationToken.None);

        db.Tests.Any(t => t.Id == scenario.Test.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTest_WrongTeacher_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();
        var scenario = TestDbHelper.SeedFullScenario(db);
        var otherTeacher = TestDbHelper.SeedTeacher(db, "other@test.com");

        var handler = new DeleteTestHandler(db);
        var act = () => handler.Handle(
            new DeleteTestCommand(scenario.Test.Id, otherTeacher.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateGroup_Deactivate_SetsInactive()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        var handler = new UpdateGroupHandler(db);
        var result = await handler.Handle(
            new UpdateGroupCommand(group.Id, teacher.Id, null, null, false),
            CancellationToken.None);

        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetGroupById_ReturnsDto()
    {
        using var db = TestDbHelper.CreateDb();
        var teacher = TestDbHelper.SeedTeacher(db);
        var group = TestDbHelper.SeedGroup(db, teacher.Id);

        var handler = new GetGroupByIdHandler(db);
        var result = await handler.Handle(new GetGroupByIdQuery(group.Id), CancellationToken.None);

        result.Name.Should().Be("Група 11-А");
    }

    [Fact]
    public async Task GetGroupById_NonExistent_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();

        var handler = new GetGroupByIdHandler(db);
        var act = () => handler.Handle(new GetGroupByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
