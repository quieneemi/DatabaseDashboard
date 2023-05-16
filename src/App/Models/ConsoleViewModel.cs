namespace Info21v3.Models;

public class ConsoleViewModel
{
    public string Query { get; set; }

    public ConsoleViewModel() => Query = string.Empty;

    public ConsoleViewModel(string query) => Query = query;
}