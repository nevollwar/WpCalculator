namespace WpEngine.Core.Parsing;

/// <summary>
/// Ошибка разбора входного текста 
/// (лексер и парсер бросают её на некорректном вводе).
/// </summary>
public class ParseException : Exception
{
    /// <summary>
    /// Индекс символа с ошибкой, начиная с 0.
    /// </summary>
    public int Position { get; }

    /// <param name="message">Текст сообщения об ошибке.</param>
    /// <param name="position">Индекс символа, начиная с 0.</param>
    public ParseException(string message, int position)
        : base($"{message} (позиция {position})")
    {
        Position = position;
    }
}
