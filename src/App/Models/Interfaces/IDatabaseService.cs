namespace Info21v3.Models.Interfaces;

public interface IDatabaseService
{
    public Task<Table?> RunQueryAsync(string query);
    public Task<string[]> GetListOfTablesAsync();
    public Task<Table?> GetTableAsync(string tableName);
    public Task<Record?> GetRecordAsync(string tableName, string keyValue);
    public Task<bool> CreateAsync(string tableName, string[] keys, string[] values);
    public Task<bool> UpdateAsync(string tableName, string[] keys, string[] values);
    public Task<bool> DeleteAsync(string tableName, string keyValue);
    public Task<Table?> GetRoutinesAsync();
    public Task<Record?> GetRoutineAsync(string routineName);
    public Task<Table?> ExecuteRoutineAsync(string routineName, string[] keys, string[] values);
    public Task<string> ExportTableAsync(Table table);
    public Task<bool> ImportTableAsync(string tableName, string fileName);
}
