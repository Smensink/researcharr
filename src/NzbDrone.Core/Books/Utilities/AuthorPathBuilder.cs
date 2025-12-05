using System;
using System.IO;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Books
{
    public interface IBuildAuthorPaths
    {
        string BuildPath(Author author, bool useExistingRelativeFolder);
    }

    public class AuthorPathBuilder : IBuildAuthorPaths
    {
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IRootFolderService _rootFolderService;

        public AuthorPathBuilder(IBuildFileNames fileNameBuilder, IRootFolderService rootFolderService)
        {
            _fileNameBuilder = fileNameBuilder;
            _rootFolderService = rootFolderService;
        }

        public string BuildPath(Author author, bool useExistingRelativeFolder)
        {
            if (author.RootFolderPath.IsNullOrWhiteSpace())
            {
                throw new ArgumentException("Root folder was not provided", nameof(author));
            }

            if (useExistingRelativeFolder && author.Path.IsNotNullOrWhiteSpace())
            {
                // Preserve existing paths for backward compatibility (don't move existing authors/journals)
                var relativePath = GetExistingRelativePath(author);
                return Path.Combine(author.RootFolderPath, relativePath);
            }

            // For new authors, separate researchers and journals into subfolders
            var isJournal = author.Metadata?.Value?.Type == AuthorMetadataType.Journal ||
                           string.Equals(author.Metadata?.Value?.Disambiguation, "Journal", StringComparison.InvariantCultureIgnoreCase);
            
            var folderName = _fileNameBuilder.GetAuthorFolder(author);
            var subfolder = isJournal ? "journals" : "authors";
            
            return Path.Combine(author.RootFolderPath, subfolder, folderName);
        }

        private string GetExistingRelativePath(Author author)
        {
            var rootFolderPath = _rootFolderService.GetBestRootFolderPath(author.Path);

            return rootFolderPath.GetRelativePath(author.Path);
        }
    }
}
