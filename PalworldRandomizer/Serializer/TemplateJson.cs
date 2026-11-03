using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalworldRandomizer.Models;

namespace PalworldRandomizer.Serializer;

/// <summary>Loads template JSON onto captured settings without depending on a WPF page.</summary>
public static class TemplateJson {
    /// <summary>Populates current settings for an object template, or returns null for a JSON null.</summary>
    public static FormData? Load(string json, Func<FormData> captureFormData) {
        var template = JsonConvert.DeserializeObject<JObject>(
            json,
            new JsonSerializerSettings {
                FloatParseHandling = FloatParseHandling.Double,
                DateParseHandling = DateParseHandling.None,
            }
        );
        if (template != null) {
            FormData formData = captureFormData();
            // Populate from the original text so known decimals never round through the inspection tokens.
            JsonConvert.PopulateObject(
                json,
                formData,
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }
            );
            return formData;
        }

        return null;
    }
}