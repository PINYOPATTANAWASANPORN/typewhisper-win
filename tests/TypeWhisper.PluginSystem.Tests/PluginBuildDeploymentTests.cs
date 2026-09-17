using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace TypeWhisper.PluginSystem.Tests;

/// <summary>
/// Regression checks for Issue #426: ensures locally built plugins target the actual
/// Windows application output directory instead of a divergent plugin TFM path.
/// </summary>
public sealed class PluginBuildDeploymentTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Directory.Build.props")) &&
                Directory.Exists(Path.Combine(current, "plugins")))
            {
                return current;
            }
            var parent = Path.GetDirectoryName(current);
            if (parent == current) break;
            current = parent;
        }
        // Fallback relative to typical test output directory
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\"));
    }

    [Fact]
    public void DirectoryBuildProps_DefinesCentralizedAppTargetFrameworkAndOutputDir()
    {
        var propsPath = Path.Combine(RepoRoot, "Directory.Build.props");
        Assert.True(File.Exists(propsPath), $"Directory.Build.props not found at: {propsPath}");

        var doc = XDocument.Load(propsPath);
        var targetFramework = doc.Descendants("TypeWhisperAppTargetFramework").FirstOrDefault()?.Value;
        var outputDir = doc.Descendants("TypeWhisperAppOutputDir").FirstOrDefault()?.Value;

        Assert.False(string.IsNullOrWhiteSpace(targetFramework), "TypeWhisperAppTargetFramework must be defined");
        Assert.False(string.IsNullOrWhiteSpace(outputDir), "TypeWhisperAppOutputDir must be defined");

        Assert.Equal("net10.0-windows10.0.19041.0", targetFramework);
        Assert.Contains("src\\TypeWhisper.Windows\\bin", outputDir);
    }

    [Fact]
    public void WindowsAppProject_TargetFrameworkMatchesDirectoryBuildProps()
    {
        var propsPath = Path.Combine(RepoRoot, "Directory.Build.props");
        var appCsProjPath = Path.Combine(RepoRoot, @"src\TypeWhisper.Windows\TypeWhisper.Windows.csproj");

        Assert.True(File.Exists(propsPath));
        Assert.True(File.Exists(appCsProjPath));

        var propsDoc = XDocument.Load(propsPath);
        var appDoc = XDocument.Load(appCsProjPath);

        var centralizedTfm = propsDoc.Descendants("TypeWhisperAppTargetFramework").FirstOrDefault()?.Value;
        var appTfm = appDoc.Descendants("TargetFramework").FirstOrDefault()?.Value;

        Assert.Equal(centralizedTfm, appTfm);
    }

    [Theory]
    [InlineData(@"plugins\TypeWhisper.Plugin.WhisperCpp\TypeWhisper.Plugin.WhisperCpp.csproj")]
    [InlineData(@"plugins\TypeWhisper.Plugin.FillerWords\TypeWhisper.Plugin.FillerWords.csproj")]
    [InlineData(@"plugins\TypeWhisper.Plugin.OpenAi\TypeWhisper.Plugin.OpenAi.csproj")]
    public void PluginProjects_UseCentralizedAppOutputDir(string relativeCsProjPath)
    {
        var projPath = Path.Combine(RepoRoot, relativeCsProjPath);
        Assert.True(File.Exists(projPath), $"Plugin project not found: {projPath}");

        var content = File.ReadAllText(projPath);
        Assert.Contains("TypeWhisperAppOutputDir", content);
        Assert.DoesNotContain(@"\src\TypeWhisper.Windows\bin\$(Configuration)\$(TargetFramework)", content);
    }
}
