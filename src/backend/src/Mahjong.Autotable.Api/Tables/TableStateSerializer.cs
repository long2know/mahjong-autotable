using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mahjong.Autotable.Api.Tables;

public interface ITableStateSerializer
{
    string Serialize(TableGameState state);
    TableGameState Deserialize(string payload);
}

public sealed class TableStateSerializer : ITableStateSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public string Serialize(TableGameState state) => JsonSerializer.Serialize(state, SerializerOptions);

    public TableGameState Deserialize(string payload)
    {
        var state = JsonSerializer.Deserialize<TableGameState>(payload, SerializerOptions);
        return state ?? throw new InvalidOperationException("Unable to deserialize table state payload.");
    }
}
