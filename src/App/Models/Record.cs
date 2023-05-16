namespace Info21v3.Models;

public class Record
{
    public string TableName { get; set; }
    public int PrimaryKeyColumnId { get; set; }
    public string[] Keys { get; set; }
    public string[] Values { get; set; }

    public Record()
    {
        TableName = string.Empty;
        Keys = Array.Empty<string>();
        Values = Array.Empty<string>();
    }

    public Record(string tableName, string[] keys)
    {
        TableName = tableName;
        Keys = keys;
        Values = new string[keys.Length];
    }

    public Record(string tableName, int primaryKeyColumnId, string[] keys, string[] values)
    {
        TableName = tableName;
        PrimaryKeyColumnId = primaryKeyColumnId;
        Keys = keys;
        Values = values;
    }
}