using System.Collections.Generic;
using MCPForUnity.Editor.Services.Abstractions;

namespace MCPForUnity.Editor.Tests.Mocks
{
    public class StubFileSystem : IFileSystem
    {
        public Dictionary<string, string> Files = new Dictionary<string, string>();
        public HashSet<string> Directories = new HashSet<string>();
        public HashSet<string> Symlinks = new HashSet<string>();

        public bool FileExists(string path) => Files.ContainsKey(path);
        public string ReadAllText(string path) => Files.TryGetValue(path, out var c) ? c : throw new System.IO.FileNotFoundException(path);
        public void WriteAllText(string path, string contents) => Files[path] = contents;
        public void Copy(string source, string dest, bool overwrite) => Files[dest] = Files[source]; // Simplified
        public void Delete(string path) => Files.Remove(path);
        public void Move(string source, string dest) 
        { 
            Files[dest] = Files[source]; 
            Files.Remove(source); 
        }

        public bool DirectoryExists(string path) => Directories.Contains(path);
        public void CreateDirectory(string path) => Directories.Add(path);

        public bool IsSymlink(string path) => Symlinks.Contains(path);
    }
}
