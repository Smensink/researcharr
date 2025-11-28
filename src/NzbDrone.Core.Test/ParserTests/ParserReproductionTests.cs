using NUnit.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class ParserReproductionTests
    {
        [Test]
        public void Should_parse_author_title_format()
        {
            var title = "Khalil Ahmad - New Exact Solutions of Landau-Ginzburg-Higgs Equation Using Power Index Method";
            var result = Parser.Parser.ParseBookTitle(title);

            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual("Khalil Ahmad", result.AuthorName);
            Assert.AreEqual("New Exact Solutions of Landau-Ginzburg-Higgs Equation Using Power Index Method", result.BookTitle);
        }
    }
}
