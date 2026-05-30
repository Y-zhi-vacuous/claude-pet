using Newtonsoft.Json;
using ClaudePet.Models;

namespace ClaudePet.Utils;

public class ConfigStore
{
    private readonly string _path;

    public ConfigStore(string baseDir)
    {
        _path = Path.Combine(baseDir, "config.json");
    }

    public PetConfig Load()
    {
        if (!File.Exists(_path))
        {
            var defaults = new PetConfig();
            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(_path);
        return JsonConvert.DeserializeObject<PetConfig>(json) ?? new PetConfig();
    }

    public void Save(PetConfig config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(_path, json);
    }
}
