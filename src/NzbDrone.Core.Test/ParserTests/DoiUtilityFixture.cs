using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class DoiUtilityFixture : CoreTest
    {
        [TestCase("10.1038/nature12373", "10.1038/nature12373")]
        [TestCase("https://doi.org/10.1038/nature12373", "10.1038/nature12373")]
        [TestCase("http://dx.doi.org/10.1038/nature12373", "10.1038/nature12373")]
        [TestCase("doi:10.1038/nature12373", "10.1038/nature12373")]
        [TestCase("DOI: 10.1038/nature12373", "10.1038/nature12373")]
        [TestCase("  10.1038/nature12373  ", "10.1038/nature12373")]
        [TestCase("10.1371/journal.pone.0123456", "10.1371/journal.pone.0123456")]
        [TestCase("10.1016/j.cell.2020.01.001", "10.1016/j.cell.2020.01.001")]
        public void should_normalize_valid_dois(string input, string expected)
        {
            DoiUtility.Normalize(input).Should().Be(expected);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not-a-doi")]
        [TestCase("9.1234/invalid")]  // Must start with 10.
        [TestCase("10.12/tooshort")]  // Registrant code must be 4+ digits
        public void should_return_null_for_invalid_dois(string input)
        {
            DoiUtility.Normalize(input).Should().BeNull();
        }

        [TestCase("The DOI is 10.1038/nature12373 for this paper", "10.1038/nature12373")]
        [TestCase("doi: 10.1371/journal.pone.0123456", "10.1371/journal.pone.0123456")]
        [TestCase("Available at https://doi.org/10.1016/j.cell.2020.01.001", "10.1016/j.cell.2020.01.001")]
        [TestCase("Reference: DOI 10.1126/science.abc1234", "10.1126/science.abc1234")]
        [TestCase("10.1038/s41586-020-2649-2.", "10.1038/s41586-020-2649-2")]
        [TestCase("(10.1038/nature12373)", "10.1038/nature12373")]
        public void should_extract_doi_from_text(string text, string expected)
        {
            DoiUtility.ExtractFromText(text).Should().Be(expected);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("This text contains no DOI")]
        [TestCase("Reference: 9.1234/invalid")]
        public void should_return_null_when_no_doi_in_text(string text)
        {
            DoiUtility.ExtractFromText(text).Should().BeNull();
        }

        [TestCase("10.1038_nature12373.pdf", "10.1038/nature12373")]
        [TestCase("10.1371_journal.pone.0123456.pdf", "10.1371/journal.pone.0123456")]
        [TestCase("Paper - 10.1038-nature12373.pdf", "10.1038/nature12373")]
        [TestCase("10.1016-j.cell.2020.01.001", "10.1016/j.cell.2020.01.001")]
        public void should_extract_doi_from_filename_with_underscore_or_dash(string filename, string expected)
        {
            DoiUtility.ExtractFromFilename(filename).Should().Be(expected);
        }

        [TestCase("10.1038/nature12373.pdf", "10.1038/nature12373")]
        public void should_extract_doi_from_filename_with_slash(string filename, string expected)
        {
            DoiUtility.ExtractFromFilename(filename).Should().Be(expected);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("random_paper_name.pdf")]
        [TestCase("Author - Title (2020).pdf")]
        public void should_return_null_when_no_doi_in_filename(string filename)
        {
            DoiUtility.ExtractFromFilename(filename).Should().BeNull();
        }

        [Test]
        public void should_extract_all_dois_from_text_with_multiple_dois()
        {
            var text = "References: 10.1038/nature12373, 10.1371/journal.pone.0123456, and 10.1016/j.cell.2020.01.001";
            var results = DoiUtility.ExtractAllFromText(text);

            results.Should().HaveCount(3);
            results.Should().Contain("10.1038/nature12373");
            results.Should().Contain("10.1371/journal.pone.0123456");
            results.Should().Contain("10.1016/j.cell.2020.01.001");
        }

        [Test]
        public void should_not_return_duplicate_dois()
        {
            var text = "DOI: 10.1038/nature12373, also see 10.1038/nature12373";
            var results = DoiUtility.ExtractAllFromText(text);

            results.Should().HaveCount(1);
            results.Should().Contain("10.1038/nature12373");
        }

        [Test]
        public void should_return_empty_for_null_or_empty_text()
        {
            DoiUtility.ExtractAllFromText(null).Should().BeEmpty();
            DoiUtility.ExtractAllFromText("").Should().BeEmpty();
            DoiUtility.ExtractAllFromText("   ").Should().BeEmpty();
        }

        [TestCase("10.1038/NATURE12373", "10.1038/nature12373")]
        [TestCase("10.1038/Nature12373", "10.1038/nature12373")]
        public void should_normalize_doi_case(string input, string expected)
        {
            DoiUtility.Normalize(input).Should().Be(expected);
        }

        [TestCase("10.3390/cancers13225694https://www.mdpi.com/journal/cancers?mailto=researcharr%40example.com", "10.3390/cancers13225694")]
        [TestCase("10.1063/1.1316015https://aip.scitation.org/doi/pdf/10.1063/1.1316015", "10.1063/1.1316015")]
        [TestCase("10.1038/nature12373http://example.com", "10.1038/nature12373")]
        public void should_extract_doi_when_concatenated_with_url(string input, string expected)
        {
            DoiUtility.Normalize(input).Should().Be(expected);
        }
    }
}
