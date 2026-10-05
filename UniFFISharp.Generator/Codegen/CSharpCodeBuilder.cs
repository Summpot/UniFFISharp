using System;
using System.Text;

namespace UniFFISharp.Generator.Codegen;

public class CSharpCodeBuilder
{
    private static readonly string[] Indents = CreateIndents();

    private static string[] CreateIndents()
    {
        var indents = new string[32];
        for (int i = 0; i < indents.Length; i++)
        {
            indents[i] = new string(' ', i * 4);
        }
        return indents;
    }

    private readonly StringBuilder _sb = new();
    private int _indent = 0;

    public void Indent() => _indent++;
    public void Unindent() { if (_indent > 0) _indent--; }

    public void AppendLine()
    {
        _sb.AppendLine();
    }

    public void AppendLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            _sb.AppendLine();
            return;
        }
        if (_indent < Indents.Length)
        {
            _sb.Append(Indents[_indent]);
        }
        else
        {
            _sb.Append(new string(' ', _indent * 4));
        }
        _sb.AppendLine(line);
    }

    public Scope Block(string line, string suffix = "")
    {
        AppendLine(line);
        AppendLine("{");
        Indent();
        return new Scope(this, suffix);
    }

    public override string ToString() => _sb.ToString();

    public readonly struct Scope : IDisposable
    {
        private readonly CSharpCodeBuilder? _builder;
        private readonly string? _suffix;

        public Scope(CSharpCodeBuilder builder, string suffix)
        {
            _builder = builder;
            _suffix = suffix;
        }

        public void Dispose()
        {
            if (_builder != null)
            {
                _builder.Unindent();
                _builder.AppendLine("}" + (_suffix ?? string.Empty));
            }
        }
    }
}
