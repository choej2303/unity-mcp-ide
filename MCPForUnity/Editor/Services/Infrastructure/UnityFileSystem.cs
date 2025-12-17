using System.IO;
using MCPForUnity.Editor.Services.Abstractions;

namespace MCPForUnity.Editor.Services.Infrastructure
{
    /// <summary>
    /// Concrete implementation of IFileSystem using System.IO.
    /// </summary>
    public class UnityFileSystem : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);
        public string ReadAllText(string path) => File.ReadAllText(path);
        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
        public void Copy(string source, string dest, bool overwrite) => File.Copy(source, dest, overwrite);
        public void Delete(string path) => File.Delete(path);
        public void Move(string source, string dest) => File.Move(source, dest);

        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public bool IsSymlink(string path)
        {
            try
            {
                var di = new DirectoryInfo(path);
                return di.Exists && (di.Attributes & FileAttributes.ReparsePoint) != 0;
            }
            catch { return false; }
        }
    }
}
