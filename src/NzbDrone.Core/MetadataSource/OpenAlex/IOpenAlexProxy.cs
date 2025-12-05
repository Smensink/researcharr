using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource.OpenAlex
{
    public interface IOpenAlexProxy
    {
        Author GetAuthorInfo(string readarrId, bool useCache = true, bool limitWorks = false, Action<List<Book>, int?> onWorkBatch = null, DateTime? updatedSince = null);
        HashSet<string> GetChangedAuthors(DateTime startTime);
        List<Author> SearchForNewAuthor(string title);
        List<Book> SearchForNewBook(string query);
        List<Book> SearchByConcept(string topic);
        List<OpenAlexTopic> SearchConcepts(string query, int perPage = 10);
        Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id);
        Book GetBookByDoi(string doi, bool useCache = true);
        OpenAlexListResponse<OpenAlexWork> SearchWorksAdvanced(string search, string filter, string sort, int perPage, string cursor);

        // Async versions for high-traffic endpoints
        Task<OpenAlexListResponse<OpenAlexWork>> SearchWorksAdvancedAsync(string search, string filter, string sort, int perPage, string cursor);
        Task<List<OpenAlexTopic>> SearchConceptsAsync(string query, int perPage = 10);
        Task<Tuple<string, Book, List<AuthorMetadata>>> GetBookInfoAsync(string id);
        Task<Book> GetBookByDoiAsync(string doi, bool useCache = true);
    }
}
