using System.IO.Compression;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Reflection;
using System.Collections.Concurrent;

namespace SchemaNode.Http;

public class CachedZipFileProvider : IFileProvider
{
    private readonly ConcurrentDictionary<string, CachedFile> _files = new();
    private readonly string _root;

    public CachedZipFileProvider(Assembly assembly, string resourceName, string root)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, false);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // skip directories
            using var es = entry.Open();
            using var ms = new MemoryStream();
            es.CopyTo(ms);
            string path = NormalizePath(entry.FullName);
            if (path.StartsWith(root)) path = path.Substring(root.Length);
            if (path.StartsWith("/")) path = path.Substring(1);
            _files[path] = new CachedFile
            {
                Name = path,
                Content = ms.ToArray(),
                LastModified = entry.LastWriteTime
            };
        }

        Console.WriteLine($"[CachedZipFileProvider] Loaded {_files.Count} files into memory from {resourceName}");
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        subpath = NormalizePath(subpath);
        var prefix = string.IsNullOrEmpty(subpath) ? "" : subpath + "/";
        var files = _files
            .Where(p => p.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(p => new InMemoryFileInfo(p.Value))
            .ToList();
        return new EnumerableDirectoryContents(files);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        subpath = NormalizePath(subpath);
        if (_files.TryGetValue(subpath, out var file))
            return new InMemoryFileInfo(file);
        return new NotFoundFileInfo(subpath);
    }

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private static string NormalizePath(string path)
    {
        path = path.TrimStart('/');
        return path.Replace('\\', '/');
    }

    private class CachedFile
    {
        public string Name = default!;
        public byte[] Content = default!;
        public DateTimeOffset LastModified;
    }

    private class InMemoryFileInfo : IFileInfo
    {
        private readonly CachedFile _file;
        public InMemoryFileInfo(CachedFile file) => _file = file;
        public bool Exists => true;
        public long Length => _file.Content.Length;
        public string PhysicalPath => null!;
        public string Name => _file.Name;
        public DateTimeOffset LastModified => _file.LastModified;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => new MemoryStream(_file.Content, writable: false);
    }

    private class EnumerableDirectoryContents : IDirectoryContents
    {
        private readonly IEnumerable<IFileInfo> _entries;
        public EnumerableDirectoryContents(IEnumerable<IFileInfo> entries) => _entries = entries;
        public bool Exists => true;
        public IEnumerator<IFileInfo> GetEnumerator() => _entries.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
