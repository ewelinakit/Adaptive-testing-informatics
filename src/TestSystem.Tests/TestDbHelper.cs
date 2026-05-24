using Microsoft.EntityFrameworkCore;
using TestSystem.Data.Data;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;

namespace TestSystem.Tests;

/// <summary>
/// Helper for creating in-memory DB and seeding test data.
/// </summary>
public static class TestDbHelper
{
    public static ApplicationDbContext CreateDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public static User SeedAdmin(ApplicationDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            FirstName = "Адмін",
            LastName = "Тестовий",
            Role = UserRole.Admin,
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static User SeedTeacher(ApplicationDbContext db, string email = "teacher@test.com")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Teacher123!"),
            FirstName = "Вчитель",
            LastName = "Тестовий",
            Role = UserRole.Teacher,
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static User SeedStudent(ApplicationDbContext db, string email = "student@test.com")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student123!"),
            FirstName = "Учень",
            LastName = "Тестовий",
            Role = UserRole.Student,
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static Subject SeedSubject(ApplicationDbContext db, string name = "Інформатика")
    {
        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Тестовий предмет"
        };
        db.Subjects.Add(subject);
        db.SaveChanges();
        return subject;
    }

    public static TopicModule SeedTopic(ApplicationDbContext db, Guid subjectId, Guid createdByUserId, string title = "Алгоритми")
    {
        var topic = new TopicModule
        {
            Id = Guid.NewGuid(),
            Title = title,
            SubjectId = subjectId,
            CreatedByUserId = createdByUserId,
            OrderIndex = 0
        };
        db.TopicModules.Add(topic);
        db.SaveChanges();
        return topic;
    }

    public static Group SeedGroup(ApplicationDbContext db, Guid teacherId, string name = "Група 11-А")
    {
        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Тестова група",
            InviteCode = Guid.NewGuid().ToString()[..8].ToUpper(),
            TeacherId = teacherId,
            IsActive = true
        };
        db.Groups.Add(group);
        db.SaveChanges();
        return group;
    }

    public static GroupMember SeedGroupMember(ApplicationDbContext db, Guid groupId, Guid studentId)
    {
        var member = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            StudentId = studentId,
            JoinedAt = DateTime.UtcNow
        };
        db.GroupMembers.Add(member);
        db.SaveChanges();
        return member;
    }

    public static Question SeedQuestion(ApplicationDbContext db, Guid topicId, DifficultyLevel difficulty, string text = "Тестове питання?")
    {
        var points = difficulty switch
        {
            DifficultyLevel.Easy => 1,
            DifficultyLevel.Medium => 2,
            DifficultyLevel.Hard => 3,
            _ => 1
        };

        var question = new Question
        {
            Id = Guid.NewGuid(),
            Text = text,
            TopicModuleId = topicId,
            DifficultyLevel = difficulty,
            Points = points,
            IsActive = true
        };

        var correctAnswer = new AnswerOption
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Text = "Правильна відповідь",
            IsCorrect = true,
            OrderIndex = 0
        };
        var wrongAnswer = new AnswerOption
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Text = "Неправильна відповідь",
            IsCorrect = false,
            OrderIndex = 1
        };

        question.AnswerOptions.Add(correctAnswer);
        question.AnswerOptions.Add(wrongAnswer);
        db.Questions.Add(question);
        db.SaveChanges();
        return question;
    }

    public static Test SeedTest(ApplicationDbContext db, Guid groupId, Guid createdByUserId, Guid? topicModuleId, List<Guid> questionIds, string title = "Тестовий тест")
    {
        var test = new Test
        {
            Id = Guid.NewGuid(),
            Title = title,
            GroupId = groupId,
            TopicModuleId = topicModuleId,
            CreatedByUserId = createdByUserId,
            IsActive = true
        };
        db.Tests.Add(test);

        for (int i = 0; i < questionIds.Count; i++)
        {
            db.TestQuestions.Add(new TestQuestion
            {
                TestId = test.Id,
                QuestionId = questionIds[i],
                OrderIndex = i
            });
        }

        db.SaveChanges();
        return test;
    }

    public static Question SeedMultipleChoiceQuestion(ApplicationDbContext db, Guid topicId, DifficultyLevel difficulty, int points, string text = "Множинний вибір?")
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            Text = text,
            TopicModuleId = topicId,
            DifficultyLevel = difficulty,
            Points = points,
            IsMultipleChoice = true,
            IsActive = true
        };

        question.AnswerOptions.Add(new AnswerOption { Id = Guid.NewGuid(), Text = "Правильна 1", IsCorrect = true, OrderIndex = 0 });
        question.AnswerOptions.Add(new AnswerOption { Id = Guid.NewGuid(), Text = "Правильна 2", IsCorrect = true, OrderIndex = 1 });
        question.AnswerOptions.Add(new AnswerOption { Id = Guid.NewGuid(), Text = "Неправильна 1", IsCorrect = false, OrderIndex = 2 });
        question.AnswerOptions.Add(new AnswerOption { Id = Guid.NewGuid(), Text = "Неправильна 2", IsCorrect = false, OrderIndex = 3 });

        db.Questions.Add(question);
        db.SaveChanges();
        return question;
    }

    public static Question SeedOpenAnswerQuestion(ApplicationDbContext db, Guid topicId, DifficultyLevel difficulty, string correctAnswer, int? customPoints = null, string text = "Відкрите питання?")
    {
        var points = customPoints ?? difficulty switch
        {
            DifficultyLevel.Easy => 1,
            DifficultyLevel.Medium => 2,
            DifficultyLevel.Hard => 3,
            _ => 1
        };

        var question = new Question
        {
            Id = Guid.NewGuid(),
            Text = text,
            TopicModuleId = topicId,
            DifficultyLevel = difficulty,
            Points = points,
            IsOpenAnswer = true,
            CorrectAnswerText = correctAnswer,
            IsActive = true
        };

        db.Questions.Add(question);
        db.SaveChanges();
        return question;
    }

    /// <summary>
    /// Seeds a full scenario: subject, topic, questions of all 3 difficulties, group, student membership, test.
    /// </summary>
    public static FullScenario SeedFullScenario(ApplicationDbContext db, int questionsPerDifficulty = 3)
    {
        var admin = SeedAdmin(db);
        var teacher = SeedTeacher(db);
        var student = SeedStudent(db);
        var subject = SeedSubject(db);
        var topic = SeedTopic(db, subject.Id, admin.Id);
        var group = SeedGroup(db, teacher.Id);
        SeedGroupMember(db, group.Id, student.Id);

        var questions = new List<Question>();
        foreach (var diff in new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard })
        {
            for (int i = 0; i < questionsPerDifficulty; i++)
            {
                questions.Add(SeedQuestion(db, topic.Id, diff, $"Питання {diff} #{i + 1}"));
            }
        }

        var test = SeedTest(db, group.Id, teacher.Id, topic.Id, questions.Select(q => q.Id).ToList());

        return new FullScenario(admin, teacher, student, subject, topic, group, questions, test);
    }

    public record FullScenario(
        User Admin, User Teacher, User Student,
        Subject Subject, TopicModule Topic, Group Group,
        List<Question> Questions, Test Test);
}
