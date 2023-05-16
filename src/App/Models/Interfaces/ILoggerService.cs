namespace Info21v3.Models.Interfaces;

public interface ILoggerService
{
    public Task InfoAsync(string text);
    public Task WarningAsync(string text);
    public Task ErrorAsync(string text);
    public void Info(string text);
    public void Warning(string text);
    public void Error(string text);
}
