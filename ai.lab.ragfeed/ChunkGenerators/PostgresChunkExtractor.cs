using System.Text;

namespace ai.lab.ragfeed.ChunkGenerators;

public class PostgresChunkExtractor
{
    public List<string> ExtractChunks(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var statements = SplitSqlStatements(content);
        return statements.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private List<string> SplitSqlStatements(string sql)
    {
        bool inString = false;
        var chunks = new List<string>();
        var builder = new StringBuilder();        

        foreach (char c in sql)
        {
            if (c == '\'') inString = !inString;
            {
                builder.Append(c);
            }

            if (c == ';' && !inString)
            {
                chunks.Add(builder.ToString().Trim());
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            chunks.Add(NormalizeChunk(builder.ToString()));
        }

        return chunks;
    }

    private string NormalizeChunk(string chunk) => chunk
            .Replace("\t", "    ") // convert tabs to spaces
            .Replace("\r", "")     // unify line endings
            .Trim();               // remove leading/trailing whitespace
}
