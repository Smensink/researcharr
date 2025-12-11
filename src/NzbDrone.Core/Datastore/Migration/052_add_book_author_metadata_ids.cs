using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(52)]
    public class AddBookAuthorMetadataIds : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add column to store list of author metadata IDs for each book
            // This allows us to track all authors for a paper without needing to fetch from metadata source
            Alter.Table("Books").AddColumn("AuthorMetadataIds").AsString().Nullable();
        }
    }
}


