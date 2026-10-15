namespace WpEngine.UI.Models;

/// <summary>
/// Представление отдельного шага пошагового протокола вычисления wp.
/// </summary>
public class WpStepViewModel
{
    public int StepIndex { get; init; }
    public required string StepBadge { get; init; }
    public required string StatementCode { get; init; }
    public required string IntermediateCondition { get; init; }
    public string IntermediateConditionLatex { get; init; } = string.Empty;
    public required string Description { get; init; }
    public string RealLifeHint { get; init; } = string.Empty;
    public bool HasDivisionConstraint { get; init; }
}
