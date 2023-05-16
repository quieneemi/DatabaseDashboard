using Info21v3.Models;
using Info21v3.Models.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Info21v3.Controllers;

public class OperationsController : Controller
{
    private readonly IDatabaseService _database;

    public OperationsController(IDatabaseService database) => _database = database;

    #region Console

    public IActionResult Console()
    {
        return View(new ConsoleViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Console(ConsoleViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Query)) return View("_Error");

        var table = await _database.RunQueryAsync(model.Query);

        if (table is null) return View("_Error");
        if (table.Columns.Length == 0) return View("Success");

        table.TableName = Path.GetRandomFileName();
        return PartialView("_Table", table);
    }

    #endregion

    #region Routines

    public async Task<IActionResult> Routines()
    {
        var table = await _database.GetRoutinesAsync();
        return table is null ? View("_Error") : View(table);
    }

    public async Task<IActionResult> ExecuteRoutine(string routineName, string description)
    {
        if (string.IsNullOrWhiteSpace(routineName) || string.IsNullOrWhiteSpace(description))
            return View("_Error");

        ViewBag.Description = description;
        var routine = await _database.GetRoutineAsync(routineName);
        return routine is null ? View("_Error") : View(routine);
    }

    [HttpPost]
    public async Task<IActionResult> ExecuteRoutine(Record routine)
    {
        var result = await _database.ExecuteRoutineAsync(routine.TableName, routine.Keys, routine.Values);
        if (result is null) return View("_Error");
        result.TableName = Path.GetRandomFileName();
        return result.Columns.Length == 0 ? View("Success") : View("_Table", result);
    }

    #endregion

    #region Files

    [HttpPost]
    public async Task<IActionResult> Export(Table table)
    {
        var filePath = await _database.ExportTableAsync(table);
        if (string.IsNullOrWhiteSpace(filePath)) return View("_Error");

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(fileBytes, "text/csv", table.TableName + ".csv");
    }

    [HttpPost]
    public async Task<IActionResult> Import(string tableName, IFormFile? file)
    {
        if (file is null || file.Length <= 0) return View("_Error");

        var fileName = Path.GetRandomFileName() + ".csv";
        var filePath = Path.Combine("/var/lib/postgresql/data", fileName);
        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);
        stream.Close();

        var importResult = await _database.ImportTableAsync(tableName, fileName);
        if (importResult is false) return View("_Error");

        return RedirectToAction("Tables", "Data", new { tableName });
    }

    #endregion
}