using SQLite;

namespace Cantimplora.Models;

[Table("Settings")]
public class StoredSetting
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}