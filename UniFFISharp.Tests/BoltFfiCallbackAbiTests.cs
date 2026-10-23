using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UniFFISharp.BoltFfi;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata.BoltFfi;
using Xunit;

namespace UniFFISharp.Tests;

public class BoltFfiCallbackAbiTests
{
    private static BoltFfiCanonicalName Name(params string[] parts)
    {
        var name = new BoltFfiCanonicalName();
        name.Parts.AddRange(parts);
        return name;
    }

    [Fact]
    public void EncodedCallbacksAndCallbackConstructorsCompileWithNativeSliceSignatures()
    {
        var callback = new BoltFfiCallbackDecl
        {
            Name = Name("logger"), RegisterSymbol = "register_logger", CreateHandleSymbol = "create_logger"
        };
        var method = new BoltFfiFunctionDecl { Name = Name("on", "log"), ReturnType = BoltFfiType.Void };
        method.Parameters.Add(new BoltFfiParamDecl { Name = "message", Type = BoltFfiType.Primitive(BoltFfiTypeKind.String) });
        method.Parameters.Add(new BoltFfiParamDecl
        {
            Name = "context", Type = new BoltFfiType { Kind = BoltFfiTypeKind.Option, InnerType = BoltFfiType.Primitive(BoltFfiTypeKind.String) }
        });
        callback.Methods.Add(method);
        var engine = new BoltFfiClassDecl { Name = Name("engine"), ReleaseSymbol = "release_engine" };
        var constructor = new BoltFfiFunctionDecl { Name = Name("new"), Symbol = "create_engine", ReturnType = BoltFfiType.Class("Engine") };
        constructor.Parameters.Add(new BoltFfiParamDecl { Name = "logger", Type = new BoltFfiType { Kind = BoltFfiTypeKind.Callback, CustomTypeName = "Logger" } });
        engine.Initializers.Add(constructor);
        foreach (bool isVoid in new[] { false, true })
        {
            string symbol = isVoid ? "stop" : "load";
            engine.Methods.Add(new BoltFfiFunctionDecl
            {
                Name = Name(symbol), Symbol = symbol, IsAsync = true, HasError = true,
                ReturnType = isVoid ? BoltFfiType.Void : BoltFfiType.Primitive(BoltFfiTypeKind.UInt32),
                ErrorType = BoltFfiType.Primitive(BoltFfiTypeKind.String),
                AsyncPollSymbol = symbol + "_poll", AsyncCompleteSymbol = symbol + "_complete",
                AsyncCancelSymbol = symbol + "_cancel", AsyncFreeSymbol = symbol + "_free"
            });
        }
        var envelope = new BoltFfiMetadataEnvelope { Magic = "boltffi.bindings", Surface = "Native" };
        envelope.Package.Name = Name("callbacks");
        envelope.Bindings.Native = new BoltFfiNativeBindings { Package = envelope.Package };
        envelope.Bindings.Native.Decls.Add(new BoltFfiDecl { Kind = BoltFfiDeclKind.Callback, Callback = callback });
        envelope.Bindings.Native.Decls.Add(new BoltFfiDecl { Kind = BoltFfiDeclKind.Class, Class = engine });
        var source = BoltFfiCodeGenerator.Generate(envelope, "callbacks_native", "Callbacks");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(FfiBuf).Assembly.Location).Distinct().Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("CallbackAbi_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        var assembly = Assembly.Load(stream.ToArray());
        var native = assembly.GetType("Callbacks.NativeMethods")!;
        var release = native.GetMethod("release_engine", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Null(release.GetCustomAttribute<SuppressGCTransitionAttribute>());
        var load = native.GetMethod("load_complete", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal(typeof(FfiBuf), load.ReturnType);
        Assert.Equal(3, load.GetParameters().Length);
        Assert.Equal(typeof(uint).MakeByRefType(), load.GetParameters()[2].ParameterType);
        var stop = native.GetMethod("stop_complete", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal(typeof(FfiBuf), stop.ReturnType);
        Assert.Equal(2, stop.GetParameters().Length);
        Assert.Equal(typeof(sbyte), native.GetMethod("load_poll", BindingFlags.Static | BindingFlags.NonPublic)!.ReturnType);
    }
}
