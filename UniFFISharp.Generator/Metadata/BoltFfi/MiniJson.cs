using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UniFFISharp.Generator.Metadata.BoltFfi;

public enum JsonKind
{
    Null,
    Boolean,
    Number,
    String,
    Array,
    Object
}

public abstract class JsonNode
{
    public abstract JsonKind Kind { get; }

    public virtual JsonNode? this[string key] => null;
    public virtual JsonNode? this[int index] => null;

    public virtual string? AsString() => null;
    public virtual bool? AsBool() => null;
    public virtual long? AsInt64() => null;
    public virtual double? AsDouble() => null;
    public virtual List<JsonNode>? AsArray() => null;
    public virtual Dictionary<string, JsonNode>? AsObject() => null;

    public static JsonNode Parse(string json)
    {
        int index = 0;
        SkipWhitespace(json, ref index);
        var result = ParseValue(json, ref index);
        return result;
    }

    private static JsonNode ParseValue(string json, ref int index)
    {
        SkipWhitespace(json, ref index);
        if (index >= json.Length) return JsonNull.Instance;

        char c = json[index];
        if (c == '{') return ParseObject(json, ref index);
        if (c == '[') return ParseArray(json, ref index);
        if (c == '"') return new JsonString(ParseString(json, ref index));
        if (c == 't' || c == 'f') return ParseBool(json, ref index);
        if (c == 'n') return ParseNull(json, ref index);
        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(json, ref index);

        throw new FormatException($"Unexpected character '{c}' at position {index}");
    }

    private static JsonObject ParseObject(string json, ref int index)
    {
        index++; // skip '{'
        var obj = new JsonObject();
        SkipWhitespace(json, ref index);

        if (index < json.Length && json[index] == '}')
        {
            index++;
            return obj;
        }

        while (index < json.Length)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '"')
                throw new FormatException($"Expected string key at position {index}");

            string key = ParseString(json, ref index);
            SkipWhitespace(json, ref index);

            if (index >= json.Length || json[index] != ':')
                throw new FormatException($"Expected ':' at position {index}");

            index++; // skip ':'
            var val = ParseValue(json, ref index);
            obj.Properties[key] = val;

            SkipWhitespace(json, ref index);
            if (index >= json.Length) break;

            if (json[index] == ',')
            {
                index++;
                continue;
            }
            if (json[index] == '}')
            {
                index++;
                break;
            }

            throw new FormatException($"Expected ',' or '}}' at position {index}");
        }

        return obj;
    }

    private static JsonArray ParseArray(string json, ref int index)
    {
        index++; // skip '['
        var arr = new JsonArray();
        SkipWhitespace(json, ref index);

        if (index < json.Length && json[index] == ']')
        {
            index++;
            return arr;
        }

        while (index < json.Length)
        {
            var val = ParseValue(json, ref index);
            arr.Items.Add(val);

            SkipWhitespace(json, ref index);
            if (index >= json.Length) break;

            if (json[index] == ',')
            {
                index++;
                continue;
            }
            if (json[index] == ']')
            {
                index++;
                break;
            }

            throw new FormatException($"Expected ',' or ']' at position {index}");
        }

        return arr;
    }

    private static string ParseString(string json, ref int index)
    {
        index++; // skip '"'
        var sb = new StringBuilder();
        while (index < json.Length)
        {
            char c = json[index++];
            if (c == '"') return sb.ToString();
            if (c == '\\')
            {
                if (index >= json.Length) break;
                char esc = json[index++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (index + 4 <= json.Length)
                        {
                            string hex = json.Substring(index, 4);
                            if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                            {
                                sb.Append((char)cp);
                                index += 4;
                            }
                        }
                        break;
                    default: sb.Append(esc); break;
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static JsonBoolean ParseBool(string json, ref int index)
    {
        if (index + 4 <= json.Length && string.Compare(json, index, "true", 0, 4, StringComparison.Ordinal) == 0)
        {
            index += 4;
            return JsonBoolean.True;
        }
        if (index + 5 <= json.Length && string.Compare(json, index, "false", 0, 5, StringComparison.Ordinal) == 0)
        {
            index += 5;
            return JsonBoolean.False;
        }
        throw new FormatException($"Invalid boolean at position {index}");
    }

    private static JsonNull ParseNull(string json, ref int index)
    {
        if (index + 4 <= json.Length && string.Compare(json, index, "null", 0, 4, StringComparison.Ordinal) == 0)
        {
            index += 4;
            return JsonNull.Instance;
        }
        throw new FormatException($"Invalid null at position {index}");
    }

    private static JsonNumber ParseNumber(string json, ref int index)
    {
        int start = index;
        if (index < json.Length && json[index] == '-') index++;
        while (index < json.Length && ((json[index] >= '0' && json[index] <= '9') || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || json[index] == '+' || json[index] == '-'))
        {
            index++;
        }
        string numStr = json.Substring(start, index - start);
        return new JsonNumber(numStr);
    }

    private static void SkipWhitespace(string json, ref int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
        {
            index++;
        }
    }
}

public sealed class JsonNull : JsonNode
{
    public static readonly JsonNull Instance = new();
    public override JsonKind Kind => JsonKind.Null;
    private JsonNull() { }
    public override string ToString() => "null";
}

public sealed class JsonBoolean : JsonNode
{
    public static readonly JsonBoolean True = new(true);
    public static readonly JsonBoolean False = new(false);
    public bool Value { get; }
    public override JsonKind Kind => JsonKind.Boolean;
    public JsonBoolean(bool value) => Value = value;
    public override bool? AsBool() => Value;
    public override string ToString() => Value ? "true" : "false";
}

public sealed class JsonNumber : JsonNode
{
    public string RawValue { get; }
    public override JsonKind Kind => JsonKind.Number;
    public JsonNumber(string raw) => RawValue = raw;
    public override long? AsInt64() => long.TryParse(RawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long val) ? val : null;
    public override double? AsDouble() => double.TryParse(RawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : null;
    public override string? AsString() => RawValue;
    public override string ToString() => RawValue;
}

public sealed class JsonString : JsonNode
{
    public string Value { get; }
    public override JsonKind Kind => JsonKind.String;
    public JsonString(string val) => Value = val;
    public override string? AsString() => Value;
    public override string ToString() => Value;
}

public sealed class JsonArray : JsonNode
{
    public List<JsonNode> Items { get; } = new();
    public override JsonKind Kind => JsonKind.Array;
    public override JsonNode? this[int index] => (index >= 0 && index < Items.Count) ? Items[index] : null;
    public override List<JsonNode>? AsArray() => Items;
}

public sealed class JsonObject : JsonNode
{
    public Dictionary<string, JsonNode> Properties { get; } = new(StringComparer.Ordinal);
    public override JsonKind Kind => JsonKind.Object;
    public override JsonNode? this[string key] => Properties.TryGetValue(key, out var val) ? val : null;
    public override Dictionary<string, JsonNode>? AsObject() => Properties;

    public bool TryGet(string key, out JsonNode value) => Properties.TryGetValue(key, out value!);
}
