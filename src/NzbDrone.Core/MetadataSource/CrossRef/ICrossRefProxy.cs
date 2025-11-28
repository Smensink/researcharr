using System.Collections.Generic;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource.CrossRef
{
    public interface ICrossRefProxy
    {
        /// <summary>
        /// Get book/paper information by DOI
        /// </summary>
        Book GetBookByDoi(string doi, bool useCache = true);

        /// <summary>
        /// Search for books/papers by title and/or author
        /// </summary>
        List<Book> SearchBooks(string title, string author = null, int maxResults = 20, bool useCache = true);

        /// <summary>
        /// Get author information by ORCID
        /// </summary>
        Author GetAuthorByOrcid(string orcid, bool useCache = true);
    }
}
