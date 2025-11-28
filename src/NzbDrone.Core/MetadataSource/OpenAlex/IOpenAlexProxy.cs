using System;
using System.Collections.Generic;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource.OpenAlex
{
    public interface IOpenAlexProxy
    {
        Author GetAuthorInfo(string readarrId, bool useCache = true, bool limitWorks = false);
        HashSet<string> GetChangedAuthors(DateTime startTime);
        List<Author> SearchForNewAuthor(string title);
        List<Book> SearchForNewBook(string query);
        List<Book> SearchByConcept(string topic);
        Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id);
        Book GetBookByDoi(string doi, bool useCache = true);
    }
}
