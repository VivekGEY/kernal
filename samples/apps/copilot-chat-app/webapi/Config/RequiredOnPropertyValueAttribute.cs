﻿// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SemanticKernel.Service.Config;

/// <summary>
/// If the other property is set to the expected value, then this property is required.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
internal sealed class RequiredOnPropertyValueAttribute : ValidationAttribute
{
    /// <summary>
    /// Name of the other property.
    /// </summary>
    public string OtherPropertyName { get; }

    /// <summary>
    /// Value of the other property when this property is required.
    /// </summary>
    public object? OtherPropertyValue { get; }

    /// <summary>
    /// If the other property is set to the expected value, then this property is required.
    /// </summary>
    /// <param name="otherPropertyName">Name of the other property.</param>
    /// <param name="otherPropertyValue">Value of the other property when this property is required.</param>
    public RequiredOnPropertyValueAttribute(string otherPropertyName, object? otherPropertyValue)
    {
        this.OtherPropertyName = otherPropertyName; ;
        this.OtherPropertyValue = otherPropertyValue;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        PropertyInfo? otherPropertyInfo = validationContext.ObjectType.GetRuntimeProperty(this.OtherPropertyName);

        // If the other property is not found, return an error.
        if (otherPropertyInfo == null)
        {
            return new ValidationResult($"Unknown other property name '{this.OtherPropertyName}'.");
        }

        // If the other property is an indexer, return an error.
        if (otherPropertyInfo.GetIndexParameters().Length > 0)
        {
            throw new ArgumentException($"Other property not found ('{validationContext.MemberName}, '{this.OtherPropertyName}').");
        }

        object? otherPropertyValue = otherPropertyInfo.GetValue(validationContext.ObjectInstance, null);

        // If the other property is set to the expected value, then this property is required.
        if (Equals(this.OtherPropertyValue, otherPropertyValue))
        {
            if (value == null)
            {
                return new ValidationResult($"Property '{validationContext.DisplayName}' is required when '{this.OtherPropertyName}' is {this.OtherPropertyValue}.");
            }
            else
            {
                return ValidationResult.Success;
            }
        }

        return ValidationResult.Success;
    }
}
