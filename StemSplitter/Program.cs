using System.CommandLine;
using StemSplitter.Models;
using StemSplitter.Services;

namespace StemSplitter;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("StemSplitter - Extract individual instrument stems from audio files")
        {
            Name = "stemsplitter"
        };

        // Input file argument
        var inputArg = new Argument<FileInfo>("input", "Input audio file (MP3 or WAV)")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        // Options
        var outputOption = new Option<DirectoryInfo?>(
            aliases: new[] { "-o", "--output" },
            description: "Output directory for stems (default: same as input file)");

        var modelOption = new Option<string>(
            aliases: new[] { "-m", "--model" },
            getDefaultValue: () => "htdemucs_6s",
            description: "Demucs model: htdemucs (4 stems) or htdemucs_6s (6 stems including guitar/piano)");

        var formatOption = new Option<string>(
            aliases: new[] { "-f", "--format" },
            getDefaultValue: () => "wav",
            description: "Output format: wav or mp3");

        var cpuOption = new Option<bool>(
            aliases: new[] { "--cpu" },
            getDefaultValue: () => false,
            description: "Use CPU only (slower but works without GPU)");

        var shiftsOption = new Option<int>(
            aliases: new[] { "-s", "--shifts" },
            getDefaultValue: () => 1,
            description: "Number of shifts for better quality (0-5, higher = better but slower)");

        var jobsOption = new Option<int>(
            aliases: new[] { "-j", "--jobs" },
            getDefaultValue: () => 1,
            description: "Number of parallel jobs");

        rootCommand.AddArgument(inputArg);
        rootCommand.AddOption(outputOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(cpuOption);
        rootCommand.AddOption(shiftsOption);
        rootCommand.AddOption(jobsOption);

        rootCommand.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArg);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var model = context.ParseResult.GetValueForOption(modelOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var cpu = context.ParseResult.GetValueForOption(cpuOption);
            var shifts = context.ParseResult.GetValueForOption(shiftsOption);
            var jobs = context.ParseResult.GetValueForOption(jobsOption);

            var exitCode = await RunSeparationAsync(
                input!, output, model, format, cpu, shifts, jobs);

            context.ExitCode = exitCode;
        });

        // Add info command
        var infoCommand = new Command("info", "Show audio file information");
        var infoInputArg = new Argument<FileInfo>("input", "Input audio file");
        infoCommand.AddArgument(infoInputArg);
        infoCommand.SetHandler(ShowAudioInfo, infoInputArg);
        rootCommand.AddCommand(infoCommand);

        // Add check command
        var checkCommand = new Command("check", "Check if Demucs is installed");
        checkCommand.SetHandler(CheckDemucsAsync);
        rootCommand.AddCommand(checkCommand);

        // Add install command
        var installCommand = new Command("install", "Show installation instructions for Demucs");
        installCommand.SetHandler(ShowInstallInstructions);
        rootCommand.AddCommand(installCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task<int> RunSeparationAsync(
        FileInfo input,
        DirectoryInfo? output,
        string model,
        string format,
        bool cpu,
        int shifts,
        int jobs)
    {
        PrintHeader();

        var options = new SeparationOptions
        {
            InputFile = input.FullName,
            OutputDirectory = output?.FullName,
            Model = model,
            OutputFormat = format,
            CpuOnly = cpu,
            Shifts = shifts,
            Jobs = jobs
        };

        Console.WriteLine($"Input file: {input.Name}");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"Format: {format}");
        Console.WriteLine();

        var audioProcessor = new AudioProcessor();
        var audioInfo = audioProcessor.GetAudioInfo(input.FullName);
        Console.WriteLine($"Audio info: {audioInfo}");
        Console.WriteLine();

        var separator = new StemSeparator();

        var progress = new Progress<string>(msg =>
        {
            Console.WriteLine($"  {msg}");
        });

        var result = await separator.SeparateAsync(options, progress);

        Console.WriteLine();

        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Separation completed successfully!");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Output directory: {result.OutputDirectory}");
            Console.WriteLine($"Processing time: {result.ProcessingTime:hh\\:mm\\:ss}");
            Console.WriteLine();
            Console.WriteLine("Extracted stems:");

            foreach (var (stemType, filePath) in result.StemFiles.OrderBy(x => x.Key.ToString()))
            {
                Console.WriteLine($"  - {stemType}: {Path.GetFileName(filePath)}");
            }

            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Separation failed!");
            Console.ResetColor();
            Console.WriteLine(result.ErrorMessage);
            return 1;
        }
    }

    static void ShowAudioInfo(FileInfo input)
    {
        PrintHeader();

        var audioProcessor = new AudioProcessor();
        var (isValid, error) = audioProcessor.ValidateInputFile(input.FullName);

        if (!isValid)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {error}");
            Console.ResetColor();
            return;
        }

        var info = audioProcessor.GetAudioInfo(input.FullName);
        Console.WriteLine($"File: {input.Name}");
        Console.WriteLine($"Path: {input.FullName}");
        Console.WriteLine($"Size: {input.Length / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine();
        Console.WriteLine($"Duration: {info.Duration:hh\\:mm\\:ss\\.fff}");
        Console.WriteLine($"Sample Rate: {info.SampleRate} Hz");
        Console.WriteLine($"Channels: {info.Channels}");
        Console.WriteLine($"Bit Depth: {info.BitsPerSample} bits");
    }

    static async Task CheckDemucsAsync()
    {
        PrintHeader();

        Console.WriteLine("Checking Demucs installation...");

        var separator = new StemSeparator();
        var (isInstalled, version) = await separator.CheckDemucsInstallationAsync();

        Console.WriteLine();

        if (isInstalled)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Demucs is installed! Version: {version ?? "unknown"}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Demucs is not installed or not in PATH.");
            Console.ResetColor();
            Console.WriteLine();
            ShowInstallInstructions();
        }
    }

    static void ShowInstallInstructions()
    {
        Console.WriteLine("Installation Instructions for Demucs:");
        Console.WriteLine("=====================================");
        Console.WriteLine();
        Console.WriteLine("1. Install Python 3.8+ if not already installed");
        Console.WriteLine("   https://www.python.org/downloads/");
        Console.WriteLine();
        Console.WriteLine("2. Install Demucs:");
        Console.WriteLine();
        Console.WriteLine("   For CPU only (slower):");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("   pip install demucs");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("   For GPU support (NVIDIA CUDA):");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("   pip install demucs torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("3. Verify installation:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("   demucs --help");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Available Models:");
        Console.WriteLine("-----------------");
        Console.WriteLine("  htdemucs    - 4 stems: drums, bass, vocals, other");
        Console.WriteLine("  htdemucs_6s - 6 stems: drums, bass, vocals, guitar, piano, other (recommended)");
        Console.WriteLine("  htdemucs_ft - Fine-tuned version for better vocals separation");
        Console.WriteLine();
        Console.WriteLine("Note: First run will download the model (~800MB for htdemucs_6s)");
    }

    static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
  ____  _                 ____        _ _ _   _
 / ___|| |_ ___ _ __ ___ / ___| _ __ | (_) |_| |_ ___ _ __
 \___ \| __/ _ \ '_ ` _ \\___ \| '_ \| | | __| __/ _ \ '__|
  ___) | ||  __/ | | | | |___) | |_) | | | |_| ||  __/ |
 |____/ \__\___|_| |_| |_|____/| .__/|_|_|\__|\__\___|_|
                               |_|
");
        Console.ResetColor();
        Console.WriteLine("Audio Stem Separator - Extract instruments from music");
        Console.WriteLine("======================================================");
        Console.WriteLine();
    }
}
