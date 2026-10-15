using WpEngine.UI.ViewModels;

namespace WpEngine.UI.Models;

/// <summary>
/// Модель отдельной строки в редакторе кода для нумерации (1, 2, 3...) и подсветки ошибок.
/// </summary>
public class CodeLineItem : ViewModelBase
{
    private bool _isError;
    private string _errorMessage = string.Empty;

    public int LineNumber { get; init; }

    public bool IsError
    {
        get => _isError;
        set => SetProperty(ref _isError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }
}
