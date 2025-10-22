using Dapper;
using System.Data;
using System.Text;

namespace ai.lab.service.Helpers;

public class VectorHandler : SqlMapper.TypeHandler<float[]>
{
    public override void SetValue(IDbDataParameter parameter, float[]? value)
    {
        if (value == null || value.Length == 0)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        // MariaDB VECTOR type expects a string representation: "[val1,val2,val3,...]"
        // Format as JSON array without spaces
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < value.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(value[i].ToString("G", System.Globalization.CultureInfo.InvariantCulture));
        }
        sb.Append(']');
        
        parameter.Value = sb.ToString();
        parameter.DbType = DbType.String;
    }

    public override float[] Parse(object value)
    {
        if (value == null || value is DBNull)
            return Array.Empty<float>();

        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
            return Array.Empty<float>();

        // Remove brackets and split by comma
        stringValue = stringValue.Trim('[', ']');
        var parts = stringValue.Split(',');
        var result = new float[parts.Length];
        
        for (int i = 0; i < parts.Length; i++)
        {
            if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out float val))
            {
                result[i] = val;
            }
        }
        
        return result;
    }
}
