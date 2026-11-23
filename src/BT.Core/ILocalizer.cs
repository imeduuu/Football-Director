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
        var category = count == 0 && forms.ContainsKey("zero") ? "zero" : PluralCategory(Language, count);
        if (!forms.TryGetValue(category, out var template)) forms.TryGetValue("other", out template);
        if (template is not string text) throw new KeyNotFoundException($"Plural key '{key}' has no '{category}' or 'other' form.");
        var values = variables is null
            ? new Dictionary<string, object?>()
            : variables.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        values["count"] = count;
        return Interpolate(text, values);
    }

    private static string PluralCategory(string language, decimal count)
    {
        var languageCode = language.Split('-')[0].ToLowerInvariant();
        var integral = decimal.Truncate(count) == count;
        var absolute = count < 0 ? -count : count;
        var whole = integral ? decimal.ToInt64(absolute % 100) : -1;

        if (languageCode == "ar" && integral)
        {
            if (absolute == 0) return "zero";
            if (absolute == 1) return "one";
            if (absolute == 2) return "two";
            if (whole is >= 3 and <= 10) return "few";
            if (whole is >= 11 and <= 99) return "many";
            return "other";
        }

        if ((languageCode is "ru" or "uk" or "be") && integral)
        {
            var mod10 = (int)(absolute % 10);
            var mod100 = (int)(absolute % 100);
            if (mod10 == 1 && mod100 != 11) return "one";
            if (mod10 is >= 2 and <= 4 && (mod100 < 12 || mod100 > 14)) return "few";
            return "many";
        }

        if (languageCode == "pl" && integral)
        {
            var mod10 = (int)(absolute % 10);
            var mod100 = (int)(absolute % 100);
            if (absolute == 1) return "one";
            if (mod10 is >= 2 and <= 4 && (mod100 < 12 || mod100 > 14)) return "few";
            return "many";
        }

        if (languageCode is "cs" or "sk")
        {
            if (count == 1) return "one";
            if (integral && absolute is >= 2 and <= 4) return "few";
            return integral ? "other" : "many";
        }

        if ((languageCode is "fr" or "pt") && (absolute is 0 or 1)) return "one";
        return count == 1 ? "one" : "other";
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
        foreach (var pair in variables.OrderBy(pair => pair.Key, StringComparer.Ordinal))
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
        foreach (var pair in primary.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (pair.Value is Dictionary<string, object?> primaryObject && fallback.TryGetValue(pair.Key, out var fallbackValue) && fallbackValue is Dictionary<string, object?> fallbackObject)
                merged[pair.Key] = Merge(primaryObject, fallbackObject);
            else merged[pair.Key] = pair.Value;
        }
        return merged;
    }
}
