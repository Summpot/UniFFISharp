using System;
using System.Text;

namespace UniFFISharp.Generator.Codegen;

public class CSharpCodeBuilder
{
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
        _sb.Append(new string(' ', _indent * 4));
        _sb.AppendLine(line);
    }

    public IDisposable Block(string line, string suffix = "")
    {
        AppendLine(line);
        AppendLine("{");
        Indent();
        return new Scope(() =>
        {
            Unindent();
            AppendLine("}" + suffix);
        });
    }

    public override string ToString() => _sb.ToString();

    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;
        public Scope(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
