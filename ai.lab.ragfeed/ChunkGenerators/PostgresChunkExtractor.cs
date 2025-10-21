using ai.lab.ragfeed.ChunkGenerators.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

public class PostgresChunkExtractor : IFileChunkGenerator
{
    public List<string> GenerateChunks(string filepath) => ExtractSqlChunks(filepath);

    public List<string> ExtractSqlChunks(string filePath)
    {
        var content = File.ReadAllText(filePath);
        
        // Remove comments first to avoid parsing issues
        content = RemoveComments(content);
        
        var statements = SplitSqlStatements(content);
        var chunks = new List<string>();

        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement))
                continue;

            // Add semantic context to each chunk
            var chunk = EnrichStatementWithContext(statement);
            chunks.Add(chunk);
        }

        return chunks;
    }

    /// <summary>
    /// Removes SQL comments (both -- and /* */ styles) while preserving string literals
    /// </summary>
    private string RemoveComments(string sql)
    {
        var result = new StringBuilder();
        bool inString = false;
        bool inSingleLineComment = false;
        bool inMultiLineComment = false;

        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];
            char next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            // Handle string literals
            if (c == '\'' && !inSingleLineComment && !inMultiLineComment)
            {
                inString = !inString;
                result.Append(c);
                continue;
            }

            if (inString)
            {
                result.Append(c);
                continue;
            }

            // Handle single-line comments
            if (c == '-' && next == '-' && !inMultiLineComment)
            {
                inSingleLineComment = true;
                i++; // Skip next '-'
                continue;
            }

            if (inSingleLineComment)
            {
                if (c == '\n' || c == '\r')
                {
                    inSingleLineComment = false;
                    result.Append(c); // Preserve newline
                }
                continue;
            }

            // Handle multi-line comments
            if (c == '/' && next == '*')
            {
                inMultiLineComment = true;
                i++; // Skip '*'
                continue;
            }

            if (inMultiLineComment)
            {
                if (c == '*' && next == '/')
                {
                    inMultiLineComment = false;
                    i++; // Skip '/'
                }
                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    /// Splits SQL into statements while handling strings, dollar-quoted strings, and PL/pgSQL blocks
    /// </summary>
    private List<string> SplitSqlStatements(string sql)
    {
        var chunks = new List<string>();
        var builder = new StringBuilder();
        bool inString = false;
        bool inDollarQuote = false;
        string dollarTag = "";
        int functionDepth = 0; // Track CREATE FUNCTION/PROCEDURE blocks

        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];
            char next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            builder.Append(c);

            // Handle single-quoted strings
            if (c == '\'' && !inDollarQuote)
            {
                // Check for escaped quote
                if (next == '\'')
                {
                    builder.Append(next);
                    i++;
                }
                else
                {
                    inString = !inString;
                }
                continue;
            }

            if (inString)
                continue;

            // Handle dollar-quoted strings (PostgreSQL-specific: $$, $tag$)
            if (c == '$')
            {
                var tagMatch = Regex.Match(sql.Substring(i), @"^\$(\w*)\$");
                if (tagMatch.Success)
                {
                    string tag = tagMatch.Value;
                    if (!inDollarQuote)
                    {
                        inDollarQuote = true;
                        dollarTag = tag;
                        builder.Append(sql.Substring(i + 1, tag.Length - 1));
                        i += tag.Length - 1;
                    }
                    else if (tag == dollarTag)
                    {
                        inDollarQuote = false;
                        dollarTag = "";
                        builder.Append(sql.Substring(i + 1, tag.Length - 1));
                        i += tag.Length - 1;
                    }
                    continue;
                }
            }

            if (inDollarQuote)
                continue;

            // Track function/procedure depth
            var remaining = sql.Substring(i);
            if (Regex.IsMatch(remaining, @"^\s*CREATE\s+(OR\s+REPLACE\s+)?(FUNCTION|PROCEDURE)", RegexOptions.IgnoreCase))
            {
                functionDepth++;
            }

            // End of statement
            if (c == ';')
            {
                // If we're in a function/procedure, only end at the final semicolon
                if (functionDepth > 0)
                {
                    // Check if this is the function-ending semicolon
                    var afterSemi = sql.Substring(i + 1).TrimStart();
                    if (Regex.IsMatch(afterSemi, @"^(CREATE|ALTER|DROP|INSERT|UPDATE|DELETE|SELECT)", RegexOptions.IgnoreCase) ||
                        string.IsNullOrWhiteSpace(afterSemi))
                    {
                        functionDepth--;
                    }
                }

                if (functionDepth == 0)
                {
                    var statement = builder.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(statement))
                    {
                        chunks.Add(statement);
                    }
                    builder.Clear();
                }
            }
        }

        // Add any remaining content
        if (builder.Length > 0)
        {
            var statement = builder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(statement))
            {
                chunks.Add(statement);
            }
        }

        return chunks;
    }

    /// <summary>
    /// Enriches SQL statement with semantic context for better RAG retrieval
    /// </summary>
    private string EnrichStatementWithContext(string statement)
    {
        var enriched = new StringBuilder();
        var statementUpper = statement.ToUpperInvariant();

        // Identify statement type and extract key elements
        if (statementUpper.Contains("CREATE TABLE"))
        {
            var tableName = ExtractTableName(statement, "CREATE TABLE");
            enriched.AppendLine($"-- SQL DDL: CREATE TABLE {tableName}");
            enriched.AppendLine($"-- Statement Type: Table Definition");
        }
        else if (statementUpper.Contains("CREATE INDEX"))
        {
            var indexName = ExtractObjectName(statement, "CREATE.*?INDEX", @"INDEX\s+(\w+)");
            enriched.AppendLine($"-- SQL DDL: CREATE INDEX {indexName}");
            enriched.AppendLine($"-- Statement Type: Index Definition");
        }
        else if (statementUpper.Contains("CREATE FUNCTION") || statementUpper.Contains("CREATE PROCEDURE"))
        {
            var type = statementUpper.Contains("FUNCTION") ? "FUNCTION" : "PROCEDURE";
            var name = ExtractObjectName(statement, $"CREATE.*?{type}", $@"{type}\s+(\w+)");
            enriched.AppendLine($"-- SQL DDL: CREATE {type} {name}");
            enriched.AppendLine($"-- Statement Type: {type} Definition");
        }
        else if (statementUpper.Contains("CREATE VIEW"))
        {
            var viewName = ExtractObjectName(statement, "CREATE.*?VIEW", @"VIEW\s+(\w+)");
            enriched.AppendLine($"-- SQL DDL: CREATE VIEW {viewName}");
            enriched.AppendLine($"-- Statement Type: View Definition");
        }
        else if (statementUpper.Contains("CREATE TRIGGER"))
        {
            var triggerName = ExtractObjectName(statement, "CREATE.*?TRIGGER", @"TRIGGER\s+(\w+)");
            enriched.AppendLine($"-- SQL DDL: CREATE TRIGGER {triggerName}");
            enriched.AppendLine($"-- Statement Type: Trigger Definition");
        }
        else if (statementUpper.Contains("ALTER TABLE"))
        {
            var tableName = ExtractTableName(statement, "ALTER TABLE");
            enriched.AppendLine($"-- SQL DDL: ALTER TABLE {tableName}");
            enriched.AppendLine($"-- Statement Type: Table Alteration");
        }
        else if (statementUpper.Contains("INSERT INTO"))
        {
            var tableName = ExtractTableName(statement, "INSERT INTO");
            enriched.AppendLine($"-- SQL DML: INSERT INTO {tableName}");
            enriched.AppendLine($"-- Statement Type: Data Insert");
        }
        else if (statementUpper.Contains("UPDATE"))
        {
            var tableName = ExtractTableName(statement, "UPDATE");
            enriched.AppendLine($"-- SQL DML: UPDATE {tableName}");
            enriched.AppendLine($"-- Statement Type: Data Update");
        }
        else if (statementUpper.Contains("DELETE FROM"))
        {
            var tableName = ExtractTableName(statement, "DELETE FROM");
            enriched.AppendLine($"-- SQL DML: DELETE FROM {tableName}");
            enriched.AppendLine($"-- Statement Type: Data Delete");
        }
        else if (statementUpper.Contains("SELECT"))
        {
            enriched.AppendLine($"-- SQL DML: SELECT Query");
            enriched.AppendLine($"-- Statement Type: Data Query");
        }
        else
        {
            enriched.AppendLine($"-- SQL Statement");
        }

        enriched.AppendLine(statement);
        return enriched.ToString();
    }

    private string ExtractTableName(string statement, string keyword)
    {
        var pattern = $@"{keyword}\s+(?:IF\s+NOT\s+EXISTS\s+)?(\w+\.)?(\w+)";
        var match = Regex.Match(statement, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[2].Value;
        }
        return "unknown";
    }

    private string ExtractObjectName(string statement, string createPattern, string namePattern)
    {
        var match = Regex.Match(statement, namePattern, RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }
        return "unknown";
    }
}
