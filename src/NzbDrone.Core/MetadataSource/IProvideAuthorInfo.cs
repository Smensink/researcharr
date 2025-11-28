using System;
using System.Collections.Generic;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource
{
    public interface IProvideAuthorInfo
    {
        Author GetAuthorInfo(string readarrId, bool useCache = true, bool limitWorks = false, Action<List<Book>> onWorkBatch = null);
        HashSet<string> GetChangedAuthors(DateTime startTime);
    }
}
