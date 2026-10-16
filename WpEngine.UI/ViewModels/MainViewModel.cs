using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpEngine.Core.AST;
using WpEngine.Core.Contracts;
using WpEngine.UI.Models;
using WpEngine.UI.Services;

namespace WpEngine.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly WpRunnerService _engine = new();

    private string _postConditionText = string.Empty;
    private string _postConditionHumanText = string.Empty;
    private string _programCodeText = string.Empty;
    private string _finalConditionShort = string.Empty;
    private string _finalConditionHuman = string.Empty;
    private string _finalConditionLatex = string.Empty;
    private string _hoareTriadText = string.Empty;
    private string _hoareTriadLatex = string.Empty;
    private string _postConditionLatex = string.Empty;
    private string _currentRealLifeAnalogy = string.Empty;
    private string _statusMessage = "Готов к расчёту wp";
    private string _elapsedTimeText = string.Empty;
    private string _errorHeaderText = string.Empty;
    private string _errorMessage = string.Empty;
    private string _errorLineSnippet = string.Empty;
    private int _errorLineNumber;
    private bool _hasErrorSnippet;
    private bool _hasResult;
    private bool _hasError;
    private bool _hasDivisionConstraint;
    private string _divisionConstraintMessage = string.Empty;
    private PresetModel? _selectedPreset;

    public ObservableCollection<PresetModel> Presets { get; } = new();
    public ObservableCollection<WpStepViewModel> TraceSteps { get; } = new();
    public ObservableCollection<CodeLineItem> EditorLines { get; } = new();

    public string PostConditionText
    {
        get => _postConditionText;
        set => SetProperty(ref _postConditionText, value);
    }
    public string PostConditionHumanText
    {
        get => _postConditionHumanText;
        set => SetProperty(ref _postConditionHumanText, value);
    }

    public string ProgramCodeText
    {
        get => _programCodeText;
        set
        {
            if (SetProperty(ref _programCodeText, value))
            {
                SyncEditorLines();
                if (_hasError && _errorLineNumber > 0)
                {
                    RevalidateCodeLive();
                }
            }
        }
    }

    public string FinalConditionShort
    {
        get => _finalConditionShort;
        set => SetProperty(ref _finalConditionShort, value);
    }

    public string FinalConditionLatex
    {
        get => _finalConditionLatex;
        set => SetProperty(ref _finalConditionLatex, value);
    }

    public string FinalConditionHuman
    {
        get => _finalConditionHuman;
        set => SetProperty(ref _finalConditionHuman, value);
    }

    public string HoareTriadText
    {
        get => _hoareTriadText;
        set => SetProperty(ref _hoareTriadText, value);
    }

    public string HoareTriadLatex
    {
        get => _hoareTriadLatex;
        set => SetProperty(ref _hoareTriadLatex, value);
    }

    public string PostConditionLatex
    {
        get => _postConditionLatex;
        set => SetProperty(ref _postConditionLatex, value);
    }

    public string CurrentRealLifeAnalogy
    {
        get => _currentRealLifeAnalogy;
        set => SetProperty(ref _currentRealLifeAnalogy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ElapsedTimeText
    {
        get => _elapsedTimeText;
        set => SetProperty(ref _elapsedTimeText, value);
    }

    public string ErrorHeaderText
    {
        get => _errorHeaderText;
        set => SetProperty(ref _errorHeaderText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string ErrorLineSnippet
    {
        get => _errorLineSnippet;
        set => SetProperty(ref _errorLineSnippet, value);
    }

    public int ErrorLineNumber
    {
        get => _errorLineNumber;
        set => SetProperty(ref _errorLineNumber, value);
    }

    public bool HasErrorSnippet
    {
        get => _hasErrorSnippet;
        set => SetProperty(ref _hasErrorSnippet, value);
    }

    public bool HasResult
    {
        get => _hasResult;
        set => SetProperty(ref _hasResult, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public bool HasDivisionConstraint
    {
        get => _hasDivisionConstraint;
        set => SetProperty(ref _hasDivisionConstraint, value);
    }

    public string DivisionConstraintMessage
    {
        get => _divisionConstraintMessage;
        set => SetProperty(ref _divisionConstraintMessage, value);
    }

    // --- Свойства вкладки «Конструктор фрагмента» ---

    public PresetModel? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                foreach (var p in Presets)
                {
                    p.IsSelected = ReferenceEquals(p, value);
                }

                if (value != null)
                {
                    ApplyPreset(value);
                }
            }
        }
    }

    // Commands
    public ICommand CalculateCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CopyTriadCommand { get; }
    public ICommand CopyWpCommand { get; }
    public ICommand CopyLatexCommand { get; }
    public ICommand InsertSnippetCommand { get; }
    public ICommand SelectPresetCommand { get; }
    public ICommand ExportReportCommand { get; }

    public MainViewModel()
    {
        CalculateCommand = new RelayCommand(ExecuteCalculate);
        ClearCommand = new RelayCommand(ExecuteClear);
        CopyTriadCommand = new RelayCommand(ExecuteCopyTriad);
        CopyWpCommand = new RelayCommand(ExecuteCopyWp);
        CopyLatexCommand = new RelayCommand(ExecuteCopyLatex);
        InsertSnippetCommand = new RelayCommand(ExecuteInsertSnippet);
        SelectPresetCommand = new RelayCommand(param =>
        {
            if (param is PresetModel preset)
            {
                SelectedPreset = preset;
                ExecuteCalculate();
            }
        });
        ExportReportCommand = new RelayCommand(ExecuteExportReport, _ => HasResult);

        SyncEditorLines();
        InitPresets();

        if (Presets.Count > 0)
        {
            SelectedPreset = Presets[0];
            ExecuteCalculate();
        }
    }

    private void SyncEditorLines()
    {
        int lineCount = 1;
        if (!string.IsNullOrEmpty(_programCodeText))
        {
            for (int i = 0; i < _programCodeText.Length; i++)
            {
                if (_programCodeText[i] == '\n')
                    lineCount++;
            }
        }

        while (EditorLines.Count < lineCount)
        {
            int nextNum = EditorLines.Count + 1;
            EditorLines.Add(new CodeLineItem
            {
                LineNumber = nextNum,
                IsError = nextNum == _errorLineNumber,
                ErrorMessage = nextNum == _errorLineNumber ? _errorMessage : string.Empty
            });
        }

        while (EditorLines.Count > lineCount)
        {
            EditorLines.RemoveAt(EditorLines.Count - 1);
        }
    }

    private void UpdateLineHighlights(int errorLine, string tooltipText)
    {
        ErrorLineNumber = errorLine;
        SyncEditorLines();
        foreach (var lineItem in EditorLines)
        {
            bool isErr = errorLine > 0 && lineItem.LineNumber == errorLine;
            lineItem.IsError = isErr;
            lineItem.ErrorMessage = isErr ? tooltipText : string.Empty;
        }
    }

    private void RevalidateCodeLive()
    {
        if (string.IsNullOrWhiteSpace(_programCodeText))
        {
            ClearErrorState();
            return;
        }

        try
        {
            _engine.ParseProgram(_programCodeText);
            ClearErrorState();
            StatusMessage = "Синтаксическая ошибка исправлена — нажмите «Рассчитать wp»";
        }
        catch (CodeParseException cex)
        {
            SetCodeError(cex.Line, cex.Column, cex.Message, cex.LineSnippet);
        }
        catch
        {
            // Игнорируем прочие исключения при живом вводе
        }
    }

    private void ClearErrorState()
    {
        HasError = false;
        ErrorHeaderText = string.Empty;
        ErrorMessage = string.Empty;
        ErrorLineSnippet = string.Empty;
        HasErrorSnippet = false;
        UpdateLineHighlights(0, string.Empty);
    }

    private void InitPresets()
    {
        Presets.Add(new PresetModel
        {
            Id = "max",
            Title = "1. Максимум из двух",
            Subtitle = "Ветвление if-else",
            Description = "Постусловие max > 100 проводится через условный оператор выбора максимума.",
            RealLifeAnalogy = "Покупатель применяет лучший из двух купонов на скидку (x1 или x2). Чтобы итоговая скидка max превысила 100 ₽, выбранный (наибольший) купон обязан быть больше 100 ₽.",
            Code = "if (x1 >= x2) {\n    max := x1;\n} else {\n    max := x2;\n}",
            PostConditionShort = "max > 100",
            PostConditionHuman = "Значение переменной max больше 100",
            ExpectedWp = "((x1 >= x2 && x1 > 100) || (!(x1 >= x2) && x2 > 100))"
        });

        Presets.Add(new PresetModel
        {
            Id = "quadratic",
            Title = "2. Квадратное ур-е",
            Subtitle = "Деление и условие определенности",
            Description = "Ветвление по дискриминанту D с делением на (2*a) и проверкой знаменателя != 0.",
            RealLifeAnalogy = "Бортовой компьютер считает тормозной путь через деление на 2*a (ускорение). Если a = 0, произойдёт сбой деления на ноль — поэтому wp заранее требует, чтобы 2*a ≠ 0.",
            Code = "if (D >= 0) {\n    x := (0 - b + d) / (2 * a);\n} else {\n    err := 1;\n}",
            PostConditionShort = "(x > 0) || (err == 1)",
            PostConditionHuman = "Корень x положителен при D >= 0, либо установлен флаг ошибки err = 1",
            ExpectedWp = "((D >= 0 && ((2 * a != 0) && ...)) || (!(D >= 0) && ...))"
        });

        Presets.Add(new PresetModel
        {
            Id = "sequence",
            Title = "3. Цепочка шагов",
            Subtitle = "Последовательность S1; S2",
            Description = "Постусловие проводится с конца через x := x + 10 и затем через y := x + 1.",
            RealLifeAnalogy = "Конвейер доставки: к весу товара x сначала добавляют тубус (+1 кг для y), а затем паллету (+10 кг к x). Чтобы итоговый вес превысил 15 кг, исходный товар должен весить строго больше 5 кг.",
            Code = "y := x + 1;\nx := x + 10;",
            PostConditionShort = "(y == x - 9) && (x > 15)",
            PostConditionHuman = "Переменная y равна (x - 9) и x больше 15",
            ExpectedWp = "x > 5"
        });

        Presets.Add(new PresetModel
        {
            Id = "cashback",
            Title = "4. Оплата и кэшбэк",
            Subtitle = "Жизненный пример: банковская карта",
            Description = "При покупке от 1000 ₽ начисляется мгновенный кэшбэк 50 ₽, иначе списывается полная сумма.",
            RealLifeAnalogy = "Оплата картой в супермаркете: если чек amount ≥ 1000 ₽, банк сразу возвращает 50 ₽ кэшбэка, поэтому на карте до оплаты достаточно иметь (amount - 50) ₽, чтобы не уйти в овердрафт.",
            Code = "if (amount >= 1000) {\n    balance := balance - amount + 50;\n} else {\n    balance := balance - amount;\n}",
            PostConditionShort = "balance >= 0",
            PostConditionHuman = "Баланс карты после покупки не ушёл в минус (без овердрафта)",
            ExpectedWp = "(amount >= 1000 && balance - amount + 50 >= 0) || (!(amount >= 1000) && balance - amount >= 0)"
        });

        Presets.Add(new PresetModel
        {
            Id = "speed",
            Title = "5. Средняя скорость",
            Subtitle = "Жизненный пример: камера на трассе",
            Description = "Расчёт средней скорости автомобиля v = dist / time с защитой от деления на ноль.",
            RealLifeAnalogy = "Дорожная камера замеряет среднюю скорость на участке: делит расстояние dist на время time. Чтобы расчёт был корректен и водитель не получил штраф, нужно time ≠ 0 и dist / time ≤ 90 км/ч.",
            Code = "speed := dist / time;",
            PostConditionShort = "(speed <= 90) && (speed > 0)",
            PostConditionHuman = "Средняя скорость автомобиля в пределах от 0 до 90 км/ч",
            ExpectedWp = "time != 0 && (dist / time <= 90 && dist / time > 0)"
        });
    }

    private void ApplyPreset(PresetModel preset)
    {
        ClearErrorState();
        ProgramCodeText = preset.Code;
        PostConditionText = preset.PostConditionShort;
        PostConditionHumanText = preset.PostConditionHuman;
        CurrentRealLifeAnalogy = preset.RealLifeAnalogy;
        StatusMessage = $"Выбран пример: {preset.Title}";
    }

    private void ExecuteCalculate()
    {
        ClearErrorState();

        if (string.IsNullOrWhiteSpace(PostConditionText))
        {
            SetGeneralError("Поле постусловия пусто", "Введите целевое постусловие R (например, x > 0).");
            return;
        }

        if (string.IsNullOrWhiteSpace(ProgramCodeText))
        {
            SetCodeError(1, 1, "Введите хотя бы один оператор программы (например, x := a + 1;).", string.Empty);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        WpEngine.Core.AST.Condition postAst;
        try
        {
            postAst = _engine.ParseCondition(PostConditionText);
        }
        catch (CodeParseException cex)
        {
            SetGeneralError($"Ошибка в постусловии R (символ {cex.Column})", cex.Message, cex.LineSnippet);
            return;
        }
        catch (Exception ex)
        {
            SetGeneralError("Ошибка в постусловии R", ex.Message);
            return;
        }

        Statement programAst;
        try
        {
            programAst = _engine.ParseProgram(ProgramCodeText);
        }
        catch (CodeParseException cex)
        {
            SetCodeError(cex.Line, cex.Column, cex.Message, cex.LineSnippet);
            return;
        }
        catch (Exception ex)
        {
            SetCodeError(1, 1, ex.Message, string.Empty);
            return;
        }

        try
        {
            WpResult result = _engine.CalculateWp(programAst, postAst);

            FinalConditionShort = result.FinalConditionShort;
            FinalConditionLatex = _engine.ToWrappedLatex(result.FinalCondition);
            PostConditionLatex = _engine.ToWrappedLatex(postAst);
            HoareTriadLatex = _engine.HoareTriadToLatex(result.FinalCondition, programAst, postAst);
            FinalConditionHuman = string.IsNullOrWhiteSpace(PostConditionHumanText)
                ? $"Словами: {result.FinalConditionHuman}"
                : $"Словами: {result.FinalConditionHuman} (цель: {FormatLowerFirst(PostConditionHumanText)})";

            HoareTriadText = result.HoareTriadText;

            TraceSteps.Clear();
            bool foundDiv = false;
            for (int i = 0; i < result.Trace.Count; i++)
            {
                var step = result.Trace[i];
                bool isDiv = step.Description.Contains("деления") || step.IntermediateCondition.Contains("≠") || step.IntermediateCondition.Contains("!= 0");
                if (isDiv) foundDiv = true;

                string stepLatex;
                try
                {
                    var parsedStepCond = _engine.ParseCondition(step.IntermediateCondition);
                    stepLatex = _engine.ToWrappedLatex(parsedStepCond);
                }
                catch
                {
                    stepLatex = _engine.WrapLatexString(step.IntermediateCondition);
                }

                string stepRealLifeHint = BuildStepRealLifeHint(step.StatementCode, isDiv);

                TraceSteps.Add(new WpStepViewModel
                {
                    StepIndex = i + 1,
                    StepBadge = $"Шаг {i + 1}",
                    StatementCode = step.StatementCode,
                    IntermediateCondition = step.IntermediateCondition,
                    IntermediateConditionLatex = stepLatex,
                    Description = step.Description,
                    RealLifeHint = stepRealLifeHint,
                    HasDivisionConstraint = isDiv
                });
            }

            if (_selectedPreset != null && ProgramCodeText.Trim() == _selectedPreset.Code.Trim())
            {
                CurrentRealLifeAnalogy = _selectedPreset.RealLifeAnalogy;
            }
            else
            {
                CurrentRealLifeAnalogy = BuildCustomRealLifeAnalogy(programAst, foundDiv);
            }

            HasDivisionConstraint = foundDiv;
            DivisionConstraintMessage = foundDiv
                ? "Условие определенности: в программе выполняется деление, поэтому в предусловие автоматически добавлена защита от деления на ноль (знаменатель ≠ 0)."
                : string.Empty;

            HasResult = true;
            stopwatch.Stop();
            ElapsedTimeText = $"{stopwatch.ElapsedMilliseconds} мс";
            StatusMessage = $"Расчёт выполнен (шагов: {result.Trace.Count}) • {stopwatch.ElapsedMilliseconds} мс";
        }
        catch (Exception ex)
        {
            SetGeneralError("Ошибка вычисления wp", ex.Message);
        }
    }

    private static string BuildStepRealLifeHint(string statementCode, bool isDiv)
    {
        if (statementCode.StartsWith("if "))
        {
            return "Развилка маршрута: как на навигаторе — независимо от того, по какой дороге (Then или Else) пойдёт выполнение, мы должны гарантированно приехать в целевую точку R.";
        }
        if (isDiv)
        {
            return "Деление с проверкой безопасности: как при разделении счёта в ресторане на несколько человек — сначала убеждаемся, что число участников не равно 0, и только потом подставляем формулу.";
        }
        return "Шаг назад во времени: мы знаем, каким должно быть значение после этого присваивания, поэтому заменяем переменную на её формулу и узнаём требование ДО выполнения строки.";
    }

    private static string BuildCustomRealLifeAnalogy(Statement programAst, bool foundDiv)
    {
        if (foundDiv)
        {
            return "Как расчёт средней скорости или деление бюджета: формула работает только тогда, когда делитель не равен нулю, а входные значения заранее укладываются в целевой коридор.";
        }
        if (programAst is IfStatement)
        {
            return "Как правило оплаты с условием (например, обычный тариф или скидка): движок проверяет обе ветки и выводит точные условия на входе для каждого сценария.";
        }
        return "Как обратное планирование ко времени отправления поезда: зная финальную цель, мы шаг за шагом отматываем действия назад и находим, с чего нужно начать.";
    }

    private static string FormatLowerFirst(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return char.ToLower(text[0]) + text[1..];
    }

    private void SetCodeError(int line, int col, string message, string snippet)
    {
        HasError = true;
        ErrorHeaderText = $"Ошибка в коде — строка {line}, символ {col}";
        ErrorMessage = message;
        ErrorLineSnippet = string.IsNullOrWhiteSpace(snippet) ? string.Empty : $"Строка {line}:  {snippet.Trim()}";
        HasErrorSnippet = !string.IsNullOrWhiteSpace(ErrorLineSnippet);
        UpdateLineHighlights(line, $"Строка {line}, символ {col}: {message}");
        StatusMessage = $"Ошибка синтаксиса в строке {line} (символ {col})";
    }

    private void SetGeneralError(string header, string message, string snippet = "")
    {
        HasError = true;
        ErrorHeaderText = header;
        ErrorMessage = message;
        ErrorLineSnippet = string.IsNullOrWhiteSpace(snippet) ? string.Empty : snippet.Trim();
        HasErrorSnippet = !string.IsNullOrWhiteSpace(ErrorLineSnippet);
        UpdateLineHighlights(0, string.Empty);
        StatusMessage = header;
    }

    private void ExecuteClear()
    {
        ClearErrorState();
        ProgramCodeText = string.Empty;
        PostConditionText = string.Empty;
        PostConditionHumanText = string.Empty;
        FinalConditionShort = string.Empty;
        FinalConditionHuman = string.Empty;
        HoareTriadText = string.Empty;
        CurrentRealLifeAnalogy = string.Empty;
        TraceSteps.Clear();
        HasResult = false;
        HasDivisionConstraint = false;
        SelectedPreset = null;
        StatusMessage = "Поля ввода очищены";
    }

    private void ExecuteCopyTriad()
    {
        if (!string.IsNullOrWhiteSpace(HoareTriadText))
        {
            Clipboard.SetText(HoareTriadText);
            StatusMessage = "Триада Хоара скопирована в буфер обмена";
        }
    }

    private void ExecuteCopyWp()
    {
        if (!string.IsNullOrWhiteSpace(FinalConditionShort))
        {
            Clipboard.SetText(FinalConditionShort);
            StatusMessage = "Формула wp скопирована в буфер обмена";
        }
    }

    private void ExecuteCopyLatex()
    {
        if (!string.IsNullOrWhiteSpace(FinalConditionLatex))
        {
            Clipboard.SetText($"$$ {FinalConditionLatex} $$");
            StatusMessage = "LaTeX-формула ($$ ... $$) скопирована в буфер обмена";
        }
    }

    private void ExecuteExportReport(object? _)
    {
        if (!HasResult)
        {
            StatusMessage = "Сначала выполните расчёт wp перед экспортом";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Экспорт отчёта wp",
            Filter = "Markdown-файл (*.md)|*.md|Все файлы (*.*)|*.*",
            DefaultExt = ".md",
            FileName = $"wp_report_{DateTime.Now:yyyyMMdd_HHmm}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ReportExportService.ExportToFile(this, dialog.FileName);
                StatusMessage = $"Отчёт сохранён: {System.IO.Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка экспорта: {ex.Message}";
                MessageBox.Show($"Не удалось сохранить файл:\n{ex.Message}", "Ошибка экспорта",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ExecuteInsertSnippet(object? parameter)
    {
        if (parameter is string snippet)
        {
            ProgramCodeText = (ProgramCodeText + "\n" + snippet).Trim();
            StatusMessage = $"Вставлен шаблон: {snippet}";
        }
    }
}
