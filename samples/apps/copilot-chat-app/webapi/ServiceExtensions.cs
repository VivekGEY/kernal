﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using SemanticKernel.Service.Auth;
using SemanticKernel.Service.Options;
using SemanticKernel.Service.Utilities;

namespace SemanticKernel.Service;

internal static class ServicesExtensions
{
    /// <summary>
    /// Parse configuration into options.
    /// </summary>
    internal static IServiceCollection AddOptions(this IServiceCollection services, ConfigurationManager configuration)
    {
        // General configuration
        services.AddOptions<ServiceOptions>()
            .Bind(configuration.GetSection(ServiceOptions.PropertyName))
            .ValidateDataAnnotations()
            .ValidateOnStart()
            .PostConfigure(TrimStringProperties);

        // Default AI service configurations for Semantic Kernel
        services.AddOptions<AIServiceOptions>()
            .Bind(configuration.GetSection(AIServiceOptions.PropertyName))
            .ValidateDataAnnotations()
            .ValidateOnStart()
            .PostConfigure(TrimStringProperties);

        var foo = services.BuildServiceProvider().GetService<IOptions<AIServiceOptions>>();

        // Authorization configuration
        services.AddOptions<ChatAuthenticationOptions>()
            .Bind(configuration.GetSection(ChatAuthenticationOptions.PropertyName))
            .ValidateOnStart()
            .ValidateDataAnnotations()
            .PostConfigure(TrimStringProperties);

        // Memory store configuration
        services.AddOptions<MemoriesStoreOptions>()
            .Bind(configuration.GetSection(MemoriesStoreOptions.PropertyName))
            .ValidateDataAnnotations()
            .ValidateOnStart()
            .PostConfigure(TrimStringProperties);

        return services;
    }

    internal static IServiceCollection AddCopilotChatUtilities(this IServiceCollection services)
    {
        return services.AddScoped<AskConverter>();
    }

    /// <summary>
    /// Add CORS settings.
    /// </summary>
    internal static IServiceCollection AddCors(this IServiceCollection services)
    {
        IConfiguration configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        string[] allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (allowedOrigins is not null && allowedOrigins.Length > 0)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    policy =>
                    {
                        policy.WithOrigins(allowedOrigins)
                            .AllowAnyHeader();
                    });
            });
        }

        return services;
    }

    /// <summary>
    /// Add authentication services
    /// </summary>
    internal static IServiceCollection AddCopilotChatAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAuthInfo, AuthInfo>();
        var config = services.BuildServiceProvider().GetRequiredService<IOptions<ChatAuthenticationOptions>>().Value;
        switch (config.Type)
        {
            case ChatAuthenticationOptions.AuthenticationType.AzureAd:
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(configuration.GetSection($"{ChatAuthenticationOptions.PropertyName}:AzureAd"));
                break;

            case ChatAuthenticationOptions.AuthenticationType.None:
                services.AddAuthentication(PassThroughAuthenticationHandler.AuthenticationScheme)
                    .AddScheme<AuthenticationSchemeOptions, PassThroughAuthenticationHandler>(
                        authenticationScheme: PassThroughAuthenticationHandler.AuthenticationScheme,
                        configureOptions: null);
                break;

            default:
                throw new InvalidOperationException($"Invalid authentication type '{config.Type}'.");
        }

        return services;
    }

    /// <summary>
    /// Trim all string properties, recursively.
    /// </summary>
    private static void TrimStringProperties<T>(T options) where T : class
    {
        Queue<object> targets = new();
        targets.Enqueue(options);

        while (targets.Count > 0)
        {
            object target = targets.Dequeue();
            Type targetType = target.GetType();
            foreach (PropertyInfo property in targetType.GetProperties())
            {
                // Skip enumerations
                if (property.PropertyType.IsEnum)
                {
                    continue;
                }

                // Property is a built-in type, readable, and writable.
                if (property.PropertyType.Namespace == "System" &&
                    property.CanRead &&
                    property.CanWrite)
                {
                    // Property is a non-null string.
                    if (property.PropertyType == typeof(string) &&
                        property.GetValue(target) != null)
                    {
                        property.SetValue(target, property.GetValue(target)!.ToString()!.Trim());
                    }
                }
                else
                {
                    // Property is a non-built-in and non-enum type - queue it for processing.
                    if (property.GetValue(target) != null)
                    {
                        targets.Enqueue(property.GetValue(target)!);
                    }
                }
            }
        }
    }
}
