using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata;
using Xunit;
using TypeKind = UniFFISharp.Generator.Metadata.TypeKind;

namespace UniFFISharp.Tests;

public class PartialTypesTests
{
    private ComponentInterface CreateSampleComponentInterface()
    {
        var ci = new ComponentInterface
        {
            CrateName = "test_crate",
            Namespace = "TestCrate"
        };

        // 1. Record
        ci.Records.Add(new RecordMetadata
        {
            Name = "user_profile",
            Fields = new List<FieldMetadata>
            {
                new() { Name = "name", Type = UniFFIType.Primitive(TypeKind.String) },
                new() { Name = "age", Type = UniFFIType.Primitive(TypeKind.Int32) }
            }
        });

        // 2. Object
        var obj = new ObjectMetadata
        {
            Name = "session_manager"
        };
        obj.Constructors.Add(new ConstructorMetadata
        {
            Name = "new",
            Inputs = new List<FnParamMetadata>()
        });
        obj.Methods.Add(new MethodMetadata
        {
            Name = "get_user_count",
            ReturnType = UniFFIType.Primitive(TypeKind.Int32),
            Inputs = new List<FnParamMetadata>()
        });
        ci.Objects.Add(obj);

        // 3. Callback Interface
        var cbi = new CallbackInterfaceMetadata
        {
            Name = "progress_listener"
        };
        cbi.Methods.Add(new TraitMethodMetadata
        {
            Name = "on_progress",
            ReturnType = null,
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "percentage", Type = UniFFIType.Primitive(TypeKind.Float32) }
            }
        });
        ci.CallbackInterfaces.Add(cbi);

        // 4. Flat Enum (Standard enum, cannot be partial)
        ci.Enums.Add(new EnumMetadata
        {
            Name = "status",
            Shape = EnumShape.Enum,
            Variants = new List<VariantMetadata>
            {
                new() { Name = "pending" },
                new() { Name = "active" }
            }
        });

        // 5. Flat Error
        ci.Enums.Add(new EnumMetadata
        {
            Name = "simple_error",
            Shape = EnumShape.ErrorFlat,
            Variants = new List<VariantMetadata>
            {
                new() { Name = "not_found" },
                new() { Name = "unauthorized" }
            }
        });

        // 6. Complex Error
        ci.Enums.Add(new EnumMetadata
        {
            Name = "detail_error",
            Shape = EnumShape.ErrorComplex,
            Variants = new List<VariantMetadata>
            {
                new() { Name = "io_failure" },
                new()
                {
                    Name = "network_timeout",
                    Fields = new List<FieldMetadata>
                    {
                        new() { Name = "timeout_ms", Type = UniFFIType.Primitive(TypeKind.Int32) }
                    }
                }
            }
        });

        // 7. Tagged Union (Sum type enum)
        ci.Enums.Add(new EnumMetadata
        {
            Name = "command",
            Shape = EnumShape.Enum,
            Variants = new List<VariantMetadata>
            {
                new() { Name = "stop" },
                new()
                {
                    Name = "set_speed",
                    Fields = new List<FieldMetadata>
                    {
                        new() { Name = "speed", Type = UniFFIType.Primitive(TypeKind.Float64) }
                    }
                }
            }
        });

        // 8. Top-level function
        ci.Functions.Add(new FnMetadata
        {
            Name = "init_system",
            Inputs = new List<FnParamMetadata>()
        });

        return ci;
    }

    [Fact]
    public void TestGeneratedCodeContainsExpectedPartialModifiers()
    {
        var ci = CreateSampleComponentInterface();
        string code = CodeGenerator.Generate(ci, "test.dll");

        // Record should be partial
        Assert.Contains("public partial record UserProfile(", code);

        // Object interface and class should be partial
        Assert.Contains("public partial interface ISessionManager : IDisposable", code);
        Assert.Contains("public partial class SessionManager : ISessionManager", code);

        // Callback interface should be partial
        Assert.Contains("public partial interface IProgressListener", code);

        // Top level methods class should be partial
        Assert.Contains("public static partial class TestCrateMethods", code);

        // Tagged Union base record and variant records should be partial
        Assert.Contains("public abstract partial record Command", code);
        Assert.Contains("public sealed partial record Stop() : Command;", code);
        Assert.Contains("public sealed partial record SetSpeed(", code);
        Assert.Contains(") : Command;", code);

        // Flat Error base and variant classes should be partial
        Assert.Contains("public partial class SimpleError : UniffiException", code);
        Assert.Contains("public partial class NotFound : SimpleError", code);
        Assert.Contains("public partial class Unauthorized : SimpleError", code);

        // Complex Error base and variant classes should be partial
        Assert.Contains("public abstract partial class DetailError : UniffiException", code);
        Assert.Contains("public sealed partial class IoFailure : DetailError", code);
        Assert.Contains("public sealed partial class NetworkTimeout : DetailError", code);

        // Converters should be partial
        Assert.Contains("public sealed partial class FfiConverterTypeUserProfile", code);
        Assert.Contains("public sealed partial class FfiConverterTypeSessionManager", code);
        Assert.Contains("public sealed partial class FfiConverterTypeCommand", code);

        // Underlying _UniFFILib class is partial
        Assert.Contains("internal static partial class _UniFFILib_test_crate", code);

        // Ordinary Enum must NOT be partial (C# restriction)
        Assert.Contains("public enum Status", code);
        Assert.DoesNotContain("partial enum Status", code);

        // Callback delegates must NOT be partial (C# restriction)
        Assert.Contains("public delegate void CallbackDelegate_ProgressListener_0(", code);
        Assert.DoesNotContain("partial delegate", code);
    }

    [Fact]
    public void TestGeneratedCodeSyntaxIsValid()
    {
        var ci = CreateSampleComponentInterface();
        string code = CodeGenerator.Generate(ci, "test.dll");

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var diagnostics = syntaxTree.GetDiagnostics();
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void TestUserPartialExtensionCompilesSuccessfullyWithGeneratedCode()
    {
        var ci = CreateSampleComponentInterface();
        string generatedCode = CodeGenerator.Generate(ci, "test.dll");

        // User extension code declaring partial types to add helper methods and properties
        string userExtensionCode = @"
namespace TestCrate;

// 1. Extend Record
public partial record UserProfile
{
    public string DisplayInfo => $""{Name} ({Age})"";
}

// 2. Extend Object Interface and Class
public partial interface ISessionManager
{
    bool IsActiveSession { get; }
}

public partial class SessionManager
{
    public bool IsActiveSession => true;
    public void Reset() { }
}

// 3. Extend TopLevel Methods static class
public static partial class TestCrateMethods
{
    public static string Version => ""1.0.0"";
}

// 4. Extend Tagged Union base and variant
public abstract partial record Command
{
    public string Summary => this switch
    {
        Stop => ""Stop"",
        SetSpeed s => $""Speed: {s.Speed}"",
        _ => ""Unknown""
    };

    public sealed partial record Stop
    {
        public bool IsImmediate => true;
    }

    public sealed partial record SetSpeed
    {
        public double SpeedKph => Speed * 3.6;
    }
}

// 5. Extend Error class
public partial class SimpleError
{
    public int ErrorCode => 400;
}
";

        var tree1 = CSharpSyntaxTree.ParseText(generatedCode);
        var tree2 = CSharpSyntaxTree.ParseText(userExtensionCode);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "PartialCompilationTest",
            new[] { tree1, tree2 },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        var errors = emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();

        Assert.True(emitResult.Success, string.Join(Environment.NewLine, errors));
    }
}
