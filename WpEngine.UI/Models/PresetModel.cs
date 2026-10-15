using WpEngine.UI.ViewModels;

namespace WpEngine.UI.Models;

/// <summary>
/// Модель сценария/пресета для быстрого заполнения редактора.
/// </summary>
public class PresetModel : ViewModelBase
{
    private bool _isSelected;

    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Description { get; init; }
    public string RealLifeAnalogy { get; init; } = string.Empty;
    public required string Code { get; init; }
    public required string PostConditionShort { get; init; }
    public required string PostConditionHuman { get; init; }
    public string ExpectedWp { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
