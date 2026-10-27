using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Velqora.Core;
using Velqora.Infrastructure;
await using var db = new SocDb(new DbContextOptionsBuilder<SocDb>()
    .UseNpgsql("Host=localhost;Database=velqora;Username=model-check;Password=unused").Options);
var model = db.Model;
var script = db.Database.GenerateCreateScript();
var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var initial = File.ReadAllText(Path.Combine(root,"src/backend/Velqora.Infrastructure/Migrations/initial.sql"));
int errors = 0, columns = 0, tables = 0;
if(db.Database.HasPendingModelChanges()){Console.Error.WriteLine("EF snapshot does not match the current model.");errors++;}
foreach (var entity in model.GetEntityTypes())
{
    if (entity.ClrType.IsAbstract) { Console.Error.WriteLine($"Unexpected abstract mapped entity: {entity.Name}"); errors++; }
    var table = entity.GetTableName()!;
    var match = Regex.Match(initial, "CREATE TABLE \"" + table + "\" \\(([\\s\\S]*?)\\);");
    if (!match.Success) { Console.Error.WriteLine($"Migration missing model table {table}"); errors++; continue; }
    var expected = entity.GetProperties().Select(p => p.GetColumnName()).ToHashSet();
    var actual = Regex.Matches(match.Groups[1].Value, "\"(\\w+)\"\\s+(?:uuid|integer|timestamp with time zone|timestamptz|boolean|character varying|varchar|text|jsonb)")
        .Select(m => m.Groups[1].Value).ToHashSet();
    foreach (var column in expected.Except(actual)) { Console.Error.WriteLine($"Missing column: {table}.{column}"); errors++; }
    foreach (var column in actual.Except(expected)) { Console.Error.WriteLine($"Unexpected column: {table}.{column}"); errors++; }
    if (typeof(VersionedEntity).IsAssignableFrom(entity.ClrType) &&
        !entity.FindProperty("Version")!.IsConcurrencyToken) { Console.Error.WriteLine($"Missing concurrency token: {table}"); errors++; }
    columns += expected.Count; tables++;
}
var output=Path.Combine(root,".local","model-create.sql");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output,script);
Console.WriteLine($"EF relational model: {tables} tables, {columns} columns, {errors} errors; SQL generation succeeded without a database.");
return errors==0?0:1;
