namespace WpEngine.Core.Parsing;

/// <summary>
/// Лексер: подготавливает введённый текст для парсера, разбивая его на токены.
/// </summary>
public static class Lexer
{
    /// <summary>
    /// Знаки из двух символов и соответствующие им типы токенов.
    /// </summary>
    private static readonly Dictionary<string, TokenType> TwoCharSymbols = new()
    {
        [":="] = TokenType.Assign,
        ["=="] = TokenType.Equal,
        ["!="] = TokenType.NotEqual,
        ["<="] = TokenType.LessOrEqual,
        [">="] = TokenType.GreaterOrEqual,
        ["&&"] = TokenType.And,
        ["||"] = TokenType.Or
    };

    /// <summary>
    /// Знаки из одного символа и соответствующие им типы токенов.
    /// </summary>
    private static readonly Dictionary<char, TokenType> OneCharSymbols = new()
    {
        ['+'] = TokenType.Plus,
        ['-'] = TokenType.Minus,
        ['*'] = TokenType.Star,
        ['/'] = TokenType.Slash,
        ['<'] = TokenType.Less,
        ['>'] = TokenType.Greater,
        ['!'] = TokenType.Not,
        ['('] = TokenType.LeftParen,
        [')'] = TokenType.RightParen,
        ['{'] = TokenType.LeftBrace,
        ['}'] = TokenType.RightBrace,
        [';'] = TokenType.Semicolon
    };

    /// <summary>
    /// Разбивает строку на токены слева направо.
    /// </summary>
    /// <param name="text">Исходный текст программы или условия.</param>
    /// <returns>Список токенов, последний из них всегда End.</returns>
    /// <exception cref="ParseException">В тексте есть символ, который не получилось распознать.</exception>
    public static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            // Пробелы и переносы строк в токены не попадают
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Методы Read* сами сдвигают i за прочитанный токен
            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber(text, ref i));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                tokens.Add(ReadIdentifier(text, ref i));
                continue;
            }

            // Всё, что не число и не имя, считаем знаком; неизвестный символ даст ошибку
            tokens.Add(ReadSymbol(text, ref i));
        }

        // Маркер конца, чтобы парсеру не проверять выход за границы списка
        tokens.Add(new Token(TokenType.End, "", text.Length));
        return tokens;
    }

    /// <summary>
    /// Парсит число из текста (если между двумя числами есть "." 
    /// то, парсит как вещественное).
    /// </summary>
    /// <param name="text">Исходный текст.</param>
    /// <param name="i">Индекс первой цифры. После вызова указывает на первый символ после числа.</param>
    /// <returns>Токен Number с текстом числа.</returns>
    private static Token ReadNumber(string text, ref int i)
    {
        // Запоминаем, где число началось, чтобы потом вырезать его из строки
        int start = i;

        // Отсчёт целой части - движение вперёд, пока попадаются цифры.
        while (i < text.Length && char.IsDigit(text[i]))
            i++;

        // Вычисление дробной части. 
        // Нужны два символа подряд: точка и цифра после неё
        if (i + 1 < text.Length && text[i] == '.' && char.IsDigit(text[i + 1]))
        {
            // Пропуск точки
            i++;

            // Движение до упора по цифрам после точки
            while (i < text.Length && char.IsDigit(text[i]))
                i++;
        }

        return new Token(TokenType.Number, text[start..i], start);
    }

    /// <summary>
    /// Парсит имя переменной из текста 
    /// (if и else тоже приходят как имена - различать их будет парсер).
    /// </summary>
    /// <param name="text">Исходный текст.</param>
    /// <param name="i">Индекс первого символа имени. После вызова указывает на первый символ после имени.</param>
    /// <returns>Токен Identifier с текстом имени.</returns>
    private static Token ReadIdentifier(string text, ref int i)
    {
        // Запоминаем начало имени
        int start = i;

        // Движение вперёд, пока попадаются буквы, цифры или "_".
        // Первый символ уже проверен снаружи, в начале имени цифры быть не может
        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
            i++;

        return new Token(TokenType.Identifier, text[start..i], start);
    }

    /// <summary>
    /// Парсит знак операции или скобку из текста 
    /// (знак может состоять из одного символа, как "+", 
    /// или из двух, как ":=").
    /// </summary>
    /// <param name="text">Исходный текст.</param>
    /// <param name="i">Индекс первого символа знака. После вызова указывает на первый символ после знака.</param>
    /// <returns>Токен нужного типа.</returns>
    /// <exception cref="ParseException">Такого символа в языке нет.</exception>
    private static Token ReadSymbol(string text, ref int i)
    {
        int start = i;

        // Сначала проверяем знаки из двух символов, 
        // иначе "<=" разобьётся на "<" и "=".
        // Проверка длины нужна, чтобы не выйти за конец строки
        if (i + 1 < text.Length)
        {
            string pair = text.Substring(i, 2);

            if (TwoCharSymbols.TryGetValue(pair, out TokenType pairType))
            {
                // Пропуск обоих символов
                i += 2;
                return new Token(pairType, pair, start);
            }
        }

        // Знаки из одного символа
        char c = text[i];

        if (OneCharSymbols.TryGetValue(c, out TokenType type))
        {
            // Пропуск одного символа
            i++;
            return new Token(type, c.ToString(), start);
        }

        // Ни один знак не подошёл
        throw new ParseException(UnknownSymbolMessage(c), start);
    }

    /// <summary>
    /// Формирует сообщение об ошибке для нераспознанного символа. Для одиночных
    /// ":", "=", "&amp;" и "|" подсказывает, какой знак имелся в виду.
    /// </summary>
    /// <param name="c">Нераспознанный символ.</param>
    /// <returns>Текст сообщения об ошибке.</returns>
    private static string UnknownSymbolMessage(char c) => c switch
    {
        ':' => "Ожидалось ':=' (присваивание), а не одиночное ':'",
        '=' => "Ожидалось '==' (сравнение) или ':=' (присваивание), а не одиночное '='",
        '&' => "Ожидалось '&&', а не одиночное '&'",
        '|' => "Ожидалось '||', а не одиночное '|'",
        _ => $"Неизвестный символ '{c}'"
    };
}
