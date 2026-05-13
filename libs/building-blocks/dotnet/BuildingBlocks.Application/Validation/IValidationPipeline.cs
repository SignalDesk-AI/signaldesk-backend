namespace BuildingBlocks.Application.Validation;

public interface IValidationPipeline<in TRequest>
{
    ValueTask<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken = default);
}
