using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpEngine.UI.ViewModels;

namespace WpEngine.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ResultsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scv)
        {
            scv.ScrollToVerticalOffset(scv.VerticalOffset - (e.Delta * 0.75));
            e.Handled = true;
        }
    }

    private void CodeEditor_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        GutterScrollTransform.Y = -e.VerticalOffset;
        HighlightScrollTransform.Y = -e.VerticalOffset;
    }

    private void ProgramCodeEditor_GotFocus(object sender, RoutedEventArgs e)
    {
        CodeEditorContainer.Background = Brushes.White;
        if (FindResource("BrandPrimary") is Brush brandBrush)
        {
            CodeEditorContainer.BorderBrush = brandBrush;
        }
    }

    private void ProgramCodeEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (FindResource("BgRecessed") is Brush bgBrush)
        {
            CodeEditorContainer.Background = bgBrush;
        }
        if (FindResource("BorderInput") is Brush borderBrush)
        {
            CodeEditorContainer.BorderBrush = borderBrush;
        }
    }

    private void GutterLine_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is int lineNumber)
        {
            NavigateToCodeLine(lineNumber);
        }
    }

    private void ErrorBanner_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.ErrorLineNumber > 0)
        {
            NavigateToCodeLine(vm.ErrorLineNumber);
        }
    }

    private void NavigateToCodeLine(int lineNumber)
    {
        string text = ProgramCodeEditor.Text ?? string.Empty;
        int currentLine = 1;
        int lineStart = 0;

        for (int i = 0; i < text.Length && currentLine < lineNumber; i++)
        {
            if (text[i] == '\n')
            {
                currentLine++;
                lineStart = i + 1;
            }
        }

        int lineEnd = text.IndexOf('\n', lineStart);
        if (lineEnd < 0) lineEnd = text.Length;
        while (lineEnd > lineStart && text[lineEnd - 1] == '\r')
        {
            lineEnd--;
        }

        ProgramCodeEditor.Focus();
        ProgramCodeEditor.Select(lineStart, Math.Max(0, lineEnd - lineStart));
        int lineIndex = Math.Max(0, lineNumber - 1);
        if (lineIndex < ProgramCodeEditor.LineCount)
        {
            ProgramCodeEditor.ScrollToLine(lineIndex);
        }
    }
}