using System.IO;

namespace MCPForUnity.Editor.Services.Abstractions
{
    /// <summary>
    /// Abstraction for file system operations to enable unit testing.
    /// </summary>
    public interface IFileSystem
    {
        bool FileExists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
        void Copy(string source, string dest, bool overwrite);
        void Delete(string path);
        void Move(string source, string dest);
        
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        bool IsSymlink(string path);
        
        // Path helpers if needed, though Path.* is usually pure enough (except pure unit tests on different OS)
        // For simple path logic, System.IO.Path is often fine to use statically as it doesn't touch disk.
        // But for consistency we can add simple wrappers if we want to mock specific platform behaviors.
    }
}
