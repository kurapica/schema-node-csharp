using System.ComponentModel.DataAnnotations;

namespace SchemaNode.Example;

/// <summary>
/// The 
/// </summary>
public class ValidateObjectAttribute : ValidationAttribute
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="validationContext"></param>
    /// <returns></returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
            return ValidationResult.Success;
        List<ValidationResult> results = new();
        ValidationContext context = new(value, null, null);
        Validator.TryValidateObject(value, context, results, true);
        if (results.Count == 0)
            return ValidationResult.Success;
        CompositeValidationResult compositeResults = new($"Validation for {validationContext.DisplayName} failed!", new List<string>
        {
            validationContext.DisplayName
        });
        results.ForEach(compositeResults.AddResult);
        return compositeResults;
    }
}

/// <summary>
/// The composite validation result
/// </summary>
public class CompositeValidationResult : ValidationResult
{
    readonly List<ValidationResult> results = new();

    /// <summary>
    /// The validation result
    /// </summary>
    public IEnumerable<ValidationResult> Results
    {
        get
        {
            return results;
        }
    }

    /// <summary>
    /// Construct the validation result
    /// </summary>
    /// <param name="errorMessage"></param>
    public CompositeValidationResult(string errorMessage) : base(errorMessage)
    {
    }

    /// <summary>
    /// Construct the validation result
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <param name="memberNames"></param>
    public CompositeValidationResult(string errorMessage, IEnumerable<string> memberNames) : base(errorMessage, memberNames)
    {
    }

    /// <summary>
    /// Construct the validation result
    /// </summary>
    /// <param name="validationResult"></param>
    protected CompositeValidationResult(ValidationResult validationResult) : base(validationResult)
    {
    }

    /// <summary>
    /// Add a valiation result
    /// </summary>
    /// <param name="validationResult"></param>
    public void AddResult(ValidationResult validationResult)
    {
        results.Add(validationResult);
    }
}