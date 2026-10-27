using System.Collections.Generic;
using System.Text;

// Builds the flat JSON object sent to the backend. Numbers stay numbers so the Sheet stores them as such.
public static class ResultJson
{
    public static string Build(IReadOnlyList<SessionData.Field> fields)
    {
        var sb = new StringBuilder("{");
        for (int i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(',');
            AppendString(sb, fields[i].Key);
            sb.Append(':');
            if (fields[i].IsNumber)
                sb.Append(fields[i].Value);
            else
                AppendString(sb, fields[i].Value);
        }
        return sb.Append('}').ToString();
    }

    static void AppendString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
