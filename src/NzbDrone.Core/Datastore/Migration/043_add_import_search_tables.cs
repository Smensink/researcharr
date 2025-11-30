using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(043)]
    public class add_import_search_tables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("ImportSearchJobs")
                  .WithColumn("Name").AsString().NotNullable()
                  .WithColumn("Source").AsInt32().NotNullable()
                  .WithColumn("Status").AsInt32().NotNullable()
                  .WithColumn("Message").AsString().Nullable()
                  .WithColumn("Total").AsInt32().NotNullable()
                  .WithColumn("Matched").AsInt32().NotNullable()
                  .WithColumn("Queued").AsInt32().NotNullable()
                  .WithColumn("Completed").AsInt32().NotNullable()
                  .WithColumn("Failed").AsInt32().NotNullable()
                  .WithColumn("Created").AsDateTime().NotNullable()
                  .WithColumn("Started").AsDateTime().Nullable()
                  .WithColumn("Ended").AsDateTime().Nullable();

            Create.TableForModel("ImportSearchItems")
                  .WithColumn("JobId").AsInt32().NotNullable().Indexed()
                  .WithColumn("Title").AsString().Nullable()
                  .WithColumn("Authors").AsString().Nullable()
                  .WithColumn("Doi").AsString().Nullable()
                  .WithColumn("Pmid").AsString().Nullable()
                  .WithColumn("Status").AsInt32().NotNullable()
                  .WithColumn("Message").AsString().Nullable()
                  .WithColumn("BookId").AsInt32().Nullable();
        }
    }
}
