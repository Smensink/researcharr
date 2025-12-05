using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(49)]
    public class add_indexer_failures : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("IndexerFailure")
                .WithColumn("IndexerId").AsInt32().NotNullable()
                .WithColumn("OperationType").AsInt32().NotNullable()
                .WithColumn("ErrorType").AsInt32().NotNullable()
                .WithColumn("ErrorMessage").AsString().Nullable()
                .WithColumn("HttpStatusCode").AsInt32().Nullable()
                .WithColumn("Timestamp").AsDateTime().NotNullable();

            Create.Index().OnTable("IndexerFailure").OnColumn("IndexerId");
            Create.Index().OnTable("IndexerFailure").OnColumn("Timestamp");
        }
    }
}

