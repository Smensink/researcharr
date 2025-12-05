using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(044)]
    public class add_saved_searches : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("SavedSearches")
                  .WithColumn("Name").AsString().NotNullable()
                  .WithColumn("SearchString").AsString().Nullable()
                  .WithColumn("FilterString").AsString().Nullable()
                  .WithColumn("SortString").AsString().Nullable()
                  .WithColumn("Cursor").AsString().Nullable()
                  .WithColumn("CreatedAt").AsDateTime().NotNullable()
                  .WithColumn("LastRunAt").AsDateTime().Nullable()
                  .WithColumn("MeshJson").AsString().Nullable()
                  .WithColumn("PubMedQuery").AsString().Nullable();
        }
    }
}
