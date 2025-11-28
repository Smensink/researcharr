using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.CustomFormats;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    public abstract class BookFileNullSpecificationFixtureBase<TSubject> : CoreTest<TSubject> where TSubject : class
    {
        protected void PrepareCommonMocks()
        {
            CustomFormatsTestHelpers.GivenCustomFormats();

            Mocker.GetMock<ICustomFormatCalculationService>()
                  .Setup(x => x.ParseCustomFormat(It.IsAny<BookFile>()))
                  .Returns(new List<CustomFormat>());

            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.DownloadPropersAndRepacks)
                  .Returns(ProperDownloadTypes.PreferAndUpgrade);
        }

        protected static RemoteBook BuildRemoteBook(bool includeParsedQuality = true, bool includeBookFiles = false, bool includeNullBook = false)
        {
            var qualityProfile = new QualityProfile
            {
                Cutoff = Quality.FLAC.Id,
                Items = Qualities.QualityFixture.GetDefaultQualities(),
                UpgradeAllowed = true,
                FormatItems = new List<ProfileFormatItem>()
            };

            var parsedBookInfo = includeParsedQuality ? new ParsedBookInfo { Quality = new QualityModel(Quality.MP3) } : null;

            var books = new List<Book>();

            if (includeNullBook)
            {
                books.Add(null);
            }

            books.Add(new Book { BookFiles = includeBookFiles ? new List<BookFile>() : null });

            return new RemoteBook
            {
                Author = new Author
                {
                    QualityProfile = qualityProfile,
                    Metadata = new AuthorMetadata { Name = "Test Author" }
                },
                Books = books,
                ParsedBookInfo = parsedBookInfo,
                CustomFormats = new List<CustomFormat>()
            };
        }
    }

    [TestFixture]
    public class CutoffSpecificationBookFileNullFixture : BookFileNullSpecificationFixtureBase<CutoffSpecification>
    {
        private RemoteBook _remoteBook;

        [SetUp]
        public void SetUp()
        {
            PrepareCommonMocks();
            Mocker.Resolve<UpgradableSpecification>();

            _remoteBook = BuildRemoteBook();
        }

        [Test]
        public void should_accept_when_book_files_are_not_loaded()
        {
            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_parsed_quality_is_missing()
        {
            _remoteBook = BuildRemoteBook(includeParsedQuality: false);

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_handle_null_books_without_throwing()
        {
            _remoteBook = BuildRemoteBook(includeParsedQuality: true, includeBookFiles: false, includeNullBook: true);

            Subject.Invoking(s => s.IsSatisfiedBy(_remoteBook, null)).Should().NotThrow();
        }
    }

    [TestFixture]
    public class UpgradeAllowedSpecificationBookFileNullFixture : BookFileNullSpecificationFixtureBase<UpgradeAllowedSpecification>
    {
        private RemoteBook _remoteBook;

        [SetUp]
        public void SetUp()
        {
            PrepareCommonMocks();
            Mocker.Resolve<UpgradableSpecification>();

            _remoteBook = BuildRemoteBook();
        }

        [Test]
        public void should_accept_when_book_files_are_not_loaded()
        {
            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_parsed_quality_is_missing()
        {
            _remoteBook = BuildRemoteBook(includeParsedQuality: false);

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_handle_null_books_without_throwing()
        {
            _remoteBook = BuildRemoteBook(includeParsedQuality: true, includeBookFiles: false, includeNullBook: true);

            Subject.Invoking(s => s.IsSatisfiedBy(_remoteBook, null)).Should().NotThrow();
        }
    }

    [TestFixture]
    public class UpgradeDiskSpecificationBookFileNullFixture : BookFileNullSpecificationFixtureBase<UpgradeDiskSpecification>
    {
        private RemoteBook _remoteBook;

        [SetUp]
        public void SetUp()
        {
            PrepareCommonMocks();
            Mocker.Resolve<UpgradableSpecification>();

            _remoteBook = BuildRemoteBook();
        }

        [Test]
        public void should_accept_when_book_files_are_not_loaded()
        {
            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_parsed_quality_is_missing()
        {
            _remoteBook = BuildRemoteBook(includeParsedQuality: false);

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_handle_null_books_without_throwing()
        {
            _remoteBook = BuildRemoteBook(includeParsedQuality: true, includeBookFiles: false, includeNullBook: true);

            Subject.Invoking(s => s.IsSatisfiedBy(_remoteBook, null)).Should().NotThrow();
        }
    }
}
