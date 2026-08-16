namespace FluNET.Compilation.Sql;

/// <summary>Finds SQLite/ADO.NET `$name` parameters outside SQL literals and comments.</summary>
public static class SqlParameterScanner
{
    public static IReadOnlyList<string> Scan(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        List<string> names = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < sql.Length; index++)
        {
            char current = sql[index];
            if (current is '\'' or '"')
            {
                char quote = current;
                while (++index < sql.Length)
                {
                    if (sql[index] == quote)
                    {
                        if (index + 1 < sql.Length && sql[index + 1] == quote) { index++; continue; }
                        break;
                    }
                }
                continue;
            }

            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index += 2;
                while (index < sql.Length && sql[index] is not '\r' and not '\n') index++;
                index--;
                continue;
            }

            if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < sql.Length && !(sql[index] == '*' && sql[index + 1] == '/')) index++;
                index++;
                continue;
            }

            if (current != '$' || index + 1 >= sql.Length ||
                !(char.IsLetter(sql[index + 1]) || sql[index + 1] == '_')) continue;

            int start = ++index;
            while (index < sql.Length && (char.IsLetterOrDigit(sql[index]) || sql[index] == '_')) index++;
            string name = sql[start..index];
            if (seen.Add(name)) names.Add(name);
            index--;
        }
        return names;
    }
}
