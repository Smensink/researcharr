using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(48)]
    public class AddOpenAlexConcepts : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("OpenAlexConcepts")
                .WithColumn("OpenAlexId").AsString().NotNullable().Unique()
                .WithColumn("DisplayName").AsString().NotNullable()
                .WithColumn("Description").AsString().Nullable()
                .WithColumn("Level").AsInt32().NotNullable()
                .WithColumn("CitedByCount").AsInt32().NotNullable()
                .WithColumn("WorksCount").AsInt32().NotNullable();

            // Add index on DisplayName for fast autocomplete search
            Create.Index("IX_OpenAlexConcepts_DisplayName").OnTable("OpenAlexConcepts").OnColumn("DisplayName");
        }
    }
}
