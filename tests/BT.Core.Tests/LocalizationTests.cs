using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BT.Core;
using Xunit;

namespace BT.Core.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void SpanishCatalogSupportsKeysVariablesPluralAndCultureFormatting()
    {
        var catalogPath = Path.Combine(FindRepositoryRoot(), "data", "i18n");
        var localizer = JsonLocalizer.Load("es-CL", catalogPath, "es");

        Assert.Equal("Presión alta", localizer.Get("ui.tactics.pressure.high"));
        Assert.Equal("Hola, Ana", localizer.Get("ui.welcome", new Dictionary<string, object?> { ["name"] = "Ana" }));
        Assert.Equal("2 jugadores", localizer.Plural("ui.players", 2));
        Assert.Contains("1.234", localizer.FormatNumber(1234));
        Assert.Contains("CLP", localizer.FormatCurrency(1234, "CLP"));
        Assert.Equal(new DateTime(2026, 11, 23).ToString("d", localizer.Culture), localizer.FormatDate(new DateTime(2026, 11, 23)));
    }

    [Fact]
    public void UiCodeDoesNotContainVisibleHardCodedLiterals()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        var violations = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, lineNumber: index + 1)))
            .Where(item => item.line.Contains("Text = \"", StringComparison.Ordinal) || item.line.Contains("Label = \"", StringComparison.Ordinal) || item.line.Contains("Caption = \"", StringComparison.Ordinal) || item.line.Contains("Title = \"", StringComparison.Ordinal))
            .Select(item => $"{item.path}:{item.lineNumber}: {item.line.Trim()}")
            .ToArray();

        Assert.True(violations.Length == 0, "Hard-coded UI literals found:\n" + string.Join("\n", violations));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BoardroomTouchline.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
