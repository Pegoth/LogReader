using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogReader.Pages;

public partial class Home
{
    private readonly List<LogEntry> _logs = [];

    private string?   _search;
    private string[]? _searchTokens;

    private string? Search
    {
        get => _search;
        set
        {
            if (_search == value)
                return;

            _search = value;
            _searchTokens = value is null
                                ? null
                                : Regex.Split(value, "(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")
                                       .Select(t => t.Trim(' ', '"'))
                                       .ToArray();
        }
    }

    private async Task FileSelected(InputFileChangeEventArgs e)
    {
        _logs.Clear();
        using var stream = new StreamReader(e.File.OpenReadStream(long.MaxValue, CancellationToken.None), Encoding.UTF8);

        while (true)
            try
            {
                var line = await stream.ReadLineAsync();
                if (line == null)
                    break;

                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("{"))
                    continue;

                _logs.Add(new LogEntry(JObject.Parse(line)));
            }
            catch (IOException ex)
            {
                Logger.LogError(ex, "IO Exception while reading log file.");
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to parse log line.");
            }

        await InvokeAsync(StateHasChanged);
    }

    private bool IsFiltered(LogEntry entry)
    {
        if (_searchTokens is null)
            return true;

        foreach (var token in _searchTokens)
        {
            var r = Regex.Match(token, @"^(?<op>\<=?|\>=?|==?|!=)(?<data>.+)$");
            if (r.Success)
            {
                var op         = r.Groups["op"].Value;
                var data       = r.Groups["data"].Value;
                var levelValue = LevelValue(data);

                if (op == "=")
                    op = "==";

                if (levelValue >= 0)
                {
                    var entryLevelValue = LevelValue(entry.Level);
                    switch (op)
                    {
                        case "<" when entryLevelValue >= levelValue: return false;
                        case "<=" when entryLevelValue > levelValue: return false;
                        case ">" when entryLevelValue <= levelValue: return false;
                        case ">=" when entryLevelValue < levelValue: return false;
                        case "==" when entryLevelValue != levelValue: return false;
                        case "!=" when entryLevelValue == levelValue: return false;
                    }
                }
                else if (DateTime.TryParse(data, out var date))
                {
                    switch (op)
                    {
                        case "<" when entry.Timestamp >= date: return false;
                        case "<=" when entry.Timestamp > date: return false;
                        case ">" when entry.Timestamp <= date: return false;
                        case ">=" when entry.Timestamp < date: return false;
                        case "==" when entry.Timestamp != date: return false;
                        case "!=" when entry.Timestamp == date: return false;
                    }
                }
            }
            else if (!(entry.Level?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false) &&
                     !(entry.RenderedMessage?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false) &&
                     !(entry.Exception?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetNumLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return Regex.Matches(text, @"\r?\n").Count + 1;
    }

    private static int LevelValue(string? level) => level?.ToLower() switch
    {
        "verb" => 0,
        "verbose" => 0,
        "trace" => 0,
        "deb" => 1,
        "debug" => 1,
        "inf" => 2,
        "info" => 2,
        "information" => 2,
        "warn" => 3,
        "warning" => 3,
        "err" => 4,
        "error" => 4,
        "fatal" => 5,
        "crit" => 5,
        "critical" => 5,
        _ => -1
    };

    private class LogEntry(JObject jobj)
    {
        public DateTime? Timestamp         { get; } = jobj["Timestamp"]?.Value<DateTime?>() ?? jobj["@t"]?.Value<DateTime?>();
        public string?   Level             { get; } = jobj["Level"]?.Value<string?>() ?? jobj["@l"]?.Value<string?>();
        public string?   RenderedMessage   { get; } = jobj["RenderedMessage"]?.Value<string?>() ?? jobj["@m"]?.Value<string?>();
        public string?   Exception         { get; } = jobj["Exception"]?.Value<string?>() ?? jobj["@x"]?.Value<string?>();
        public string    Raw               { get; } = jobj.ToString(Formatting.Indented);
        public bool      Expanded          { get; set; }
        public bool      ExceptionExpanded { get; set; } = true;
        public bool      RawExpanded       { get; set; }

        public string Color => Level switch
        {
            "Info" => "orange",
            "Information" => "orange",
            "Warn" => "Tomato",
            "Warning" => "Tomato",
            "Error" => "red",
            "Fatal" => "red",
            "Critical" => "red",
            _ => "black"
        };
    }
}