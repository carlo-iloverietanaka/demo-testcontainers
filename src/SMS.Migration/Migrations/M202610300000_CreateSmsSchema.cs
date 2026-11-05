using FluentMigrator;

namespace SMS.Migration.Migrations;

[Migration(202610300000)]
public class M202610300000_CreateSmsSchema : FluentMigrator.Migration
{
    public override void Up()
    {
        Create.Schema("messaging");
    }

    public override void Down()
    {
        Delete.Schema("messaging");
    }
}
