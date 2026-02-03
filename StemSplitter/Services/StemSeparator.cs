using System.Diagnostics;
using System.Text;
using StemSplitter.Models;

namespace StemSplitter.Services;

/// <summary>
/// Handles audio stem separation using Demucs.
/// </summary>
public class StemSeparator
{
    private readonly AudioProcessor _audioProcessor;

    // Mapping from Demucs output names to stem name strings
    private static readonly Dictionary<string, (StemType Type, string Name)> DemucsToStem = new(StringComparer.OrdinalIgnoreCase)
    {
        { "drums", (StemType.Drums, "drums") },
        { "bass", (StemType.Bass, "bass") },
        { "vocals", (StemType.Vocals, "vocals") },
        { "guitar", (StemType.ElectricGuitar, "guitar") },
        { "piano", (StemType.Piano, "piano") },
        { "other", (StemType.Other, "other") }
    };

    public StemSeparator()
    {
        _audioProcessor = new AudioProcessor();
    }

    /// <summary>
    /// Checks if Demucs is installed and accessible.
    /// </summary>
    public async Task<(bool IsInstalled, string? Version)> CheckDemucsInstallationAsync()
    {
        try
        {
            var result = await RunProcessAsync("demucs", "--help", TimeSpan.FromSeconds(30));
            if (result.ExitCode == 0)
            {
                // Try to get version
                var versionResult = await RunProcessAsync("pip", "show demucs", TimeSpan.FromSeconds(30));
                var version = ExtractVersion(versionResult.Output);
                return (true, version);
            }
        }
        catch
        {
            // Demucs not found
        }

        return (false, null);
    }

    /// <summary>
    /// Separates audio into individual stems.
    /// </summary>
    public async Task<SeparationResult> SeparateAsync(SeparationOptions options, IProgress<string>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();

        // Validate input
        var (isValid, error) = _audioProcessor.ValidateInputFile(options.InputFile);
        if (!isValid)
            return SeparationResult.Failed(error!);

        // Check Demucs installation
        progress?.Report("Checking Demucs installation...");
        var (isInstalled, version) = await CheckDemucsInstallationAsync();
        if (!isInstalled)
        {
            return SeparationResult.Failed(
                "Demucs is not installed. Please install it using:\n" +
                "  pip install demucs\n" +
                "Or for GPU support:\n" +
                "  pip install demucs torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118");
        }

        progress?.Report($"Using Demucs {version ?? "unknown version"}");

        // Prepare output directory - same as input file location
        var finalOutputDir = options.OutputDirectory ?? Path.GetDirectoryName(options.InputFile) ?? ".";
        var trackName = Path.GetFileNameWithoutExtension(options.InputFile);

        // Use a temp directory for Demucs output
        var tempOutputDir = Path.Combine(Path.GetTempPath(), "StemSplitter", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempOutputDir);

        // Build Demucs command
        var args = BuildDemucsArguments(options, tempOutputDir);

        progress?.Report($"Starting separation with model: {options.Model}");
        progress?.Report("This may take several minutes depending on the audio length and your hardware...");

        // Run Demucs
        var result = await RunDemucsAsync(args, progress);

        if (result.ExitCode != 0)
        {
            // Clean up temp directory on failure
            try { Directory.Delete(tempOutputDir, true); } catch { }
            return SeparationResult.Failed($"Demucs failed with exit code {result.ExitCode}:\n{result.Error}");
        }

        // Find and map output files
        var stems = await FindAndMapStemFilesAsync(tempOutputDir, options.Model, trackName, options.OutputFormat);

        if (stems.Count == 0)
        {
            // Clean up temp directory on failure
            try { Directory.Delete(tempOutputDir, true); } catch { }
            return SeparationResult.Failed("No stem files were generated. Check the output directory.");
        }

        // Post-process: Copy/rename files to final location with standardized names
        var finalStems = await PostProcessStemsAsync(stems, finalOutputDir, trackName, progress);

        // Clean up temp directory
        try { Directory.Delete(tempOutputDir, true); } catch { }

        stopwatch.Stop();

        progress?.Report($"Separation complete! {finalStems.Count} stems extracted.");

        return SeparationResult.Succeeded(finalOutputDir, finalStems, stopwatch.Elapsed);
    }

    private string BuildDemucsArguments(SeparationOptions options, string outputDir)
    {
        var args = new StringBuilder();

        // Input file
        args.Append($"\"{options.InputFile}\"");

        // Model
        args.Append($" -n {options.Model}");

        // Output directory
        args.Append($" -o \"{outputDir}\"");

        // Output format
        if (options.OutputFormat.Equals("mp3", StringComparison.OrdinalIgnoreCase))
            args.Append(" --mp3");

        // CPU only
        if (options.CpuOnly)
            args.Append(" -d cpu");

        // Jobs
        if (options.Jobs > 1)
            args.Append($" -j {options.Jobs}");

        // Shifts for better quality
        if (options.Shifts > 0)
            args.Append($" --shifts {options.Shifts}");

        return args.ToString();
    }

    private async Task<ProcessResult> RunDemucsAsync(string arguments, IProgress<string>? progress)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "demucs",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                output.AppendLine(e.Data);
                progress?.Report(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                error.AppendLine(e.Data);
                // Demucs outputs progress to stderr
                if (e.Data.Contains("%") || e.Data.Contains("Separating"))
                    progress?.Report(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            Output = output.ToString(),
            Error = error.ToString()
        };
    }

    private async Task<Dictionary<string, (StemType Type, string SourcePath)>> FindAndMapStemFilesAsync(
        string outputDir, string model, string trackName, string format)
    {
        var stems = new Dictionary<string, (StemType Type, string SourcePath)>();
        var extension = format.Equals("mp3", StringComparison.OrdinalIgnoreCase) ? ".mp3" : ".wav";

        // Demucs outputs to: output_dir/model_name/track_name/stem.wav
        var possibleDirs = new[]
        {
            Path.Combine(outputDir, model, trackName),
            Path.Combine(outputDir, "htdemucs_6s", trackName),
            Path.Combine(outputDir, "htdemucs", trackName),
            Path.Combine(outputDir, "separated", model, trackName)
        };

        string? stemDir = null;
        foreach (var dir in possibleDirs)
        {
            if (Directory.Exists(dir))
            {
                stemDir = dir;
                break;
            }
        }

        if (stemDir == null)
        {
            // Search recursively for stem files
            var searchResult = await Task.Run(() =>
            {
                foreach (var dir in Directory.GetDirectories(outputDir, "*", SearchOption.AllDirectories))
                {
                    var files = Directory.GetFiles(dir, $"*{extension}");
                    if (files.Any(f => Path.GetFileNameWithoutExtension(f).Equals("drums", StringComparison.OrdinalIgnoreCase)))
                        return dir;
                }
                return null;
            });
            stemDir = searchResult;
        }

        if (stemDir == null)
            return stems;

        // Map found files to stem types
        var stemFiles = Directory.GetFiles(stemDir, $"*{extension}");
        foreach (var file in stemFiles)
        {
            var stemName = Path.GetFileNameWithoutExtension(file);
            if (DemucsToStem.TryGetValue(stemName, out var stemInfo))
            {
                stems[stemInfo.Name] = (stemInfo.Type, file);
            }
        }

        return stems;
    }

    private async Task<Dictionary<StemType, string>> PostProcessStemsAsync(
        Dictionary<string, (StemType Type, string SourcePath)> stems, string outputDir, string trackName, IProgress<string>? progress)
    {
        var finalStems = new Dictionary<StemType, string>();

        foreach (var (stemName, stemInfo) in stems)
        {
            var extension = Path.GetExtension(stemInfo.SourcePath);
            // Format: originalfilename_stemname.extension
            var destFileName = $"{trackName}_{stemName}{extension}";
            var destPath = Path.Combine(outputDir, destFileName);

            try
            {
                await Task.Run(() => File.Copy(stemInfo.SourcePath, destPath, overwrite: true));
                finalStems[stemInfo.Type] = destPath;
                progress?.Report($"  Created: {destFileName}");
            }
            catch (Exception ex)
            {
                progress?.Report($"  Warning: Failed to copy {stemName}: {ex.Message}");
            }
        }

        return finalStems;
    }

    private async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        using var cts = new CancellationTokenSource(timeout);

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new ProcessResult { ExitCode = -1, Error = "Process timed out" };
        }

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            Output = output.ToString(),
            Error = error.ToString()
        };
    }

    private static string? ExtractVersion(string pipShowOutput)
    {
        foreach (var line in pipShowOutput.Split('\n'))
        {
            if (line.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                return line.Substring(8).Trim();
        }
        return null;
    }

    private class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
}
