using System.Collections.Generic;
using Xunit;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Tests;

public class CodeGeneratorReexportTests
{
    [Fact]
    public void TestSubCrateCodeEmissionIsolation()
    {
        var ci = new ComponentInterface
        {
            CrateName = "sub_alpha",
            Namespace = "MyCompany.Sdk.Security"
        };
        ci.Functions.Add(new FnMetadata
        {
            Name = "login",
            ModulePath = "sub_alpha::service",
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "username", Type = new UniFFIType { Kind = TypeKind.String } }
            },
            ReturnType = new UniFFIType { Kind = TypeKind.Boolean }
        });

        string code = CodeGenerator.Generate(ci, "my_umbrella.dll", null);

        // 1. Correct namespace
        Assert.Contains("namespace MyCompany.Sdk.Security;", code);

        // 2. File-scoped alias
        Assert.Contains("using _UniFFILib = _UniFFILib_sub_alpha;", code);

        // 3. Class declaration and ctor
        Assert.Contains("internal static partial class _UniFFILib_sub_alpha", code);
        Assert.Contains("static _UniFFILib_sub_alpha()", code);

        // 4. Specific P/Invoke EntryPoint with sub-crate prefix
        Assert.Contains("EntryPoint = \"ffi_sub_alpha_rustbuffer_alloc\"", code);
        Assert.Contains("EntryPoint = \"uniffi_sub_alpha_fn_func_login\"", code);
        Assert.Contains("public static extern sbyte uniffi_sub_alpha_fn_func_login(RustBuffer username, ref UniffiRustCallStatus status);", code);
    }

    [Fact]
    public void TestCrossCrateTypeResolutionInGeneratedCode()
    {
        var ciCore = new ComponentInterface
        {
            CrateName = "core",
            Namespace = "MyCompany.Sdk.Core"
        };

        var externalRecordType = new UniFFIType
        {
            Kind = TypeKind.Record,
            Name = "user_credential",
            ModulePath = "sub_security::auth"
        };

        // Core has a function that accepts an external UserCredential
        ciCore.Functions.Add(new FnMetadata
        {
            Name = "authenticate",
            ModulePath = "core::auth",
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "cred", Type = externalRecordType }
            },
            ReturnType = new UniFFIType { Kind = TypeKind.Boolean }
        });

        // Core also has a record containing a list of external credentials
        ciCore.Records.Add(new RecordMetadata
        {
            Name = "Account",
            ModulePath = "core::models",
            Fields = new List<FieldMetadata>
            {
                new()
                {
                    Name = "credentials",
                    Type = new UniFFIType
                    {
                        Kind = TypeKind.Sequence,
                        InnerType = externalRecordType
                    }
                }
            }
        });

        var nsResolver = new System.Func<string, string>(c =>
        {
            if (c == "sub_security") return "MyCompany.Sdk.Security";
            if (c == "core") return "MyCompany.Sdk.Core";
            return "MyCompany.Sdk";
        });

        string code = CodeGenerator.Generate(ciCore, "my_umbrella.dll", nsResolver);

        // Function parameter should use MyCompany.Sdk.Security.UserCredential
        Assert.Contains("MyCompany.Sdk.Security.UserCredential cred", code);
        Assert.Contains("MyCompany.Sdk.Security.FfiConverterTypeUserCredential.INSTANCE.Lower(cred)", code);

        // Record definition should use List<MyCompany.Sdk.Security.UserCredential>
        Assert.Contains("List<MyCompany.Sdk.Security.UserCredential> Credentials", code);
    }
}
