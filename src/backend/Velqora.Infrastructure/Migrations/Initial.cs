using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Velqora.Infrastructure.Migrations;
[DbContext(typeof(SocDb))]
[Migration("202610260001_Initial")]
public sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        using var stream = typeof(Initial).Assembly.GetManifestResourceStream("Velqora.Infrastructure.Migrations.initial.sql")
            ?? throw new InvalidOperationException("Initial migration resource is missing.");
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Destructive schema rollback is intentionally unsupported. Restore a verified backup.");
}
