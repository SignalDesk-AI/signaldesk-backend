namespace BuildingBlocks.Application.Validation;

public sealed record ValidationIssue(
    string Field,
    string Message,
    string Code = "VALIDATION_ERROR");
