using System.Diagnostics.CodeAnalysis;
using Claude.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the Claude Agent client with a service collection.</summary>
public static class ClaudeAgentServiceCollectionExtensions
{
    /// <summary>Registers a <see cref="ClaudeAgentClient"/> singleton configured by <paramref name="configure"/>.</summary>
    public static IServiceCollection AddClaudeAgentClient(
        this IServiceCollection services,
        Action<ClaudeAgentClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        AddCore(services);
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }

    /// <summary>Registers a <see cref="ClaudeAgentClient"/> singleton configured by <paramref name="configure"/>, which may resolve services.</summary>
    public static IServiceCollection AddClaudeAgentClient(
        this IServiceCollection services,
        Action<ClaudeAgentClientOptions, IServiceProvider> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddCore(services);
        services.AddOptions<ClaudeAgentClientOptions>().Configure<IServiceProvider>(configure);
        return services;
    }

    /// <summary>Registers a <see cref="ClaudeAgentClient"/> singleton bound from <paramref name="section"/>; a value that cannot be mapped fails the options at host start, naming its key.</summary>
    [RequiresUnreferencedCode("Binding ClaudeAgentClientOptions from configuration uses reflection over its properties.")]
    [RequiresDynamicCode("Binding ClaudeAgentClientOptions from configuration may need runtime code generation.")]
    public static IServiceCollection AddClaudeAgentClient(this IServiceCollection services, IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        var path = (section as IConfigurationSection)?.Path ?? string.Empty;

        return services.AddClaudeAgentClient(options =>
        {
            if (section.Get<ClaudeAgentClientOptionsDto>() is { } dto)
            {
                OptionsFromConfiguration.Apply(dto, options, path);
            }
        });
    }

    /// <summary>Serves <typeparamref name="T"/>'s <c>[McpTool]</c> methods to the binary as the MCP server <paramref name="serverName"/>, by adding it to the defaults' <see cref="ClaudeAgentSessionOptions.McpServers"/>, where it stays for every call: a call's own servers are added beside it.</summary>
    [RequiresUnreferencedCode("Tool discovery reflects over T's methods and their parameter types.")]
    public static IServiceCollection AddClaudeTools<T>(
        this IServiceCollection services,
        string serverName,
        string version = "1.0.0")
        where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);

        services.TryAddSingleton<T>();
        services.AddOptions<ClaudeAgentClientOptions>().PostConfigure<IServiceProvider>(
            (options, sp) => options.Defaults.McpServers[serverName] = McpServerConfiguration.InProcess(sp.GetRequiredService<T>(), version));

        return services;
    }

    private static void AddCore(IServiceCollection services)
    {
        services.AddOptions<ClaudeAgentClientOptions>().ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ClaudeAgentClientOptions>, ClaudeAgentClientOptionsValidator>(
                sp => new ClaudeAgentClientOptionsValidator(Host(sp))));

        services.TryAddSingleton(sp => new ClaudeAgentClient(
            sp.GetRequiredService<IOptions<ClaudeAgentClientOptions>>().Value,
            sp.GetService<ILoggerFactory>(),
            null,
            Host(sp)));

        services.TryAddSingleton(sp => sp.GetRequiredService<ClaudeAgentClient>().GetSessionClient());
    }

    // Where the validator and the client read the user's directory and the default configuration
    // directory from: a HostEnvironment registered in the container, which only a test registers, else
    // this process.
    private static HostEnvironment Host(IServiceProvider services) =>
        services.GetService<HostEnvironment>() ?? HostEnvironment.Current;
}
