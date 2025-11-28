using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(042)]
    public class add_author_metadata_type : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add Type column to AuthorMetadata table
            // 0 = Person (default for existing researchers)
            // 1 = Journal
            Alter.Table("AuthorMetadata").AddColumn("Type").AsInt32().WithDefaultValue(0);
        }
    }
}
