using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.BooksTests
{
    [TestFixture]
    public class AddBookServiceFixture : CoreTest<AddBookService>
    {
        private Book _book;
        private Book _existingBook;

        [SetUp]
        public void Setup()
        {
            _book = new Book
            {
                ForeignBookId = "A123",
                Title = "The structure of DNA",
                AuthorMetadata = new AuthorMetadata { ForeignAuthorId = "A123" },
                Editions = new LazyLoaded<List<Edition>>(new List<Edition>
                {
                    new Edition { ForeignEditionId = "E123", Monitored = true, Isbn13 = "10.1038/171737a0" }
                })
            };

            _existingBook = new Book
            {
                Id = 10,
                ForeignBookId = "W456", // Different OpenAlex ID
                Title = "The structure of DNA (Existing)",
                Editions = new LazyLoaded<List<Edition>>(new List<Edition>
                {
                    new Edition { ForeignEditionId = "E456", Monitored = true, Isbn13 = "10.1038/171737a0" } // Same DOI
                })
            };

            Mocker.GetMock<IProvideBookInfo>()
                  .Setup(s => s.GetBookInfo(It.IsAny<string>()))
                  .Returns(new Tuple<string, Book, List<AuthorMetadata>>(_book.ForeignBookId, _book, new List<AuthorMetadata> { _book.AuthorMetadata.Value }));

            Mocker.GetMock<IAuthorService>()
                  .Setup(s => s.FindById(It.IsAny<string>()))
                  .Returns(new Author { Id = 1, AuthorMetadataId = 1 });
        }

        [Test]
        public void should_use_existing_book_if_doi_matches()
        {
            Mocker.GetMock<IBookService>()
                  .Setup(s => s.FindById(_book.ForeignBookId))
                  .Returns((Book)null);

            Mocker.GetMock<IBookService>()
                  .Setup(s => s.FindBookByIsbn13("10.1038/171737a0"))
                  .Returns(_existingBook);

            var result = Subject.AddBook(_book);

            result.Id.Should().Be(_existingBook.Id);
            Mocker.GetMock<IBookService>().Verify(s => s.AddBook(It.IsAny<Book>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void should_add_new_book_if_doi_does_not_match()
        {
            Mocker.GetMock<IBookService>()
                  .Setup(s => s.FindById(_book.ForeignBookId))
                  .Returns((Book)null);

            Mocker.GetMock<IBookService>()
                  .Setup(s => s.FindBookByIsbn13(It.IsAny<string>()))
                  .Returns((Book)null);

            Mocker.GetMock<IAddAuthorService>()
                  .Setup(s => s.AddAuthor(It.IsAny<Author>(), It.IsAny<bool>()))
                  .Returns(new Author { Id = 1, AuthorMetadataId = 1 });

            var result = Subject.AddBook(_book);

            result.Id.Should().Be(0); // New book
            Mocker.GetMock<IBookService>().Verify(s => s.AddBook(It.IsAny<Book>(), It.IsAny<bool>()), Times.Once());
        }

        [Test]
        public void should_add_book_when_ids_mismatch()
        {
            // Setup mismatching IDs
            var book = new Book
            {
                ForeignBookId = "W999",
                Title = "Mismatched IDs Book",
                AuthorMetadata = new AuthorMetadata { ForeignAuthorId = "A888" },
                Editions = new LazyLoaded<List<Edition>>(new List<Edition>
                {
                    new Edition { ForeignEditionId = "E999", Monitored = true, Isbn13 = "10.1234/5678" }
                })
            };

            Mocker.GetMock<IProvideBookInfo>()
                  .Setup(s => s.GetBookInfo(book.ForeignBookId))
                  .Returns(new Tuple<string, Book, List<AuthorMetadata>>(book.ForeignBookId, book, new List<AuthorMetadata> { book.AuthorMetadata.Value }));

            Mocker.GetMock<IAddAuthorService>()
                  .Setup(s => s.AddAuthor(It.IsAny<Author>(), It.IsAny<bool>()))
                  .Returns(new Author { Id = 2, AuthorMetadataId = 2 });

            var result = Subject.AddBook(book);

            result.Should().NotBeNull();
            result.ForeignBookId.Should().Be("W999");
        }
    }
}
