namespace Info21v3.Models;

public class Table
{
    public string TableName { get; set; }
    public bool Editable { get; set; }
    public int PrimaryKeyColumnId { get; set; }
    public string[] Columns { get; set; }
    public string[][] Rows { get; set; }

    public Table()
    {
        TableName = string.Empty;
        Columns = Array.Empty<string>();
        Rows = Array.Empty<string[]>();
    }

    public Table(string[] columns, string[][] rows)
    {
        TableName = string.Empty;
        Columns = columns;
        Rows = rows;
    }
}