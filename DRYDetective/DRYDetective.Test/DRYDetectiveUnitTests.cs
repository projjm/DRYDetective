using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = DRYDetective.Test.CSharpCodeFixVerifier<
    DRYDetective.Refactoring.DRYDetectiveAnalyzer,
    DRYDetective.DRYDetectiveCodeFixProvider>;

namespace DRYDetective.Test
{
    [TestClass]
    public class DRYDetectiveUnitTest
    {
        private static string RootPath = System.IO.Directory.GetCurrentDirectory();
        private static string Directory = Path.GetFullPath(Path.Combine(RootPath, @"..\..\..\"));
        private static string SamplePath = Directory + "SampleCode/";

        //var expected = VerifyCS.Diagnostic("DRYDetective").WithSpan(11, 13, 11, 30);

        [TestMethod]
        public async Task TestBasicExample()
        {
            string fileName = "BasicExample.cs";
            string source = File.ReadAllText(SamplePath + fileName);

            await VerifyCS.VerifyCodeFixAsync(source, "");
        }
    }
}
