using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class DownloadDecisionMakerFixture : CoreTest<DownloadDecisionMaker>
    {
        private List<ReleaseInfo> _reports;
        private RemoteBook _remoteBook;

        private Mock<IDecisionEngineSpecification> _pass1;
        private Mock<IDecisionEngineSpecification> _pass2;
        private Mock<IDecisionEngineSpecification> _pass3;

        private Mock<IDecisionEngineSpecification> _fail1;
        private Mock<IDecisionEngineSpecification> _fail2;
        private Mock<IDecisionEngineSpecification> _fail3;

        private Mock<IDecisionEngineSpecification> _failDelayed1;

        [SetUp]
        public void Setup()
        {
            _pass1 = new Mock<IDecisionEngineSpecification>();
            _pass2 = new Mock<IDecisionEngineSpecification>();
            _pass3 = new Mock<IDecisionEngineSpecification>();

            _fail1 = new Mock<IDecisionEngineSpecification>();
            _fail2 = new Mock<IDecisionEngineSpecification>();
            _fail3 = new Mock<IDecisionEngineSpecification>();

            _failDelayed1 = new Mock<IDecisionEngineSpecification>();

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null)).Returns(Decision.Accept);
            _pass2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null)).Returns(Decision.Accept);
            _pass3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null)).Returns(Decision.Accept);

            _fail1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null)).Returns(Decision.Reject("fail1"));
            _fail2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null)).Returns(Decision.Reject("fail2"));
            _fail3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null)).Returns(Decision.Reject("fail3"));

            _failDelayed1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null)).Returns(Decision.Reject("failDelayed1"));
            _failDelayed1.SetupGet(c => c.Priority).Returns(SpecificationPriority.Disk);

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = "Coldplay-A Head Full Of Dreams-CD-FLAC-2015-PERFECT" } };
            _remoteBook = new RemoteBook
            {
                Author = new Author(),
                Books = new List<Book> { new Book() },
                ParsedBookInfo = Builder<ParsedBookInfo>.CreateNew().With(x => x.Quality = new QualityModel(Quality.FLAC)).Build()
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()))
                  .Returns(_remoteBook);
        }

        private void GivenSpecifications(params Mock<IDecisionEngineSpecification>[] mocks)
        {
            Mocker.SetConstant<IEnumerable<IDecisionEngineSpecification>>(mocks.Select(c => c.Object));
        }

        [Test]
        public void should_call_all_specifications()
        {
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            Subject.GetRssDecision(_reports).ToList();

            _fail1.Verify(c => c.IsSatisfiedBy(_remoteBook, null), Times.Once());
            _fail2.Verify(c => c.IsSatisfiedBy(_remoteBook, null), Times.Once());
            _fail3.Verify(c => c.IsSatisfiedBy(_remoteBook, null), Times.Once());
            _pass1.Verify(c => c.IsSatisfiedBy(_remoteBook, null), Times.Once());
            _pass2.Verify(c => c.IsSatisfiedBy(_remoteBook, null), Times.Once());
            _pass3.Verify(c => c.IsSatisfiedBy(_remoteBook, null), Times.Once());
        }

        [Test]
        public void should_call_delayed_specifications_if_non_delayed_passed()
        {
            GivenSpecifications(_pass1, _failDelayed1);

            Subject.GetRssDecision(_reports).ToList();
            _failDelayed1.Verify(c => c.IsSatisfiedBy(_remoteBook, null), Times.Once());
        }

        [Test]
        public void should_not_call_delayed_specifications_if_non_delayed_failed()
        {
            GivenSpecifications(_fail1, _failDelayed1);

            Subject.GetRssDecision(_reports).ToList();

            _failDelayed1.Verify(c => c.IsSatisfiedBy(_remoteBook, null), Times.Never());
        }

        [Test]
        public void should_return_rejected_if_single_specs_fail()
        {
            GivenSpecifications(_fail1);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_rejected_if_one_of_specs_fail()
        {
            GivenSpecifications(_pass1, _fail1, _pass2, _pass3);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_pass_if_all_specs_pass()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_author_is_missing_even_if_specs_pass()
        {
            _remoteBook.Author = null;
            _remoteBook.Books = new List<Book>();
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, new BookSearchCriteria
            {
                Author = new Author { Name = "Expected Author" },
                Books = new List<Book>()
            }).Single();

            result.Approved.Should().BeFalse();
            result.Rejections.Any(r => r.Reason.Contains("Unknown Author")).Should().BeTrue();
        }

        [Test]
        public void should_have_same_number_of_rejections_as_specs_that_failed()
        {
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            var result = Subject.GetRssDecision(_reports);
            result.Single().Rejections.Should().HaveCount(3);
        }

        [Test]
        public void should_not_attempt_to_map_book_if_not_parsable()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "Not parsable";

            Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
        }

        [Test]
        public void should_not_attempt_to_map_book_if_author_title_is_blank()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "2013 - Night Visions";

            var results = Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());

            results.Should().BeEmpty();
        }

        [Test]
        public void should_return_rejected_result_for_unparsable_search()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "1937 - Snow White and the Seven Dwarves";

            var author = new Author { Name = "Some Author" };
            var books = new List<Book>
            {
                new Book
                {
                    Title = "Some Book",
                    Editions = new List<Edition>
                    {
                        new Edition { Title = "Some Edition Title" }
                    }
                }
            };

            Subject.GetSearchDecision(_reports, new BookSearchCriteria { Author = author, Books = books }).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
        }

        [Test]
        public void should_not_attempt_to_make_decision_if_author_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteBook.Author = null;

            Subject.GetRssDecision(_reports);

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteBook>(), null), Times.Never());
        }

        [Test]
        public void broken_report_shouldnt_blowup_the_process()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "Coldplay-A Head Full Of Dreams-CD-FLAC-2015-PERFECT" },
                    new ReleaseInfo { Title = "Coldplay-A Head Full Of Dreams-CD-FLAC-2015-PERFECT" },
                    new ReleaseInfo { Title = "Coldplay-A Head Full Of Dreams-CD-FLAC-2015-PERFECT" }
                };

            Subject.GetRssDecision(_reports);

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()), Times.Exactly(_reports.Count));

            ExceptionVerification.ExpectedErrors(3);
        }

        [Test]
        public void should_return_unknown_author_rejection_if_author_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteBook.Author = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);
        }

        [Test]
        public void should_only_include_reports_for_requested_books()
        {
            var author = Builder<Author>.CreateNew().Build();

            var books = Builder<Book>.CreateListOfSize(2)
                .All()
                .With(v => v.AuthorId, author.Id)
                .With(v => v.Author, new LazyLoaded<Author>(author))
                .BuildList();

            var criteria = new AuthorSearchCriteria { Books = books.Take(1).ToList() };

            var reports = books.Select(v =>
                new ReleaseInfo()
                {
                    Title = string.Format("{0}-{1}[FLAC][2017][DRONE]", author.Name, v.Title)
                }).ToList();

            Mocker.GetMock<IParsingService>()
                .Setup(v => v.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()))
                .Returns<ParsedBookInfo, SearchCriteriaBase>((p, c) =>
                    new RemoteBook
                    {
                        DownloadAllowed = true,
                        ParsedBookInfo = p,
                        Author = author,
                        Books = books.Where(v => v.Title == p.BookTitle).ToList()
                    });

            Mocker.SetConstant<IEnumerable<IDecisionEngineSpecification>>(new List<IDecisionEngineSpecification>
            {
                Mocker.Resolve<NzbDrone.Core.DecisionEngine.Specifications.Search.BookRequestedSpecification>()
            });

            var decisions = Subject.GetSearchDecision(reports, criteria);

            var approvedDecisions = decisions.Where(v => v.Approved).ToList();

            approvedDecisions.Count.Should().Be(1);
        }

        [Test]
        public void should_not_allow_download_if_author_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteBook.Author = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            result.First().RemoteBook.DownloadAllowed.Should().BeFalse();
        }

        [Test]
        public void should_not_allow_download_if_no_books_found()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteBook.Books = new List<Book>();

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            result.First().RemoteBook.DownloadAllowed.Should().BeFalse();
        }

        [Test]
        public void should_return_a_decision_when_exception_is_caught()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "Alien Ant Farm - TruAnt (FLAC) DRONE" },
                };

            Subject.GetRssDecision(_reports).Should().HaveCount(1);

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_reject_when_author_not_parsed_and_no_doi_match()
        {
            // Arrange: Release has a DOI that doesn't match the search criteria
            GivenSpecifications(_pass1, _pass2, _pass3);

            var author = new Author { Id = 1, Name = "Test Author" };
            var book = new Book
            {
                Id = 1,
                Title = "Test Book",
                Links = new List<Links>
                {
                    new Links { Name = "doi", Url = "10.1234/expected.doi" }
                }
            };

            var searchCriteria = new BookSearchCriteria
            {
                Author = author,
                Books = new List<Book> { book }
            };

            // Release has a different DOI (no match)
            _reports = new List<ReleaseInfo>
            {
                new ReleaseInfo
                {
                    Title = "Some unparseable title",
                    Doi = "10.9999/different.doi"
                }
            };

            // ParsingService returns remoteBook with null Author (couldn't parse)
            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(new RemoteBook
                {
                    Author = null,
                    Books = new List<Book>(),
                    ParsedBookInfo = new ParsedBookInfo { Quality = new QualityModel(Quality.PDF) }
                });

            // Act
            var result = Subject.GetSearchDecision(_reports, searchCriteria).ToList();

            // Assert: Should be rejected because author couldn't be parsed and DOI doesn't match
            result.Should().HaveCount(1);
            result.First().Approved.Should().BeFalse();
            result.First().Rejections.Any(r => r.Reason.Contains("Unknown Author")).Should().BeTrue();
        }

        [Test]
        public void should_accept_when_author_not_parsed_but_doi_matches()
        {
            // Arrange: Release has a DOI that matches the search criteria
            GivenSpecifications(_pass1, _pass2, _pass3);

            var author = new Author { Id = 1, Name = "Test Author" };
            var book = new Book
            {
                Id = 1,
                Title = "Test Book",
                Links = new List<Links>
                {
                    new Links { Name = "doi", Url = "10.1234/matching.doi" }
                }
            };

            var searchCriteria = new BookSearchCriteria
            {
                Author = author,
                Books = new List<Book> { book }
            };

            // Release has the same DOI (match!)
            _reports = new List<ReleaseInfo>
            {
                new ReleaseInfo
                {
                    Title = "Some unparseable title",
                    Doi = "10.1234/matching.doi"
                }
            };

            // ParsingService returns remoteBook with null Author initially (couldn't parse)
            // but DOI matches, so author should be filled from search criteria
            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(new RemoteBook
                {
                    Author = null,
                    Books = new List<Book>(),
                    ParsedBookInfo = new ParsedBookInfo { Quality = new QualityModel(Quality.PDF) }
                });

            // Act
            var result = Subject.GetSearchDecision(_reports, searchCriteria).ToList();

            // Assert: Should be approved because DOI matches (strong identifier)
            result.Should().HaveCount(1);
            result.First().Approved.Should().BeTrue();
            result.First().RemoteBook.Author.Should().NotBeNull();
            result.First().RemoteBook.Author.Id.Should().Be(author.Id);
        }

        [Test]
        public void should_reject_when_author_not_parsed_and_release_has_no_doi()
        {
            // Arrange: Release has no DOI at all
            GivenSpecifications(_pass1, _pass2, _pass3);

            var author = new Author { Id = 1, Name = "Test Author" };
            var book = new Book
            {
                Id = 1,
                Title = "Test Book",
                Links = new List<Links>
                {
                    new Links { Name = "doi", Url = "10.1234/expected.doi" }
                }
            };

            var searchCriteria = new BookSearchCriteria
            {
                Author = author,
                Books = new List<Book> { book }
            };

            // Release has no DOI
            _reports = new List<ReleaseInfo>
            {
                new ReleaseInfo
                {
                    Title = "Some unparseable title",
                    Doi = null
                }
            };

            // ParsingService returns remoteBook with null Author (couldn't parse)
            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedBookInfo>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(new RemoteBook
                {
                    Author = null,
                    Books = new List<Book>(),
                    ParsedBookInfo = new ParsedBookInfo { Quality = new QualityModel(Quality.PDF) }
                });

            // Act
            var result = Subject.GetSearchDecision(_reports, searchCriteria).ToList();

            // Assert: Should be rejected because author couldn't be parsed and no DOI to verify
            result.Should().HaveCount(1);
            result.First().Approved.Should().BeFalse();
            result.First().Rejections.Any(r => r.Reason.Contains("Unknown Author")).Should().BeTrue();
        }
    }
}
