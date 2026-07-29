using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SchemaNode.Attribute;
using SchemaTypeProp = SchemaNode.Property.Core.SchemaType;

namespace SchemaNode.UnitTest;

/// <summary>
/// Temporary utility: reflects all [Meta&lt;SchemaType&gt;] declarations in
/// SchemaNode.Core and SchemaNode.App and dumps a JSON manifest used to
/// regenerate the locale files. Run with: dotnet test --filter LocaleDump
/// </summary>
[TestClass]
public class LocaleDumpTest
{
    private static readonly string OutputPath = "/tmp/schema_locale_manifest.json";

    [TestMethod]
    public void DumpLocaleManifest()
    {
        Assembly coreAssembly = typeof(SchemaNode.Utility.Constant).Assembly;
        Assembly appAssembly = typeof(SchemaNode.Schema.AppSchema).Assembly;

        List<TypeEntry> coreTypes = DumpAssembly(coreAssembly);
        List<TypeEntry> appTypes = DumpAssembly(appAssembly);

        var manifest = new
        {
            core = new { types = coreTypes },
            app = new { types = appTypes },
        };

        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(OutputPath, json);

        Console.WriteLine($"Wrote manifest to {OutputPath}");
        Console.WriteLine($"Core types: {coreTypes.Count}, App types: {appTypes.Count}");
        Assert.IsTrue(File.Exists(OutputPath));
    }

    /// <summary>
    /// Verifies the locale JSON files are embedded as manifest resources in the
    /// Core and App assemblies, and that the embedded content is readable.
    /// </summary>
    [TestMethod]
    public void EmbeddedLocaleResources_ArePresent()
    {
        Assembly coreAssembly = typeof(SchemaNode.Utility.Constant).Assembly;
        Assembly appAssembly = typeof(SchemaNode.Schema.AppSchema).Assembly;

        string[] coreResources = coreAssembly.GetManifestResourceNames();
        string[] appResources = appAssembly.GetManifestResourceNames();

        Console.WriteLine("Core locale resources: " + string.Join(", ",
            coreResources.Where(r => r.Contains("locale", StringComparison.OrdinalIgnoreCase)
                                  || r.Contains("schema_", StringComparison.OrdinalIgnoreCase))));
        Console.WriteLine("App locale resources: " + string.Join(", ",
            appResources.Where(r => r.Contains("locale", StringComparison.OrdinalIgnoreCase)
                                 || r.Contains("schema_", StringComparison.OrdinalIgnoreCase))));

        string? coreEn = coreResources.FirstOrDefault(r => r.EndsWith("schema_core_enUS.json", StringComparison.OrdinalIgnoreCase));
        string? coreZh = coreResources.FirstOrDefault(r => r.EndsWith("schema_core_zhCN.json", StringComparison.OrdinalIgnoreCase));
        string? appEn = appResources.FirstOrDefault(r => r.EndsWith("schema_app_enUS.json", StringComparison.OrdinalIgnoreCase));
        string? appZh = appResources.FirstOrDefault(r => r.EndsWith("schema_app_zhCN.json", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(coreEn, "Core enUS locale resource is missing");
        Assert.IsNotNull(coreZh, "Core zhCN locale resource is missing");
        Assert.IsNotNull(appEn, "App enUS locale resource is missing");
        Assert.IsNotNull(appZh, "App zhCN locale resource is missing");

        // The locale files must NOT be copied to the output directory anymore
        string coreOutDir = Path.GetDirectoryName(coreAssembly.Location)!;
        string appOutDir = Path.GetDirectoryName(appAssembly.Location)!;
        Assert.IsFalse(Directory.Exists(Path.Combine(coreOutDir, "locale")),
            "Core locale directory should not be copied to output (resources are embedded)");
        Assert.IsFalse(Directory.Exists(Path.Combine(appOutDir, "locale")),
            "App locale directory should not be copied to output (resources are embedded)");

        // Embedded content must deserialize into the expected key/value shape
        Assert.IsTrue(ReadEmbedded(coreAssembly, coreEn!).ContainsKey("system.bool"));
        Assert.IsTrue(ReadEmbedded(coreAssembly, coreZh!).ContainsKey("system.bool"));
        Assert.IsTrue(ReadEmbedded(appAssembly, appEn!).ContainsKey("system.schema.app.schema"));
        Assert.IsTrue(ReadEmbedded(appAssembly, appZh!).ContainsKey("system.schema.app.schema"));
    }

    private static Dictionary<string, string> ReadEmbedded(Assembly assembly, string name)
    {
        using Stream? stream = assembly.GetManifestResourceStream(name);
        Assert.IsNotNull(stream, $"Resource stream {name} not found");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        Dictionary<string, string>? dict = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
        Assert.IsNotNull(dict);
        return dict!;
    }

    private sealed class TypeEntry
    {
        public string schemaType { get; set; } = "";
        public string csharpName { get; set; } = "";
        public string? fullName { get; set; }
        public string kind { get; set; } = "";
        public List<string> enumValues { get; set; } = new();
        public List<string> methods { get; set; } = new();
        public List<string> fields { get; set; } = new();
    }

    private static List<TypeEntry> DumpAssembly(Assembly assembly)
    {
        var types = new List<TypeEntry>();

        foreach (Type type in assembly.GetTypes().OrderBy(t => t.FullName))
        {
            // SchemaType value is lowercased by SchemaType.SetValue
            string? schemaType = type.GetMetaProperty<SchemaTypeProp>()?.Value;
            if (string.IsNullOrEmpty(schemaType)) continue;

            bool isEnum = type.IsEnum;
            bool isStaticClass = type.IsAbstract && type.IsSealed && !type.IsInterface;

            List<string> enumValues = new();
            if (isEnum)
            {
                enumValues = System.Enum.GetNames(type).ToList();
            }

            List<string> methods = new();
            if (isStaticClass)
            {
                methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName) // exclude property getters/setters, operators
                    .Where(m => m.GetCustomAttribute<SchemaIgnoreAttribute>() == null)
                    .Select(m => m.Name)
                    .OrderBy(n => n)
                    .ToList();
            }

            // Schema fields: public instance properties decorated with [Meta<SchemaType>]
            List<string> fields = new();
            if (!isEnum && !isStaticClass)
            {
                fields = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(p => p.GetMetaProperty<SchemaTypeProp>() != null)
                    .Where(p => p.GetCustomAttribute<SchemaIgnoreAttribute>() == null)
                    .Select(p => p.Name)
                    .OrderBy(n => n)
                    .ToList();
            }

            types.Add(new TypeEntry
            {
                schemaType = schemaType!,
                csharpName = type.Name,
                fullName = type.FullName,
                kind = isEnum ? "enum" : isStaticClass ? "static" : "class",
                enumValues = enumValues,
                methods = methods,
                fields = fields,
            });
        }

        return types;
    }
}
