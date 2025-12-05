using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(47)]
    public class AddMeshTermIndex : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add index on Term column for faster LIKE queries
            Create.Index("IX_MeshTerms_Term").OnTable("MeshTerms").OnColumn("Term");
        }
    }
}
