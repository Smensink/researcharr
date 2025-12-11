using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Books.Commands
{
    public class MoveFilesToJournalFoldersCommand : Command
    {
        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;
    }
}


