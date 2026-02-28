using Microsoft.SemanticKernel.Embeddings;
using Npgsql;
using Pgvector;
using SchemaNode.AI.Services;

namespace SchemaNode.PostgreSQL;

/// <summary>
/// PostgreSQL + pgvector implementation of <see cref="IOntologyVectorService"/>.
/// <para>
/// Each SSP block is embedded via <see cref="ITextEmbeddingGenerationService"/> and stored
/// in a dedicated table with a <c>vector(N)</c> column.  Vector dimensions are controlled
/// by <see cref="OntologyVectorOptions.Dimensions"/> — change this value to match your
/// embedding model (e.g. 1536 for <c>text-embedding-3-small</c>).
/// </para>
/// <para>
/// The table is created automatically on the first call to <see cref="EnsureTableAsync"/>.
/// An HNSW approximate-nearest-neighbour index (cosine distance) is added at that time.
/// </para>
/// <para>
/// Requires <c>pgvector</c> extension to be enabled in PostgreSQL:
/// <code>CREATE EXTENSION IF NOT EXISTS vector;</code>
/// </para>
/// </summary>
public class OntologyVectorPostgreSqlService(
    NpgsqlDataSource dataSource,
    ITextEmbeddingGenerationService embeddingService,
    OntologyVectorOptions options) : IOntologyVectorService
{
    private readonly string _table = options.TableName;

    /// <inheritdoc />
    public async Task EnsureTableAsync(CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS "{_table}" (
                id         BIGSERIAL    PRIMARY KEY,
                schema_key TEXT         NOT NULL,
                locale     TEXT         NOT NULL DEFAULT 'enUS',
                category   TEXT         NOT NULL,
                content    TEXT         NOT NULL,
                embedding  vector({options.Dimensions}),
                indexed_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
            );
            ALTER TABLE "{_table}" ADD COLUMN IF NOT EXISTS locale TEXT NOT NULL DEFAULT 'enUS';
            DROP INDEX IF EXISTS "{_table}_schema_key_idx";
            CREATE UNIQUE INDEX IF NOT EXISTS "{_table}_schema_key_locale_idx"
                ON "{_table}" (schema_key, locale);
            CREATE INDEX IF NOT EXISTS "{_table}_embedding_idx"
                ON "{_table}" USING hnsw (embedding vector_cosine_ops);
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string schemaKey, CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"""DELETE FROM "{_table}" WHERE schema_key = $1;""";
        cmd.Parameters.AddWithValue(schemaKey);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task IndexAsync(
        string schemaKey, string sspContent, OntologyVectorCategory category, string locale, CancellationToken cancellationToken = default)
    {
        IList<ReadOnlyMemory<float>> embeddings =
            await embeddingService.GenerateEmbeddingsAsync([sspContent], cancellationToken: cancellationToken);
        var vector = new Vector(embeddings[0].ToArray());

        await using NpgsqlConnection conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO "{_table}" (schema_key, locale, category, content, embedding)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (schema_key, locale) DO UPDATE SET
                category   = EXCLUDED.category,
                content    = EXCLUDED.content,
                embedding  = EXCLUDED.embedding,
                indexed_at = NOW();
            """;
        cmd.Parameters.AddWithValue(schemaKey);
        cmd.Parameters.AddWithValue(locale);
        cmd.Parameters.AddWithValue(category.ToString());
        cmd.Parameters.AddWithValue(sspContent);
        cmd.Parameters.AddWithValue(vector);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OntologyVectorMatch>> SearchAsync(
        string queryText, int topK = 5, OntologyVectorCategory? category = null, string? locale = null, CancellationToken cancellationToken = default)
    {
        IList<ReadOnlyMemory<float>> queryEmbeddings =
            await embeddingService.GenerateEmbeddingsAsync([queryText], cancellationToken: cancellationToken);
        var queryVector = new Vector(queryEmbeddings[0].ToArray());

        await using NpgsqlConnection conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand cmd = conn.CreateCommand();

        var conditions = new List<string>();
        int paramIdx = 3;
        if (category.HasValue)             conditions.Add($"category = ${paramIdx++}");
        if (!string.IsNullOrEmpty(locale)) conditions.Add($"locale = ${paramIdx++}");
        string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        cmd.CommandText = $"""
            SELECT schema_key, category, content, locale, 1 - (embedding <=> $1) AS score
            FROM   "{_table}"
            {whereClause}
            ORDER  BY embedding <=> $1
            LIMIT  $2;
            """;
        cmd.Parameters.AddWithValue(queryVector);
        cmd.Parameters.AddWithValue(topK);
        if (category.HasValue)             cmd.Parameters.AddWithValue(category.Value.ToString());
        if (!string.IsNullOrEmpty(locale)) cmd.Parameters.AddWithValue(locale);

        var results = new List<OntologyVectorMatch>();
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new OntologyVectorMatch
            {
                SchemaKey = reader.GetString(0),
                Category  = System.Enum.Parse<OntologyVectorCategory>(reader.GetString(1)),
                Content   = reader.GetString(2),
                Locale    = reader.GetString(3),
                Score     = reader.GetDouble(4),
            });
        }
        return results;
    }
}
