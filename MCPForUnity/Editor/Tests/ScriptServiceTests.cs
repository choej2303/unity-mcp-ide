using NUnit.Framework;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Tests.Mocks;
using System.IO;

namespace MCPForUnity.Editor.Tests.Services
{
    public class ScriptServiceTests
    {
        private StubFileSystem _fs;
        private string _fakeAssetsPath = "c:/fake/project/Assets";

        [SetUp]
        public void Setup()
        {
            _fs = new StubFileSystem();
            ScriptService.FileSystem = _fs;
            ScriptService.AssetsPathProvider = () => _fakeAssetsPath;
        }

        [TearDown]
        public void Teardown()
        {
            // Reset to default avoids side effects if running in Unity (though this test intends to run outside too)
            // ScriptService.AssetsPathProvider = ... (cannot easily restore original lambda without static backup, but for test suite it is fine)
        }

        [Test]
        public void TryResolve_ValidRelativePath_ReturnsTrue()
        {
            bool result = ScriptService.TryResolveUnderAssets("Scripts/MyScript.cs", out string full, out string rel);
            Assert.IsTrue(result);
            Assert.AreEqual("c:/fake/project/Assets/Scripts/MyScript.cs", full);
            Assert.AreEqual("Assets/Scripts/MyScript.cs", rel);
        }

        [Test]
        public void TryResolve_Traversal_ReturnsFalse()
        {
            bool result = ScriptService.TryResolveUnderAssets("../Outside.cs", out string full, out string rel);
            Assert.IsFalse(result);
        }

        [Test]
        public void TryResolve_Symlink_ReturnsFalse()
        {
            // Arrange
            string symlinkDir = "c:/fake/project/Assets/SymLinkedFolder";
            _fs.Symlinks.Add(symlinkDir);

            // Act
            bool result = ScriptService.TryResolveUnderAssets("SymLinkedFolder/Script.cs", out string full, out string rel);
            
            // Assert
            Assert.IsFalse(result);
        }
    }
}
