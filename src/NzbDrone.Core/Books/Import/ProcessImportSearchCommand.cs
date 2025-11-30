using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Books.Import
{
    public class ProcessImportSearchCommand : Command
    {
        public int JobId { get; set; }

        public ProcessImportSearchCommand()
        {
        }

        public ProcessImportSearchCommand(int jobId)
        {
            JobId = jobId;
        }

        public override bool SendUpdatesToClient => true;
    }
}
