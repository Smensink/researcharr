using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(041)]
    public class add_http_download_items : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("HttpDownloadItems")
                .WithColumn("DownloadId").AsString().NotNullable().Unique()
                .WithColumn("Title").AsString().NotNullable()
                .WithColumn("OutputPath").AsString().NotNullable()
                .WithColumn("TotalSize").AsInt64().WithDefaultValue(0)
                .WithColumn("Status").AsInt32().NotNullable()
                .WithColumn("Message").AsString().Nullable()
                .WithColumn("DateAdded").AsDateTime().NotNullable()
                .WithColumn("DownloadClientId").AsInt32().NotNullable();
        }
    }
}
