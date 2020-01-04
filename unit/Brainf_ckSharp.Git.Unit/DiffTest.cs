using System.Buffers;
using System.Linq;
using System.Runtime.CompilerServices;
using Brainf_ckSharp.Git.Enums;
using Brainf_ckSharp.Git.Unit.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Brainf_ckSharp.Git.Unit
{
    [TestClass]
    public class DiffTest
    {
        [TestMethod]
        public void Small()
        {
            LineModificationType[] expected =
            {
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.Modified,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.Modified,
                LineModificationType.Modified,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.Modified,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.Modified
            };

            Test(expected);
        }

        [TestMethod]
        public void Medium()
        {
            LineModificationType[] expected =
            {
                // 1
                LineModificationType.Modified,
                LineModificationType.Modified,
                LineModificationType.Modified,
                LineModificationType.Modified,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.Modified,
                LineModificationType.Modified,
                LineModificationType.None,

                // 10
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.Modified,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.None,

                // 20
                LineModificationType.Modified,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.Modified,
                LineModificationType.Modified,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.None,
                LineModificationType.Modified,
                LineModificationType.None,

                // 30
                LineModificationType.Modified,
                LineModificationType.Modified,
                LineModificationType.Modified,
                LineModificationType.None,
                LineModificationType.None
            };

            Test(expected);
        }

        /// <summary>
        /// Runs a test for a pair of resources with the specified name
        /// </summary>
        /// <param name="expected">The expected results for the test</param>
        /// <param name="key">The key of the test resources to load</param>
        private static void Test(LineModificationType[] expected, [CallerMemberName] string key = null)
        {
            var data = ResourceLoader.LoadTestSample(key);

            string
                oldText = data.Old.Replace("\n", string.Empty),
                newText = data.New.Replace("\n", string.Empty);

            MemoryOwner<LineModificationType> result = LineDiffer.ComputeDiff(oldText, newText, '\r');

            try
            {
                Assert.AreEqual(expected.Length, result.Size);

                if (expected.Length == 0) return;

                Assert.IsTrue(expected.SequenceEqual(result.Span.ToArray()));
            }
            finally
            {
                result.Dispose();
            }
        }
    }
}
