using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Network_Monitor;

/// <summary>
/// Reads and writes the JSON in the settings file, so the app stays a single exe instead of needing a JSON library beside it.
/// </summary>
/// <remarks>
/// Objects are read as dictionaries whose names ignore case, arrays as lists, numbers as doubles, and strings, true, false, and null as themselves.
/// A comma after the last value in an object or array is allowed, which strict JSON doesn't, as one is easily left behind when a setting is removed by hand.
/// </remarks>
public static class Json
{
    /// <summary>
    /// Values nested deeper than this are refused, so a damaged file can't run the reader out of stack, which would close the app every time it starts.
    /// </summary>
    private const int MaxDepth = 32;

    /// <summary>
    /// Reads the JSON value in <paramref name="text" />.
    /// </summary>
    /// <exception cref="FormatException">The text isn't JSON.</exception>
    public static object Parse(string text)
    {
        var reader = new Reader(text);
        var value = reader.ReadValue(0);

        if (!reader.IsAtEnd())
            throw reader.Error("Unexpected text after the end");

        return value;
    }

    /// <summary>
    /// Writes <paramref name="value" /> as JSON indented with two spaces, one value to a line, so it's easy to read and change by hand.
    /// </summary>
    /// <param name="value">Names paired with values for an object, or a string, bool, int, or null.</param>
    public static string Write(object value)
    {
        var builder = new StringBuilder();
        Write(builder, value, 0);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, object value, int depth)
    {
        if (value is IEnumerable<KeyValuePair<string, object>> properties)
        {
            WriteObject(builder, properties, depth);
            return;
        }

        builder.Append(value switch
        {
            null => "null",
            bool boolean => boolean ? "true" : "false",
            int number => number.ToString(CultureInfo.InvariantCulture),
            string text => Quote(text),
            _ => throw new NotSupportedException($"{value.GetType()} can't be written as JSON."),
        });
    }

    private static void WriteObject(StringBuilder builder, IEnumerable<KeyValuePair<string, object>> properties, int depth)
    {
        var isEmpty = true;
        builder.Append('{');

        foreach (var property in properties)
        {
            builder.Append(isEmpty ? "" : ",").Append(Environment.NewLine).Append(' ', (depth + 1) * 2);
            builder.Append(Quote(property.Key)).Append(": ");
            Write(builder, property.Value, depth + 1);
            isEmpty = false;
        }

        if (!isEmpty)
            builder.Append(Environment.NewLine).Append(' ', depth * 2);

        builder.Append('}');
    }

    private static string Quote(string text)
    {
        var builder = new StringBuilder(text.Length + 2);
        builder.Append('"');

        foreach (var c in text)
        {
            var escaped = c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
                _ => null,
            };

            if (escaped is null)
                builder.Append(c);
            else
                builder.Append(escaped);
        }

        return builder.Append('"').ToString();
    }

    private sealed class Reader
    {
        private readonly string _text;
        private int _position;

        public Reader(string text)
        {
            _text = text ?? string.Empty;
        }

        /// <summary>
        /// Skips whitespace and returns whether that was the end of the text.
        /// </summary>
        public bool IsAtEnd()
        {
            while (_position < _text.Length && _text[_position] is ' ' or '\t' or '\r' or '\n')
                _position++;

            return _position == _text.Length;
        }

        public FormatException Error(string message) => new($"{message} at character {_position + 1}.");

        public object ReadValue(int depth)
        {
            if (depth > MaxDepth)
                throw Error("Nested too deeply");

            if (IsAtEnd())
                throw Error("Expected a value");

            return _text[_position] switch
            {
                '{' => ReadObject(depth),
                '[' => ReadArray(depth),
                '"' => ReadString(),
                't' => ReadWord("true", true),
                'f' => ReadWord("false", false),
                'n' => ReadWord("null", null),
                '-' or (>= '0' and <= '9') => ReadNumber(),
                _ => throw Error("Expected a value"),
            };
        }

        private Dictionary<string, object> ReadObject(int depth)
        {
            // Names that differ only in case are the same setting, so a hand-edited "topmost" still counts.
            var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _position++;

            // Stopping at the closing brace after a comma, as well as at the start, allows a comma after the last value.
            while (!IsAtEnd() && _text[_position] != '}')
            {
                if (_text[_position] != '"')
                    throw Error("Expected a name in quotes");

                var name = ReadString();
                Expect(':');
                properties[name] = ReadValue(depth + 1);

                if (IsAtEnd() || _text[_position] != ',')
                    break;

                _position++;
            }

            Expect('}');
            return properties;
        }

        private List<object> ReadArray(int depth)
        {
            var items = new List<object>();
            _position++;

            while (!IsAtEnd() && _text[_position] != ']')
            {
                items.Add(ReadValue(depth + 1));

                if (IsAtEnd() || _text[_position] != ',')
                    break;

                _position++;
            }

            Expect(']');
            return items;
        }

        private string ReadString()
        {
            var builder = new StringBuilder();
            _position++;

            while (true)
            {
                if (_position >= _text.Length)
                    throw Error("Expected the closing quote");

                var c = _text[_position++];

                if (c == '"')
                    return builder.ToString();

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (_position >= _text.Length)
                    throw Error("Expected an escaped character");

                builder.Append(_text[_position++] switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    '/' => '/',
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'u' => ReadHexCharacter(),
                    _ => throw Error("Unknown escaped character"),
                });
            }
        }

        private char ReadHexCharacter()
        {
            if (_position + 4 > _text.Length || !int.TryParse(_text.Substring(_position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
                throw Error("Expected four hex digits");

            _position += 4;
            return (char)code;
        }

        private object ReadWord(string word, object value)
        {
            if (string.CompareOrdinal(_text, _position, word, 0, word.Length) != 0)
                throw Error("Expected a value");

            _position += word.Length;
            return value;
        }

        private double ReadNumber()
        {
            var start = _position;

            while (_position < _text.Length && _text[_position] is (>= '0' and <= '9') or '-' or '+' or '.' or 'e' or 'E')
                _position++;

            // Only the plain forms JSON allows, not hex, thousands separators, or the words for infinity.
            if (!double.TryParse(_text.Substring(start, _position - start), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var number))
            {
                _position = start;
                throw Error("Expected a number");
            }

            return number;
        }

        private void Expect(char c)
        {
            if (IsAtEnd() || _text[_position] != c)
                throw Error($"Expected '{c}'");

            _position++;
        }
    }
}
