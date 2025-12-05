using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(50)]
    public class AddIndexerSuccesses : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("IndexerSuccess")
                .WithColumn("IndexerId").AsInt32().NotNullable()
                .WithColumn("OperationType").AsInt32().NotNullable()
                .WithColumn("Timestamp").AsDateTime().NotNullable();

            Create.Index("IX_IndexerSuccess_IndexerId").OnTable("IndexerSuccess").OnColumn("IndexerId");
            Create.Index("IX_IndexerSuccess_Timestamp").OnTable("IndexerSuccess").OnColumn("Timestamp");
            Create.Index("IX_IndexerSuccess_IndexerId_OperationType").OnTable("IndexerSuccess").OnColumn("IndexerId").OnColumn("OperationType");
        }
    }
}

