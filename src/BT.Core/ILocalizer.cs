using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BT.Core;

/// <summary>Resolves UI messages and formats values using an explicitly selected locale.</summary>
public interface ILocalizer
{
    string Language { get; }
    CultureInfo Culture { get; }
    string Get(string key, IReadOnlyDictionary<string, object?>? variables = null);
    string Plural(string key, decimal count, IReadOnlyDictionary<string, object?>? variables = null);
    string FormatNumber(decimal value, string? format = null);
    string FormatCurrency(decimal amount, string currencyCode, string? format = null);
    string FormatDate(DateTime value, string? format = null);
}

/// <summary>Loads a JSON catalog whose nested objects are addressed by dot-separated keys.</summary>
public sealed class JsonLocalizer : ILocalizer
{
    private readonly Dictionary<string, object?> _messages;

    private JsonLocalizer(string language, CultureInfo culture, Dictionary<string, object?> messages)
    {
        Language = language;
        Culture = culture;
        _messages = messages;
    }

    public string Language { get; }
    public CultureInfo Culture { get; }

    public static JsonLocalizer Load(string language, string catalogDirectory, string fallbackLanguage = "es")
    {
        if (string.IsNullOrWhiteSpace(language)) throw new ArgumentException("A language is required.", nameof(language));
        if (string.IsNullOrWhiteSpace(catalogDirectory)) throw new ArgumentException("A catalog directory is required.", nameof(catalogDirectory));
        var requested = LoadCatalog(language, catalogDirectory);
        var fallback = string.Equals(language, fallbackLanguage, StringComparison.OrdinalIgnoreCase)
            ? requested : LoadCatalog(fallbackLanguage, catalogDirectory);
        return new JsonLocalizer(language, CultureInfo.GetCultureInfo(language), Merge(requested, fallback));
    }

    public static JsonLocalizer LoadJson(string language, string json, string? fallbackJson = null)
    {
        if (string.IsNullOrWhiteSpace(language)) throw new ArgumentException("A language is required.", nameof(language));
        var requested = Parse(json);
        var fallback = fallbackJson is null ? requested : Parse(fallbackJson);
        return new JsonLocalizer(language, CultureInfo.GetCultureInfo(language), Merge(requested, fallback));
    }

    public string Get(string key, IReadOnlyDictionary<string, object?>? variables = null)
    {
        var value = Resolve(key);
        if (value is not string template) throw new KeyNotFoundException($"Localization key '{key}' was not found.");
        return Interpolate(template, variables);
    }

    public string Plural(string key, decimal count, IReadOnlyDictionary<string, object?>? variables = null)
    {
        var node = Resolve(key);
        if (node is not Dictionary<string, object?> forms) throw new InvalidOperationException($"Plural key '{key}' must contain forms such as 'one' and 'other'.");
        var category = count == 1 ? "one" : count == 0 ? "zero" : "other";
        if (!forms.TryGetValue(category, out var template)) forms.TryGetValue("other", out template);
        if (template is not string text) throw new KeyNotFoundException($"Plural key '{key}' has no '{category}' or 'other' form.");
        var values = variables is null
            ? new Dictionary<string, object?>()
            : variables.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        values["count"] = count;
        return Interpolate(text, values);
    }

    public string FormatNumber(decimal value, string? format = null) => value.ToString(format ?? "N", Culture);

    public string FormatCurrency(decimal amount, string currencyCode, string? format = null)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) throw new ArgumentException("An ISO currency code is required.", nameof(currencyCode));
        var currencyCulture = (CultureInfo)Culture.Clone();
        currencyCulture.NumberFormat.CurrencySymbol = currencyCode.ToUpperInvariant();
        return amount.ToString(format ?? "C", currencyCulture);
    }

    public string FormatDate(DateTime value, string? format = null) => value.ToString(format ?? "d", Culture);

    private object? Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A localization key is required.", nameof(key));
        object? node = _messages;
        foreach (var segment in key.Split('.'))
        {
            if (node is not Dictionary<string, object?> map || !map.TryGetValue(segment, out node))
                throw new KeyNotFoundException($"Localization key '{key}' was not found.");
        }
        return node;
    }

    private string Interpolate(string template, IReadOnlyDictionary<string, object?>? variables)
    {
        if (variables is null) return template;
        foreach (var pair in variables)
            template = template.Replace("{" + pair.Key + "}", Convert.ToString(pair.Value, Culture) ?? string.Empty, StringComparison.Ordinal);
        return template;
    }

    private static Dictionary<string, object?> LoadCatalog(string language, string directory)
    {
        var path = System.IO.Path.Combine(directory, language + ".json");
        if (!System.IO.File.Exists(path) && language.Contains('-', StringComparison.Ordinal))
            path = System.IO.Path.Combine(directory, language.Split('-')[0] + ".json");
        return Parse(System.IO.File.ReadAllText(path));
    }

    private static Dictionary<string, object?> Parse(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return ReadObject(document.RootElement);
    }

    private static Dictionary<string, object?> ReadObject(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind != System.Text.Json.JsonValueKind.Object) throw new FormatException("A localization catalog root must be a JSON object.");
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            result.Add(property.Name, property.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Object => ReadObject(property.Value),
                System.Text.Json.JsonValueKind.String => property.Value.GetString(),
                _ => throw new FormatException($"Catalog value '{property.Name}' must be a string or object.")
            });
        return result;
    }

    private static Dictionary<string, object?> Merge(Dictionary<string, object?> primary, Dictionary<string, object?> fallback)
    {
        var merged = new Dictionary<string, object?>(fallback, StringComparer.Ordinal);
        foreach (var pair in primary)
        {
            if (pair.Value is Dictionary<string, object?> primaryObject && fallback.TryGetValue(pair.Key, out var fallbackValue) && fallbackValue is Dictionary<string, object?> fallbackObject)
                merged[pair.Key] = Merge(primaryObject, fallbackObject);
            else merged[pair.Key] = pair.Value;
        }
        return merged;
    }
}
