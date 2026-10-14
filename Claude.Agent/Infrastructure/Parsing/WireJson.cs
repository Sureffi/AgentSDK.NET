using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Claude.Agent;

// The JSON spelling of everything this library writes to the binary: ", " and ": " separators,
// ASCII only with lowercase \uXXXX escapes, and floats in the shortest round-trip form with a
// trailing ".0" for integral values. The fixtures pin these bytes line for line, and a recording
// made through this writer replays through it byte-exact; System.Text.Json's writer spells all
// three differently.
internal sealed class WireJson
{
    private readonly StringBuilder _text = new();
    private readonly List<Container> _stack = [];
    private readonly List<bool> _first = [];
    private bool _pendingName;

    private enum Container
    {
        Object,
        Array,
    }

    public static string Serialize(JsonElement element)
    {
        var writer = new WireJson();
        writer.Element(element);
        return writer.ToString();
    }

    public void BeginObject()
    {
        WriteValuePrefix();
        _text.Append('{');
        _stack.Add(Container.Object);
        _first.Add(true);
    }

    public void EndObject()
    {
        _text.Append('}');
        Pop();
    }

    public void BeginArray()
    {
        WriteValuePrefix();
        _text.Append('[');
        _stack.Add(Container.Array);
        _first.Add(true);
    }

    public void EndArray()
    {
        _text.Append(']');
        Pop();
    }

    public void Name(string name)
    {
        Separator();
        AppendString(name);
        _text.Append(": ");
        _pendingName = true;
    }

    public void String(string value)
    {
        WriteValuePrefix();
        AppendString(value);
    }

    public void Number(long value)
    {
        WriteValuePrefix();
        _text.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    public void Number(double value)
    {
        WriteValuePrefix();
        _text.Append(Float(value));
    }

    public void Bool(bool value)
    {
        WriteValuePrefix();
        _text.Append(value ? "true" : "false");
    }

    public void Null()
    {
        WriteValuePrefix();
        _text.Append("null");
    }

    public void Raw(string json)
    {
        WriteValuePrefix();
        _text.Append(json);
    }

    public void Element(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                BeginObject();
                foreach (var property in element.EnumerateObject())
                {
                    Name(property.Name);
                    Element(property.Value);
                }

                EndObject();
                break;
            case JsonValueKind.Array:
                BeginArray();
                foreach (var item in element.EnumerateArray())
                {
                    Element(item);
                }

                EndArray();
                break;
            case JsonValueKind.String:
                String(element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Number:
                RawNumber(element.GetRawText());
                break;
            case JsonValueKind.True:
                Bool(true);
                break;
            case JsonValueKind.False:
                Bool(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                Null();
                break;
        }
    }

    public void Strings(IEnumerable<string> values)
    {
        BeginArray();
        foreach (var value in values)
        {
            String(value);
        }

        EndArray();
    }

    public void Map(IEnumerable<KeyValuePair<string, string>> values)
    {
        BeginObject();
        foreach (var (key, value) in values)
        {
            Name(key);
            String(value);
        }

        EndObject();
    }

    public void Property(string name, string value)
    {
        Name(name);
        String(value);
    }

    public void Property(string name, long value)
    {
        Name(name);
        Number(value);
    }

    public void Property(string name, bool value)
    {
        Name(name);
        Bool(value);
    }

    public void Property(string name, JsonElement value)
    {
        Name(name);
        Element(value);
    }

    public override string ToString() => _text.ToString();

    public static string Float(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        var round = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture);
        var exponent = 0;
        var marker = round.IndexOfAny(['E', 'e']);
        if (marker >= 0)
        {
            exponent = int.Parse(round[(marker + 1)..], CultureInfo.InvariantCulture);
            round = round[..marker];
        }

        var point = round.IndexOf('.', StringComparison.Ordinal);
        var digits = point < 0 ? round : round.Remove(point, 1);
        var decimalPoint = (point < 0 ? round.Length : point) + exponent;

        var lead = 0;
        while (lead < digits.Length && digits[lead] == '0')
        {
            lead++;
        }

        digits = digits[lead..].TrimEnd('0');
        decimalPoint -= lead;

        string text;
        if (digits.Length == 0)
        {
            text = "0.0";
        }
        else if (decimalPoint is > -4 and <= 16)
        {
            text = decimalPoint switch
            {
                <= 0 => "0." + new string('0', -decimalPoint) + digits,
                _ when decimalPoint >= digits.Length =>
                    digits + new string('0', decimalPoint - digits.Length) + ".0",
                _ => digits[..decimalPoint] + "." + digits[decimalPoint..],
            };
        }
        else
        {
            var power = decimalPoint - 1;
            var mantissa = digits.Length == 1 ? digits : digits[..1] + "." + digits[1..];
            text = mantissa
                + "e"
                + (power < 0 ? "-" : "+")
                + Math.Abs(power).ToString("00", CultureInfo.InvariantCulture);
        }

        return double.IsNegative(value) ? "-" + text : text;
    }

    private void RawNumber(string raw)
    {
        if (raw.AsSpan().IndexOfAny('.', 'e', 'E') < 0)
        {
            Raw(raw);
            return;
        }

        // A literal too large for a double parses as an infinity, which spells a token that is not
        // JSON. The line the binary wrote is JSON by construction, so it goes back as it came.
        Raw(double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value)
            ? Float(value)
            : raw);
    }

    private void Pop()
    {
        _stack.RemoveAt(_stack.Count - 1);
        _first.RemoveAt(_first.Count - 1);
    }

    private void Separator()
    {
        if (_first.Count == 0)
        {
            return;
        }

        if (_first[^1])
        {
            _first[^1] = false;
        }
        else
        {
            _text.Append(", ");
        }
    }

    private void WriteValuePrefix()
    {
        if (_pendingName)
        {
            _pendingName = false;
            return;
        }

        if (_stack.Count != 0 && _stack[^1] == Container.Array)
        {
            Separator();
        }
    }

    private void AppendString(string value)
    {
        _text.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    _text.Append("\\\"");
                    break;
                case '\\':
                    _text.Append("\\\\");
                    break;
                case '\b':
                    _text.Append("\\b");
                    break;
                case '\f':
                    _text.Append("\\f");
                    break;
                case '\n':
                    _text.Append("\\n");
                    break;
                case '\r':
                    _text.Append("\\r");
                    break;
                case '\t':
                    _text.Append("\\t");
                    break;
                default:
                    if (c is < ' ' or > '~')
                    {
                        _text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        _text.Append(c);
                    }

                    break;
            }
        }

        _text.Append('"');
    }
}
