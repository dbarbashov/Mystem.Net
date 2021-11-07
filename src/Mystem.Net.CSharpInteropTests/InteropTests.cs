using System.Threading.Tasks;
using NUnit.Framework;

namespace Mystem.Net.CSharpInteropTests
{
    public class Tests
    {
        [Test]
        public async Task TestAnalyzeIsConvinientAsync()
        {
            using (var mystemHandle = new Mystem())
            {
                var lemmas = await mystemHandle.Mystem.Analyze("Привет");
                
                Assert.AreEqual(2, lemmas.Length);
                Assert.AreEqual("Привет", lemmas[0].Text);
                Assert.AreEqual("привет", lemmas[0].AnalysisResults[0].Lexeme);
            }
        }
        
        [Test]
        public async Task TestLemmatizeIsConvinientAsync()
        {
            using (var mystemHandle = new Mystem())
            {
                var lemmas = await mystemHandle.Mystem.Lemmatize("Привет");
                Assert.AreEqual(new[] { "привет", "\n" }, lemmas);
            }
        }
    }
}