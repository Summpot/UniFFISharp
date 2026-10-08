using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata;
using Xunit;
using TypeKind = UniFFISharp.Generator.Metadata.TypeKind;

namespace UniFFISharp.Tests;

public class CSharp15UnionTests
{
    private ComponentInterface CreateTestComponentInterface()
    {
        var ci = new ComponentInterface
        {
            CrateName = "drone_controller",
            Namespace = "DroneController"
        };

        // Tagged Union
        ci.Enums.Add(new EnumMetadata
        {
            Name = "flight_command",
            Shape = EnumShape.Enum,
            Docstring = "Commands for operating drone",
            Variants = new List<VariantMetadata>
            {
                new()
                {
                    Name = "hover",
                    Docstring = "Hover in place"
                },
                new()
                {
                    Name = "fly_to",
                    Docstring = "Fly to coordinate",
                    Fields = new List<FieldMetadata>
                    {
                        new() { Name = "latitude", Type = UniFFIType.Primitive(TypeKind.Float64) },
                        new() { Name = "longitude", Type = UniFFIType.Primitive(TypeKind.Float64) }
                    }
                }
            }
        });

        // Function using Option<FlightCommand>
        ci.Functions.Add(new FnMetadata
        {
            Name = "get_current_command",
            Inputs = new List<FnParamMetadata>(),
            ReturnType = new UniFFIType
            {
                Kind = TypeKind.Option,
                InnerType = new UniFFIType
                {
                    Kind = TypeKind.Enum,
                    Name = "flight_command"
                }
            }
        });

        return ci;
    }

    [Fact]
    public void TestGeneratesNativeUnionWhenOptionExplicitlyTrue()
    {
        var ci = CreateTestComponentInterface();
        var options = new CodeGeneratorOptions
        {
            UseNativeUnions = true
        };

        string code = CodeGenerator.Generate(ci, "drone.dll", null, options);

        // Native union declaration
        Assert.Contains("public partial union FlightCommand(FlightCommand.Hover, FlightCommand.FlyTo)", code);

        // Variants are sealed partial records inside union and DO NOT inherit from FlightCommand
        Assert.Contains("public sealed partial record Hover();", code);
        Assert.DoesNotContain("public sealed partial record Hover() : FlightCommand;", code);
        Assert.Contains("public sealed partial record FlyTo(double Latitude, double Longitude);", code);
        Assert.DoesNotContain("public sealed partial record FlyTo(double Latitude, double Longitude) : FlightCommand;", code);

        // FfiConverter returns wrapped union instances
        Assert.Contains("return new FlightCommand(new FlightCommand.Hover());", code);
        Assert.Contains("return new FlightCommand(new FlightCommand.FlyTo(", code);

        // Optional converter treats native union as value type (value.Value)
        Assert.Contains("FfiConverterTypeFlightCommand.INSTANCE.AllocationSize(value.Value)", code);
        Assert.Contains("FfiConverterTypeFlightCommand.INSTANCE.Write(value.Value, stream)", code);
    }

    [Fact]
    public void TestGeneratesClosedRecordHierarchyWhenDefaultOrPreCSharp15()
    {
        var ci = CreateTestComponentInterface();
        var options = new CodeGeneratorOptions
        {
            LanguageVersion = LanguageVersion.CSharp10
        };

        string code = CodeGenerator.Generate(ci, "drone.dll", null, options);

        // Classic closed hierarchy
        Assert.Contains("public abstract partial record FlightCommand", code);
        Assert.Contains("private FlightCommand() { }", code);
        Assert.Contains("public sealed partial record Hover() : FlightCommand;", code);
        Assert.Contains("public sealed partial record FlyTo(double Latitude, double Longitude) : FlightCommand;", code);

        // FfiConverter returns direct variant record
        Assert.Contains("return new FlightCommand.Hover();", code);
        Assert.Contains("return new FlightCommand.FlyTo(", code);

        // Optional converter treats record class as reference type (value!)
        Assert.Contains("FfiConverterTypeFlightCommand.INSTANCE.AllocationSize(value!)", code);
        Assert.Contains("FfiConverterTypeFlightCommand.INSTANCE.Write(value!, stream)", code);
    }

    [Fact]
    public void TestAutoDetectsCSharp15ViaLanguageVersion()
    {
        var ci = CreateTestComponentInterface();
        var options = new CodeGeneratorOptions
        {
            LanguageVersion = (LanguageVersion)1500 // C# 15.0
        };

        Assert.True(options.EffectiveUseNativeUnions);

        string code = CodeGenerator.Generate(ci, "drone.dll", null, options);
        Assert.Contains("public partial union FlightCommand(FlightCommand.Hover, FlightCommand.FlyTo)", code);
    }

    [Fact]
    public void TestAutoDetectsPreviewWithUnionAttributeSupport()
    {
        var ci = CreateTestComponentInterface();

        // Preview with support enabled
        var optionsWithSupport = new CodeGeneratorOptions
        {
            LanguageVersion = LanguageVersion.Preview,
            HasUnionAttributeSupport = true
        };
        Assert.True(optionsWithSupport.EffectiveUseNativeUnions);
        string codeSupported = CodeGenerator.Generate(ci, "drone.dll", null, optionsWithSupport);
        Assert.Contains("public partial union FlightCommand(FlightCommand.Hover, FlightCommand.FlyTo)", codeSupported);

        // Preview without support (e.g. .NET 8 / 9 preview SDK)
        var optionsWithoutSupport = new CodeGeneratorOptions
        {
            LanguageVersion = LanguageVersion.Preview,
            HasUnionAttributeSupport = false
        };
        Assert.False(optionsWithoutSupport.EffectiveUseNativeUnions);
        string codeNotSupported = CodeGenerator.Generate(ci, "drone.dll", null, optionsWithoutSupport);
        Assert.Contains("public abstract partial record FlightCommand", codeNotSupported);
    }
}
