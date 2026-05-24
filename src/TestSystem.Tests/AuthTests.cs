using FluentAssertions;
using Moq;
using System.Security.Claims;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.Features.Auth;
using TestSystem.Services.Interfaces;

namespace TestSystem.Tests;

public class AuthTests
{
    private readonly Mock<ITokenService> _tokenService;

    public AuthTests()
    {
        _tokenService = new Mock<ITokenService>();
        _tokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("test-access-token");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("test-refresh-token");
    }

    [Fact]
    public async Task Register_NewUser_ReturnsAuthResponse()
    {
        using var db = TestDbHelper.CreateDb();
        var handler = new RegisterHandler(db, _tokenService.Object);

        var result = await handler.Handle(
            new RegisterCommand("new@test.com", "Pass123!", "Іван", "Тестовий", UserRole.Student),
            CancellationToken.None);

        result.AccessToken.Should().Be("test-access-token");
        result.RefreshToken.Should().Be("test-refresh-token");
        result.User.Email.Should().Be("new@test.com");
        result.User.Role.Should().Be("Student");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        TestDbHelper.SeedStudent(db, "existing@test.com");

        var handler = new RegisterHandler(db, _tokenService.Object);
        var act = () => handler.Handle(
            new RegisterCommand("existing@test.com", "Pass123!", "Іван", "Тестовий", UserRole.Student),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*email*існує*");
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        using var db = TestDbHelper.CreateDb();
        TestDbHelper.SeedStudent(db, "login@test.com"); // password: Student123!

        var handler = new LoginHandler(db, _tokenService.Object);
        var result = await handler.Handle(
            new LoginCommand("login@test.com", "Student123!"),
            CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.User.Email.Should().Be("login@test.com");
    }

    [Fact]
    public async Task Login_WrongPassword_Throws()
    {
        using var db = TestDbHelper.CreateDb();
        TestDbHelper.SeedStudent(db, "wrong@test.com");

        var handler = new LoginHandler(db, _tokenService.Object);
        var act = () => handler.Handle(
            new LoginCommand("wrong@test.com", "WrongPassword!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Невірний*");
    }

    [Fact]
    public async Task Login_NonExistentEmail_Throws()
    {
        using var db = TestDbHelper.CreateDb();

        var handler = new LoginHandler(db, _tokenService.Object);
        var act = () => handler.Handle(
            new LoginCommand("nobody@test.com", "Pass123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Невірний*");
    }

    [Fact]
    public async Task Login_InactiveAccount_ThrowsForbidden()
    {
        using var db = TestDbHelper.CreateDb();
        var user = TestDbHelper.SeedStudent(db, "inactive@test.com");
        user.IsActive = false;
        db.SaveChanges();

        var handler = new LoginHandler(db, _tokenService.Object);
        var act = () => handler.Handle(
            new LoginCommand("inactive@test.com", "Student123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*деактивовано*");
    }

    [Fact]
    public async Task GetCurrentUser_ExistingUser_ReturnsDto()
    {
        using var db = TestDbHelper.CreateDb();
        var user = TestDbHelper.SeedAdmin(db);

        var handler = new GetCurrentUserHandler(db);
        var result = await handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        result.Email.Should().Be("admin@test.com");
        result.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task GetCurrentUser_NonExistent_ThrowsNotFound()
    {
        using var db = TestDbHelper.CreateDb();

        var handler = new GetCurrentUserHandler(db);
        var act = () => handler.Handle(new GetCurrentUserQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RevokeToken_ExistingToken_Revokes()
    {
        using var db = TestDbHelper.CreateDb();
        var user = TestDbHelper.SeedStudent(db);
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "revoke-me",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        db.RefreshTokens.Add(token);
        db.SaveChanges();

        var handler = new RevokeTokenHandler(db);
        await handler.Handle(new RevokeTokenCommand("revoke-me"), CancellationToken.None);

        var updated = db.RefreshTokens.First(t => t.Token == "revoke-me");
        updated.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_TeacherRole_CreatesTeacher()
    {
        using var db = TestDbHelper.CreateDb();
        var handler = new RegisterHandler(db, _tokenService.Object);

        var result = await handler.Handle(
            new RegisterCommand("teacher@new.com", "Pass123!", "Марія", "Тестова", UserRole.Teacher),
            CancellationToken.None);

        result.User.Role.Should().Be("Teacher");
        result.User.FirstName.Should().Be("Марія");
    }
}
