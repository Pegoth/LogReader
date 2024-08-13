using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogReader.Pages;

public partial class Home : IDisposable
{
    private readonly List<LogEntry> _logs = [];

    private bool      _loading;
    private string?   _logFile;
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

    public void Dispose() => App.OnGlobalKeyup -= OnGlobalKeyup;

    protected override void OnInitialized() => App.OnGlobalKeyup += OnGlobalKeyup;

    private async Task OnGlobalKeyup(KeyboardEventArgs e)
    {
        if (_loading)
            return;

        if (e is {Key: "v", CtrlKey: true, ShiftKey: false, AltKey: false, MetaKey: false})
            await LoadFromClipboard();
    }

    private async Task LogFileKeypress(KeyboardEventArgs e)
    {
        if (_loading)
            return;

        if (e.Key == "Enter")
        {
            await Runtime.InvokeVoidAsync("document.activeElement.blur");
            await LoadFile();
        }
    }

    private async Task ShowFileSelect()
    {
        using var selector = new OpenFileDialog();
        selector.CheckFileExists = true;
        selector.CheckPathExists = true;

        if (!string.IsNullOrEmpty(_logFile))
            selector.InitialDirectory = Path.GetDirectoryName(_logFile);

        if (selector.ShowDialog() != DialogResult.OK)
            return;

        _logFile = selector.FileName;
        await LoadFile();
    }

    private async Task LoadFile()
    {
        if (string.IsNullOrWhiteSpace(_logFile) || !File.Exists(_logFile))
        {
            _logs.Clear();
            await InvokeAsync(StateHasChanged);
            return;
        }

        await using var stream = File.Open(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await LoadStream(stream);
    }

    private async Task LoadFromClipboard()
    {
        // Try to read as file and stop at the first successful read
        if (Clipboard.ContainsFileDropList())
            foreach (var file in Clipboard.GetFileDropList())
            {
                if (!File.Exists(file))
                    continue;

                _logFile = file;
                await LoadFile();
                if (_logs.Count > 0)
                    return;
            }

        // Try to read as text
        var clipText = Clipboard.GetText();
        if (!string.IsNullOrEmpty(clipText))
        {
            if (File.Exists(clipText))
            {
                _logFile = clipText;
                await LoadFile();
            }
            else
            {
                _logFile = null;

                using var stream = new MemoryStream();
                await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    await writer.WriteAsync(clipText);
                }

                stream.Position = 0;
                await LoadStream(stream);
            }
        }

        // Try to read as stream
        var data = Clipboard.GetData("FileContents");
        if (data is Stream dataStream)
        {
            _logFile = null;
            await LoadStream(dataStream);
            await dataStream.DisposeAsync();
        }
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

    private async Task LoadStream(Stream sourceStream)
    {
        try
        {
            _loading = true;
            _logs.Clear();
            await InvokeAsync(StateHasChanged);

            using var stream = new StreamReader(sourceStream, Encoding.UTF8);

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
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static int GetRowCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 1;

        return Regex.Matches(text, @"\r?\n").Count + 2;
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
        public DateTime? Timestamp         { get; } = Read<DateTime>(jobj, "Timestamp", "@t");
        public string?   Level             { get; } = Read<string>(jobj, "Level", "@l");
        public string?   RenderedMessage   { get; } = Read<string>(jobj, "RenderedMessage", "@m");
        public string?   Exception         { get; } = Read<string>(jobj, "Exception", "@x");
        public string    Raw               { get; } = jobj.ToString(Formatting.Indented);
        public bool      Expanded          { get; set; }
        public bool      ExceptionExpanded { get; set; } = true;
        public bool      RawExpanded       { get; set; }

        public string Color => LevelValue(Level) switch
        {
            0 => "gray",
            1 => "black",
            2 => "orange",
            3 => "#FF8000",
            4 => "red",
            5 => "red",
            _ => "black"
        };

        public bool IsBold => LevelValue(Level) >= 5;

        private static T? Read<T>(JObject jobj, params string[] keys)
        {
            foreach (var key in keys)
                if (jobj.TryGetValue(key, out var value))
                    return value.Value<T>();
                else if (jobj.TryGetValue(key.ToLower(), out var lowerValue))
                    return lowerValue.Value<T>();

            return default;
        }
    }
}