namespace BuildingBlocks.Application.Validation;

public sealed record ValidationResult(IReadOnlyCollection<ValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;

    public static ValidationResult Success { get; } = new(Array.Empty<ValidationIssue>());

    public static ValidationResult Failure(params ValidationIssue[] issues)
    {
        return new ValidationResult(issues);
    }
}
