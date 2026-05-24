namespace TestSystem.Services.Common.Exceptions;

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("Помилка валідації даних")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors) : this()
    {
        Errors = errors;
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string name, object key) : base($"{name} з ідентифікатором {key} не знайдено") { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "У вас немає дозволу на виконання цієї дії") : base(message) { }
}

public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}
