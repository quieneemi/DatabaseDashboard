using Microsoft.AspNetCore.Mvc;
using Info21v3.Models;
using Info21v3.Models.Interfaces;

namespace Info21v3.Controllers;

public class DataController : Controller
{
    private readonly IDatabaseService _database;

    public DataController(IDatabaseService database) => _database = database;

    #region Read

    public async Task<IActionResult> Tables(string? tableName)
    {
        // if no table provided return all available tables
        if (string.IsNullOrWhiteSpace(tableName))
            return View(await _database.GetListOfTablesAsync());

        var table = await _database.GetTableAsync(tableName);

        if (table is null) return View("_Error");

        table.Editable = true;
        return PartialView("_Table", table);
    }

    #endregion

    #region Create

    public async Task<IActionResult> Create(string tableName)
    {
        var table = await _database.GetTableAsync(tableName);

        if (table is null) return View("_Error");

        var model = new Record(tableName, table.Columns);
        return View(model);
    }

    [HttpPost]
    public IActionResult ConfirmCreate(Record record)
    {
        ViewBag.Action = "Create";
        return View("_ModificationConfirmation", record);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Record record)
    {
        var result = await _database.CreateAsync(record.TableName, record.Keys, record.Values);
        return result ? RedirectToAction("Tables", "Data", new { record.TableName }) : View("_Error");
    }

    #endregion

    #region Update

    public async Task<IActionResult> Update(string tableName, string keyValue)
    {
        var record = await _database.GetRecordAsync(tableName, keyValue);
        return record is null ? View("_Error") : View(record);
    }

    [HttpPost]
    public IActionResult ConfirmUpdate(Record record)
    {
        ViewBag.Action = "Update";
        return View("_ModificationConfirmation", record);
    }

    [HttpPost]
    public async Task<IActionResult> Update(Record record)
    {
        var result = await _database.UpdateAsync(record.TableName, record.Keys, record.Values);
        return result ? RedirectToAction("Tables", "Data", new { record.TableName }) : View("_Error");
    }

    #endregion

    #region Delete

    public async Task<IActionResult> Delete(string tableName, string keyValue)
    {
        ViewBag.Action = "Delete";
        var record = await _database.GetRecordAsync(tableName, keyValue);
        return record is null ? View("_Error") : View("_ModificationConfirmation", record);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Record record)
    {
        var result = await _database.DeleteAsync(record.TableName, record.Values[record.PrimaryKeyColumnId]);
        return result ? RedirectToAction("Tables", "Data", new { record.TableName }) : View("_Error");
    }

    #endregion
}