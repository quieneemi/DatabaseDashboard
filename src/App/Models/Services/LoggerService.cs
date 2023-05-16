using Info21v3.Models.Interfaces;

namespace Info21v3.Models.Services;

public class LoggerService : ILoggerService
{
    private const string LogDirectory = "logs";
    private readonly string _filePath;

    public LoggerService()
    {
        var directory = Directory.CreateDirectory(LogDirectory);
        var date = DateTime.Now;
        var files = directory.EnumerateFiles()
            .Select(x => x)
            .Where(x => x.Name.StartsWith($"logs_{date:dd-MM-yy}"))
            .ToArray();
        if (files.Length > 0)
        {
            _filePath = files[0].FullName;
        }
        else
        {
            var fileName = $"logs_{date:dd-MM-yy-hh-mm-ss}";
            _filePath = Path.Combine(LogDirectory, fileName);
            File.Create(_filePath).Close();
        }
    }

    public async Task InfoAsync(string text)
    {
        await using var stream = new StreamWriter(_filePath, true);
        await stream.WriteLineAsync("[INFO]: " + text);
    }

    public void Info(string text)
    {
        using var stream = new StreamWriter(_filePath, true);
        stream.WriteLine("[INFO]: " + text);
    }

    public async Task WarningAsync(string text)
    {
        await using var stream = new StreamWriter(_filePath, true);
        await stream.WriteLineAsync("[WARNING]: " + text);
    }

    public void Warning(string text)
    {
        using var stream = new StreamWriter(_filePath, true);
        stream.WriteLine("[WARNING]: " + text);
    }

    public async Task ErrorAsync(string text)
    {
        await using var stream = new StreamWriter(_filePath, true);
        await stream.WriteLineAsync("[ERROR]: " + text);
    }

    public void Error(string text)
    {
        using var stream = new StreamWriter(_filePath, true);
        stream.WriteLine("[ERROR]: " + text);
    }
}
