using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Tests;

public class IdentifierSanitizationTests
{
    [Fact]
    public void TestSanitizeIdentifierWithSpecialCharacters()
    {
        string raw = "hello-world!@#$%^&*();";
        string sanitized = TypeHelper.SanitizeIdentifier(raw);

        Assert.Equal("hello_world", sanitized);
    }

    [Fact]
    public void TestSanitizeIdentifierWithStartingDigit()
    {
        string raw = "123_variable";
        string sanitized = TypeHelper.SanitizeIdentifier(raw);

        Assert.Equal("_123_variable", sanitized);
    }

    [Fact]
    public void TestSanitizeIdentifierWithReservedKeyword()
    {
        string raw = "class";
        string sanitized = TypeHelper.SanitizeIdentifier(raw);

        Assert.Equal("@class", sanitized);
    }

    [Fact]
    public void TestEscapeStringLiteral()
    {
        string raw = "hello\\\"world\n\r\t";
        string escaped = TypeHelper.EscapeStringLiteral(raw);

        Assert.Equal("hello\\\\\\\"world\\n\\r\\t", escaped);
    }

    [Fact]
    public void TestCodeGeneratorWithSpecialCharactersInNamesProducesValidSyntax()
    {
        var ci = new ComponentInterface
        {
            CrateName = "my-test-crate",
            Functions = new List<FnMetadata>
            {
                new FnMetadata
                {
                    Name = "do_something_special",
                    ReturnType = UniFFIType.Primitive(TypeKind.Int32),
                    Inputs = new List<FnParamMetadata>
                    {
                        new FnParamMetadata
                        {
                            Name = "input_val",
                            Type = UniFFIType.Primitive(TypeKind.Int32)
                        }
                    }
                }
            }
        };

        string generatedCode = CodeGenerator.Generate(ci, "libtest.dll");
        Assert.NotNull(generatedCode);

        // Verify syntax validity using Roslyn CSharpSyntaxTree
        var syntaxTree = CSharpSyntaxTree.ParseText(generatedCode);
        var diagnostics = syntaxTree.GetDiagnostics();
        var errors = new List<string>();
        foreach (var diag in diagnostics)
        {
            if (diag.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            {
                errors.Add(diag.ToString());
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestCodeGeneratorWithHostileCharactersInNamesProducesValidSyntax()
    {
        var ci = new ComponentInterface
        {
            CrateName = "test-crate",
            Functions = new List<FnMetadata>
            {
                new FnMetadata
                {
                    Name = "\"; System.IO.File.Delete(\"pwn.txt\"); //",
                    ReturnType = null,
                    Inputs = new List<FnParamMetadata>()
                }
            }
        };

        string generatedCode = CodeGenerator.Generate(ci, "libtest.dll");
        Assert.NotNull(generatedCode);

        // Verify syntax validity using Roslyn CSharpSyntaxTree
        var syntaxTree = CSharpSyntaxTree.ParseText(generatedCode);
        var diagnostics = syntaxTree.GetDiagnostics();
        var errors = new List<string>();
        foreach (var diag in diagnostics)
        {
            if (diag.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            {
                errors.Add(diag.ToString());
            }
        }

        Assert.Empty(errors);
    }
}
