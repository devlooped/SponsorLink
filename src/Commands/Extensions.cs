using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;
using Spectre.Console.Rendering;

namespace Devlooped.Sponsors;

public static class Extensions
{
    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> values) where T : notnull
    {
        foreach (var value in values)
            collection.Add(value);
    }

    public static ICommandConfigurator AddCommand<TCommand>(this IConfigurator configurator)
        where TCommand : class, ICommand
    {
        var name = typeof(TCommand).Name.Replace("Command", "").ToLowerInvariant();
        return configurator.AddCommand<TCommand>(name);
    }

    public static IRenderable ToDetails(this JwtSecurityToken jwt, string path)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink");
        var header = $"|:magnifying_glass_tilted_left:[link={path}]~{Path.DirectorySeparatorChar}.sponsorlink{path[root.Length..]}[/] |";

        var content = new List<IRenderable>
        { 
            new JsonText(jwt.Payload.SerializeToJson()) 
        };

        if (jwt.Claims.Where(c => c.Type == "client_id").Select(c => c.Value).FirstOrDefault() is string client_id)
            content.Add(new Markup($":backhand_index_pointing_right: [link=https://github.com/settings/connections/applications/{client_id}]Review permissions :globe_showing_americas: github.com[/]"));

        return new Panel(new Rows(content))
        {
            Header = new PanelHeader(header),
        };
    }

    public static Table AsTable<T>(this IEnumerable<T> items)
    {
        var table = new Table();
        var props = TypeDescriptor.GetProperties(typeof(T)).Cast<PropertyDescriptor>().Where(x => x.IsBrowsable).ToList();

        foreach (var prop in props)
        {
            var name = prop.DisplayName;
            if (!name.Contains(' '))
            {
                // Separate words by upper case letters
                var sb = new StringBuilder();
                foreach (var c in name)
                {
                    if (char.IsUpper(c))
                        sb.Append(' ');
                    sb.Append(c);
                }
                name = sb.ToString().Trim();
            }

            Action<TableColumn>? configure = null;

            if (prop.PropertyType == typeof(DateTime)
#if NET6_0_OR_GREATER
                || prop.PropertyType == typeof(DateOnly))
#else
                )
#endif
                configure = c => c.Centered();

            if (prop.PropertyType == typeof(bool))
                configure = c => c.Centered();

            if (prop.PropertyType == typeof(int))
                configure = c => c.RightAligned();

            table.AddColumn(name, configure);
        }

        var values = new List<string>();
        foreach (var item in items)
        {
            values.Clear();
            foreach (var prop in props)
            {
                var value = prop.GetValue(item);
                if (value is DateTime dt)
                    values.Add(dt.ToString("yyyy-MM-dd HH:mm:ss"));
#if NET6_0_OR_GREATER
                else if (value is DateOnly date)
                    values.Add(date.ToString("yyyy-MM-dd"));
#endif
                else if (value is bool b)
                    if (b) values.Add("[green]✔[/]");
                    else values.Add("");
                else
                    values.Add(value?.ToString() ?? "");
            }
            table.AddRow(values.ToArray());
        }

        return table;
    }
}
