# StemSplitter

A C# command-line tool for extracting individual instrument stems from audio files (MP3, WAV, FLAC, etc.).

## Features

- Extract stems: **Drums**, **Bass**, **Guitar**, **Piano**, **Vocals**, and **Other**
- Supports input formats: MP3, WAV, FLAC, OGG, M4A, AAC
- Output formats: WAV or MP3
- GPU acceleration support (NVIDIA CUDA)
- Progress reporting during separation

## Prerequisites

### 1. .NET 8.0 SDK
Download from: https://dotnet.microsoft.com/download/dotnet/8.0

### 2. Python 3.8+
Download from: https://www.python.org/downloads/

### 3. Demucs (Facebook's audio source separation)

**For CPU only (slower):**
```bash
pip install demucs
```

**For GPU support (NVIDIA CUDA - recommended for speed):**
```bash
pip install demucs torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
```

## Building

```bash
cd StemSplitter
dotnet build
```

## Usage

### Basic Usage

```bash
# Separate an audio file into stems
dotnet run -- "path/to/song.mp3"

# Or after building:
./StemSplitter "path/to/song.mp3"
```

### Options

```bash
# Specify output directory
dotnet run -- "song.mp3" -o "./output"

# Use 4-stem model (faster)
dotnet run -- "song.mp3" -m htdemucs

# Use 6-stem model with guitar/piano separation (default)
dotnet run -- "song.mp3" -m htdemucs_6s

# Output as MP3 instead of WAV
dotnet run -- "song.mp3" -f mp3

# Use CPU only (no GPU)
dotnet run -- "song.mp3" --cpu

# Higher quality (more shifts, slower)
dotnet run -- "song.mp3" -s 5
```

### Commands

```bash
# Check if Demucs is installed
dotnet run -- check

# Show audio file information
dotnet run -- info "song.mp3"

# Show installation instructions
dotnet run -- install
```

## Available Models

| Model | Stems | Description |
|-------|-------|-------------|
| `htdemucs` | 4 | drums, bass, vocals, other |
| `htdemucs_6s` | 6 | drums, bass, vocals, guitar, piano, other |
| `htdemucs_ft` | 4 | Fine-tuned for better vocals |

## Output

Stems are saved to: `[output_dir]/stems/[track_name]/`

Example output files:
```
stems/
  mysong/
    mysong_Drums.wav
    mysong_Bass.wav
    mysong_Vocals.wav
    mysong_ElectricGuitar.wav
    mysong_Piano.wav
    mysong_Other.wav
```

## Notes on Stem Types

The standard Demucs models provide these separations:
- **Drums** - Percussion, drums, cymbals
- **Bass** - Bass guitar, bass synth
- **Vocals** - Lead vocals, backing vocals
- **Guitar** - Electric and acoustic guitars (htdemucs_6s only)
- **Piano** - Piano, keys, synths (htdemucs_6s only)
- **Other** - Everything else (strings, synths, effects, etc.)

For more specific separations like electric vs acoustic guitar, or extracting strings separately, you would need specialized models or additional processing.

## Troubleshooting

### "Demucs is not installed"
Run `pip install demucs` and ensure Python is in your PATH.

### Slow processing
- Enable GPU support with CUDA
- Use `htdemucs` model instead of `htdemucs_6s`
- Reduce shifts with `-s 0`

### Out of memory
- Use `--cpu` flag
- Close other applications
- Process shorter audio segments

## License

MIT License
