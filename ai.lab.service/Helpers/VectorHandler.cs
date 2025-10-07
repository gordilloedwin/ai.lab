using Dapper;
using System.Data;
using System.Text.Json;

namespace ai.lab.service.Helpers;

public class VectorHandler : SqlMapper.TypeHandler<float[]>
{
    public override void SetValue(IDbDataParameter parameter, float[]? value)
    {
        parameter.Value = JsonSerializer.Serialize(value); // or convert to byte[]
    }

    public override float[] Parse(object value)
    {
        return JsonSerializer.Deserialize<float[]>(value.ToString() ?? "[]")!;
    }
}
