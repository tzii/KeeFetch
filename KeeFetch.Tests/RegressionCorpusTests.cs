using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace KeeFetch.Tests
{
    [TestClass]
    public class RegressionCorpusTests
    {
        [TestMethod]
        public void Issue1RegressionCorpus_IsPresentAndComplete()
        {
            string fixturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Fixtures", "Issue1RegressionUrls.txt");
            Assert.IsTrue(File.Exists(fixturePath), "Issue #1 regression URL corpus should be present.");

            var urls = File.ReadAllLines(fixturePath)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            Assert.IsTrue(urls.Count >= 23, "Issue #1 corpus should include all reported problem URLs.");
            Assert.IsTrue(urls.Contains("https://www.springer.com/de"), "SVG-heavy issue sample should remain in corpus.");
            Assert.IsTrue(urls.Contains("https://genostore.de"), "Icon Horse placeholder regression sample should remain in corpus.");
        }
    }
}
