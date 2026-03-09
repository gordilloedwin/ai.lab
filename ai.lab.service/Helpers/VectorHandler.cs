using Dapper;
using System.Data;

namespace ai.lab.service.Helpers;

public class VectorHandler : SqlMapper.TypeHandler<float[]>
{
    private const int EXPECTED_DIMENSION = 4096;

    public override void SetValue(IDbDataParameter parameter, float[]? value)
    {
        if (value == null || value.Length == 0)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        // Validate vector dimension
        if (value.Length != EXPECTED_DIMENSION)
        {
            throw new InvalidOperationException(
                $"Vector dimension mismatch. Expected {EXPECTED_DIMENSION} dimensions, but got {value.Length}. " +
                $"Ensure the embedding model generates vectors of the correct dimension.");
        }

        // Validate for NaN or Infinity values
        for (int i = 0; i < value.Length; i++)
        {
            if (float.IsNaN(value[i]) || float.IsInfinity(value[i]))
            {
                throw new InvalidOperationException(
                    $"Invalid vector value at index {i}: {value[i]}. " +
                    $"Vector contains NaN or Infinity values. Please check the embedding generation.");
            }
        }

        // MariaDB VECTOR type stores as binary (4 bytes per float32)
        // Convert float[] to byte array
        var bytes = new byte[value.Length * sizeof(float)];
        Buffer.BlockCopy(value, 0, bytes, 0, bytes.Length);
        
        parameter.Value = bytes;
        parameter.DbType = DbType.Binary;
    }

    public override float[] Parse(object value)
    {
        if (value == null || value is DBNull)
            return Array.Empty<float>();

        // If it's already a byte array, convert back to float[]
        if (value is byte[] bytes)
        {
            var floats = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }

        // Fallback: try parsing as string (for legacy data)
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
