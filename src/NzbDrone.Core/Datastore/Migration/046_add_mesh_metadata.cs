using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(046)]
    public class add_mesh_metadata : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("MeshMetadata")
                  .WithColumn("SourceUrl").AsString().Nullable()
                  .WithColumn("Version").AsString().Nullable()
                  .WithColumn("ImportedAt").AsDateTime().NotNullable();
        }
    }
}
