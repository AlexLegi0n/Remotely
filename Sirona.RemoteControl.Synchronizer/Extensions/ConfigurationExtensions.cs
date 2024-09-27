namespace Sirona.RemoteControl.Synchronizer.Extensions;

public static class ConfigurationExtensions
{
    public static string GetRequiredValue(this IConfiguration configuration, string name)
    {
        return configuration[name] ??
               throw new InvalidOperationException(
                   $"Configuration missing value for: {(configuration is IConfigurationSection s ? s.Path + ":" + name : name)}");
    }

    public static T GetRequiredValue<T>(this IConfiguration configuration, string name)
    {
        return configuration.GetValue<T>(name) ??
               throw new InvalidOperationException(
                   $"Configuration missing value for: {(configuration is IConfigurationSection s ? s.Path + ":" + name : name)}");
    }

    public static string GetRequiredConnectionString(this IConfiguration configuration, string name)
    {
        return configuration.GetConnectionString(name) ??
               throw new InvalidOperationException(
                   $"Configuration missing value for: {(configuration is IConfigurationSection s ? s.Path + ":ConnectionStrings:" + name : "ConnectionStrings:" + name)}");
    }
}