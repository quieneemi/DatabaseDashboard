using System.Data;
using Info21v3.Models.Interfaces;
using Npgsql;

namespace Info21v3.Models.Services;

public class DatabaseService : IDatabaseService
{
    private readonly NpgsqlConnection _connection;
    private readonly ILoggerService _logger;

    public DatabaseService(NpgsqlConnection connection, ILoggerService loggerService)
    {
        _connection = connection;
        _logger = loggerService;
        try
        {
            _connection.Open();
        }
        catch
        {
            _logger.Error("Failed to connect as a database");
            Environment.Exit(1);
        }
        _connection.Close();
    }

    public async Task<Table?> RunQueryAsync(string query)
    {
        await _logger.InfoAsync($"RunQueryAsync(string query): query = {query}");
        
        var dataTable = new DataTable();
        await _connection.OpenAsync();

        try
        {
            new NpgsqlDataAdapter(query, _connection)
                .Fill(dataTable);
        }
        catch
        {
            await _logger.WarningAsync($"Incorrect query: {query}");
            return null;
        }
        finally
        {
            await _connection.CloseAsync();
        }

        return new Table
        {
            Columns = (from DataColumn column in dataTable.Columns select column.ColumnName).ToArray(),
            Rows = (from DataRow row in dataTable.Rows
                    select row.ItemArray
                        .Select(cell => cell?.ToString() ?? string.Empty).ToArray()).ToArray()
        };
    }

    public async Task<string[]> GetListOfTablesAsync()
    {
        await _logger.InfoAsync("GetListOfTablesAsync()");
        
        const string query = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'";
        await _connection.OpenAsync();
        var result = new List<string>();
        await using var command = new NpgsqlCommand(query, _connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            result.Add((string)row[0]);
        }
        await _connection.CloseAsync();
        return result.ToArray();
    }

    public async Task<Table?> GetTableAsync(string tableName)
    {
        await _logger.InfoAsync($"GetTableAsync(string tableName): tableName = {tableName}");
        
        var table = await RunQueryAsync($"SELECT * FROM {tableName}");

        if (table is null) return null;

        table.TableName = tableName;
        table.PrimaryKeyColumnId = await GetTablePrimaryKeyColumnIdAsync(table);
        return table;
    }

    public async Task<Record?> GetRecordAsync(string tableName, string keyValue)
    {
        await _logger.InfoAsync($"GetRecordAsync(string tableName): tableName = {tableName}, keyValue = {keyValue}");
        
        var keyColumnName = await GetTablePrimaryKeyColumnName(tableName);
        var table = await RunQueryAsync($"SELECT * FROM {tableName} WHERE {keyColumnName} = '{keyValue}'");

        if (table is null) return null;

        return new Record
        {
            TableName = tableName,
            PrimaryKeyColumnId = await GetTablePrimaryKeyColumnIdAsync(table, keyColumnName),
            Keys = table.Columns,
            Values = table.Rows.FirstOrDefault(new string[table.Columns.Length])
        };
    }

    public async Task<bool> CreateAsync(string tableName, string[] keys, string[] values)
    {
        await _logger.InfoAsync(
            $"CreateAsync(string tableName, string[] keys, string[] values): tableName = {tableName}, keys = {keys}, values = {values}");
        
        var keysStr = string.Empty;
        var valuesStr = string.Empty;

        for (var i = 0; i < keys.Length; ++i)
        {
            keysStr += $"\"{keys[i]}\", ";
            valuesStr += $"'{values[i]}', ";
        }

        if (keysStr.Length > 1)
            keysStr = keysStr.Remove(keysStr.Length - 2);
        if (valuesStr.Length > 1)
            valuesStr = valuesStr.Remove(valuesStr.Length - 2);

        var query = $"INSERT INTO {tableName} ({keysStr}) VALUES ({valuesStr})";
        var result = await RunQueryAsync(query);
        return result is not null;
    }

    public async Task<bool> UpdateAsync(string tableName, string[] keys, string[] values)
    {
        await _logger.InfoAsync(
            $"UpdateAsync(string tableName, string[] keys, string[] values): tableName = {tableName}, keys = {keys}, values = {values}");
        
        var keyColumnName = await GetTablePrimaryKeyColumnName(tableName);
        var keyColumnId = 0;

        var newValues = string.Empty;
        for (var i = 0; i < keys.Length; i++)
        {
            if (keys[i] == keyColumnName)
            {
                keyColumnId = i;
                continue;
            }
            newValues += $"\"{keys[i]}\" = '{values[i]}', ";
        }

        if (newValues.EndsWith(", ")) newValues = newValues.Remove(newValues.Length - 2);

        var query = $"UPDATE {tableName} SET {newValues} WHERE {keyColumnName} = '{values[keyColumnId]}'";
        var result = await RunQueryAsync(query);
        return result is not null;
    }

    public async Task<bool> DeleteAsync(string tableName, string keyValue)
    {
        await _logger.InfoAsync(
            $"DeleteAsync(string tableName, string keyValue): tableName = {tableName}, keyValue = {keyValue}");
        
        var keyColumnName = await GetTablePrimaryKeyColumnName(tableName);
        var query = $"DELETE FROM {tableName} WHERE {keyColumnName} = '{keyValue}'";
        var result = await RunQueryAsync(query);
        return result is not null;
    }

    public async Task<Table?> GetRoutinesAsync()
    {
        await _logger.InfoAsync("GetRoutinesAsync()");
        
        const string query = @"
            WITH t AS (SELECT p.proname AS routine, obj_description(p.oid) AS description
            FROM pg_catalog.pg_namespace n
                JOIN pg_catalog.pg_proc p ON p.pronamespace = n.oid
                    WHERE n.nspname = 'public')
            SELECT * FROM t
            WHERE description IS NOT NULL;";
        return await RunQueryAsync(query);
    }

    public async Task<Record?> GetRoutineAsync(string routineName)
    {
        await _logger.InfoAsync($"GetRoutineAsync(string routineName): routineName = {routineName}");
        
        var request = @$"
                SELECT parameters.parameter_name
                FROM information_schema.routines
                    LEFT JOIN information_schema.parameters ON routines.specific_name=parameters.specific_name
                WHERE routines.specific_schema='public' AND
                      parameters.parameter_mode='IN' AND
                      routines.routine_name='{routineName.ToLower()}' AND
                      parameters.data_type!='refcursor'";

        var table = await RunQueryAsync(request);
        return table is null ? null : new Record(routineName, table.Rows.Select(x => x[0]).ToArray());
    }

    public async Task<Table?> ExecuteRoutineAsync(string routineName, string[] keys, string[] values)
    {
        await _logger.InfoAsync(
            $"ExecuteRoutineAsync(string routineName, string[] keys, string[] values): routineName = {routineName}, keys = {keys}, values = {values}");
        
        var type = await GetRoutineTypeAsync(routineName);
        string request;
        var valuesStr = string.Empty;
        for (var i = 0; i < keys.Length; ++i)
        {
            valuesStr += $"{keys[i]} => '{values[i]}', ";
        }
        if (valuesStr.Length > 1)
            valuesStr = valuesStr.Remove(valuesStr.Length - 2);

        if (type == "f")
        {
            request = $"SELECT * FROM {routineName}({valuesStr})";
        }
        else if (type == "p")
        {
            var refCursorName = await GetRoutineRefCursorNameAsync(routineName);
            if (string.IsNullOrEmpty(refCursorName))
            {
                request = $"CALL {routineName}({valuesStr})";
            }
            else
            {
                request = @$"
                    BEGIN;
                    CALL {routineName}({refCursorName} => 'cursor' {(!string.IsNullOrEmpty(valuesStr) ? ", " : " ")} {valuesStr});
                    FETCH ALL IN ""cursor"";
                    COMMIT;";
            }
        }
        else
        {
            await _logger.WarningAsync($"Unknown type: {type}");
            return new Table();
        }
        return await RunQueryAsync(request);
    }

    public async Task<string> ExportTableAsync(Table table)
    {
        await _logger.InfoAsync($"ExportTableAsync(Table table): table = {table}");
        
        var filePath = Path.Combine(await GetDataDirectoryAsync(), table.TableName) + ".csv";

        var internalTables = await GetListOfTablesAsync();
        if (internalTables.Contains(table.TableName))
        {
            var exportResult = await ExportInternalTableAsync(table.TableName);
            if (exportResult is false)
                return string.Empty;
        }
        else
        {
            await ExportCustomTableAsync(table, filePath);
        }

        return filePath;
    }

    public async Task<bool> ImportTableAsync(string tableName, string fileName)
    {
        await _logger.InfoAsync(
            $"ImportTableAsync(string tableName, string fileName): tableName = {tableName}, fileName = {fileName}");
        
        var query = $"CALL import_table('{fileName}', '{tableName}', ',')";
        var result = await RunQueryAsync(query);
        return result is not null;
    }

    private async Task<int> GetTablePrimaryKeyColumnIdAsync(Table table)
    {
        await _logger.InfoAsync($"GetTablePrimaryKeyColumnId(Table table): table = {table}");
        
        var keyColumnName = await GetTablePrimaryKeyColumnName(table.TableName);
        return await GetTablePrimaryKeyColumnIdAsync(table, keyColumnName);
    }

    private async Task<int> GetTablePrimaryKeyColumnIdAsync(Table table, string keyColumnName)
    {
        await _logger.InfoAsync(
            $"GetTablePrimaryKeyColumnId(Table table, string keyColumnName): table = {table}, keyColumnName = {keyColumnName}");
        
        return table.Columns.TakeWhile(column => column != keyColumnName).Count();
    }

    private async Task<string> GetTablePrimaryKeyColumnName(string tableName)
    {
        await _logger.InfoAsync($"GetTablePrimaryKeyColumnName(string tableName): tableName = {tableName}");
        
        var query = "SELECT pg_attribute.attname " +
                    "FROM pg_index, pg_class, pg_attribute, pg_namespace " +
                    $"WHERE pg_class.oid = '{tableName}'::regclass " +
                    "AND indrelid = pg_class.oid " +
                    "AND nspname = 'public' " +
                    "AND pg_class.relnamespace = pg_namespace.oid " +
                    "AND pg_attribute.attrelid = pg_class.oid " +
                    "AND pg_attribute.attnum = any (pg_index.indkey) " +
                    "AND indisprimary";

        await _connection.OpenAsync();
        await using var command = new NpgsqlCommand(query, _connection);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var row = new object[reader.FieldCount];
        reader.GetValues(row);
        await _connection.CloseAsync();

        return (string)row[0];
    }

    private async Task<string> GetDataDirectoryAsync()
    {
        await _logger.InfoAsync("GetDataDirectoryAsync()");
        
        const string query = "SHOW data_directory";
        await _connection.OpenAsync();
        await using var command = new NpgsqlCommand(query, _connection);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var result = reader.GetString(0);
        await _connection.CloseAsync();
        return result;
    }

    private async Task<bool> ExportInternalTableAsync(string tableName)
    {
        await _logger.InfoAsync($"GetRecordAsync(string tableName): tableName = {tableName}");
        
        var query = $"CALL export_table('{tableName}.csv', '{tableName}', ',')";
        var result = await RunQueryAsync(query);
        return result is not null;
    }

    private async Task ExportCustomTableAsync(Table table, string filePath)
    {
        await _logger.InfoAsync(
            $"ExportCustomTableAsync(Table table, string filePath): table = {table}, filePath = {filePath}");
        
        await using var writer = new StreamWriter(filePath, false);

        var line = table.Columns.Aggregate(string.Empty, (current, column) => current + (column + ','));
        if (line.Length > 0)
            line = line.Remove(line.Length - 1);

        await writer.WriteLineAsync(line);

        foreach (var row in table.Rows)
        {
            line = row.Aggregate(string.Empty, (current, cell) => current + (cell + ','));
            if (line.Length > 0)
                line = line.Remove(line.Length - 1);

            await writer.WriteLineAsync(line);
        }
    }

    private async Task<string> GetRoutineRefCursorNameAsync(string name)
    {
        await _logger.InfoAsync($"GetRoutineRefCursorNameAsync(string name): name = {name}");
        
        var request = @$"
            SELECT parameters.parameter_name
            FROM information_schema.routines
                LEFT JOIN information_schema.parameters ON routines.specific_name=parameters.specific_name
            WHERE routines.specific_schema='public' AND
                  parameters.parameter_mode='IN' AND
                  routines.routine_name='{name.ToLower()}' AND
                  parameters.data_type='refcursor'";
        var table = await RunQueryAsync(request);
        return table!.Rows.Length > 0 ? table.Rows[0][0] : "";
    }

    private async Task<string> GetRoutineTypeAsync(string name)
    {
        await _logger.InfoAsync($"GetRoutineTypeAsync(string name): name = {name}");
        var request = @$"
                SELECT
                    p.prokind
                FROM
                    pg_catalog.pg_namespace n
                JOIN pg_catalog.pg_proc p
                    ON p.pronamespace = n.oid AND
                       n.nspname = 'public' AND
                       p.proname = '{name.ToLower()}'";
        var table = await RunQueryAsync(request);
        return table!.Rows.Length > 0 ? table.Rows[0][0] : "";
    }
}
