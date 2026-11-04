using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class CoreIsolationTests
    {
        static string CoreDir([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), "../../Assets/_Project/Scripts/Core"));

        [Test]
        public void CoreAsmdef_HasNoEngineReferences()
        {
            using var asmdef = JsonDocument.Parse(File.ReadAllText(Path.Combine(CoreDir(), "ChessBoss.Core.asmdef")));

            Assert.IsTrue(asmdef.RootElement.GetProperty("noEngineReferences").GetBoolean());
            var unityReferences = asmdef.RootElement.GetProperty("references").EnumerateArray()
                .Select(r => r.GetString())
                .Where(r => r.StartsWith("Unity"));
            CollectionAssert.IsEmpty(unityReferences);
        }

        [Test]
        public void CoreSources_DoNotMentionUnityNamespaces()
        {
            var offenders = Directory.GetFiles(CoreDir(), "*.cs", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var text = File.ReadAllText(f);
                    return text.Contains("UnityEngine") || text.Contains("UnityEditor");
                });

            CollectionAssert.IsEmpty(offenders);
        }
    }
}
