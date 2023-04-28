// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.Memory.Pinecone.Model;

#pragma warning disable CA1008 // Add a member to the enum with a zero value

/// <summary>
/// The pod type
/// </summary>
[JsonConverter(typeof(PodTypeJsonConverter))]
public enum PodType
{
    /// <summary>
    /// Enum S1X1 for value: s1.x1
    /// </summary>
    [EnumMember(Value = "s1.x1")]
    S1X1 = 1,

    /// <summary>
    /// Enum S1X2 for value: s1.x2
    /// </summary>
    [EnumMember(Value = "s1.x2")]
    S1X2 = 2,

    /// <summary>
    /// Enum S1X4 for value: s1.x4
    /// </summary>
    [EnumMember(Value = "s1.x4")]
    S1X4 = 3,

    /// <summary>
    /// Enum S1X8 for value: s1.x8
    /// </summary>
    [EnumMember(Value = "s1.x8")]
    S1X8 = 4,

    /// <summary>
    /// Enum P1X1 for value: p1.x1
    /// </summary>
    [EnumMember(Value = "p1.x1")]
    P1X1 = 5,

    /// <summary>
    /// Enum P1X2 for value: p1.x2
    /// </summary>
    [EnumMember(Value = "p1.x2")]
    P1X2 = 6,

    /// <summary>
    /// Enum P1X4 for value: p1.x4
    /// </summary>
    [EnumMember(Value = "p1.x4")]
    P1X4 = 7,

    /// <summary>
    /// Enum P1X8 for value: p1.x8
    /// </summary>
    [EnumMember(Value = "p1.x8")]
    P1X8 = 8,

    /// <summary>
    /// Enum P2X1 for value: p2.x1
    /// </summary>
    [EnumMember(Value = "p2.x1")]
    P2X1 = 9,

    /// <summary>
    /// Enum P2X2 for value: p2.x2
    /// </summary>
    [EnumMember(Value = "p2.x2")]
    P2X2 = 10,

    /// <summary>
    /// Enum P2X4 for value: p2.x4
    /// </summary>
    [EnumMember(Value = "p2.x4")]
    P2X4 = 11,

    /// <summary>
    /// Enum P2X8 for value: p2.x8
    /// </summary>
    [EnumMember(Value = "p2.x8")]
    P2X8 = 12

}

public class PodTypeJsonConverter : JsonConverter<PodType>
{
    public override PodType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? stringValue = reader.GetString();

        foreach (object? enumValue in from object? enumValue in Enum.GetValues(typeToConvert)
                                      let enumMemberAttr = typeToConvert.GetMember(enumValue.ToString())[0]
                                          .GetCustomAttribute(typeof(EnumMemberAttribute)) as EnumMemberAttribute
                                      where enumMemberAttr != null && enumMemberAttr.Value == stringValue
                                      select enumValue)
        {
            return (PodType)enumValue;
        }
        throw new JsonException($"Unable to parse '{stringValue}' as a PodType enum.");
    }

    public override void Write(Utf8JsonWriter writer, PodType value, JsonSerializerOptions options)
    {
        EnumMemberAttribute? enumMemberAttr = value.GetType().GetMember(value.ToString())[0].GetCustomAttribute(typeof(EnumMemberAttribute)) as EnumMemberAttribute;

        if (enumMemberAttr != null)
        {
            writer.WriteStringValue(enumMemberAttr.Value);
        }
        else
        {
            throw new JsonException($"Unable to find EnumMember attribute for PodType '{value}'.");
        }
    }
}
