# PostImage Uploader

> A production-quality .NET 8 console application that uploads images to [PostImages.org](https://postimages.org) with a **5-layer validation pipeline** — HTTP status verification, JSON parsing, URL legitimacy check, and live link accessibility testing.

🌐 [中文文档 → ReadMe-ZhCn.md](./ReadMe-ZhCn.md)

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20|%20macOS%20|%20Linux-lightgrey)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## Table of Contents

- [Introduction](#introduction)
- [Tech Stack](#tech-stack)
- [Environment Dependencies](#environment-dependencies)
- [Local Deployment & Setup](#local-deployment--setup)
- [Project Structure](#project-structure)
- [Development Guidelines](#development-guidelines)
- [Formal Verification with Dafny](#formal-verification-with-dafny)
- [Troubleshooting](#troubleshooting)

---

## Introduction

PostImage Uploader is a robust command-line application built with .NET 8 for uploading local images or remote image URLs directly to PostImages.org. Utilizing a strict 5-layer validation pipeline, it guarantees reliability and returns a direct link upon successful upload.

### Features

- ✅ Upload local image files to PostImages.org via the official JSON API
- ✅ Download and re-upload remote images by URL
- ✅ **5-dimensional validation**: file pre-check → HTTP 200 → JSON parse → URL domain check → live link HEAD/GET verify
- ✅ Precise failure diagnosis: `FileNotFound`, `FileTooLarge`, `UnsupportedFormat`, `NetworkError`, `Timeout`, `HttpError`, `JsonParseError`, `InvalidResponseUrl`, `LinkNotAccessible`
- ✅ Returns permanent `postimg.cc` direct links on success
- ✅ Core + CLI architecture utilizing modern .NET Dependency Injection and Logging abstractions.

---

## Tech Stack

The project's technical choices are structured into four main categories:

### 1. Frontend
- **None**: This is a pure command-line interface (CLI) application with no graphical user interface (GUI) or web frontend components.

### 2. Backend
| Component / Library | Version | Role in Project |
|---------------------|---------|----------------|
| **.NET 8** (Target Framework) | `net8.0` | Runtime and compilation target; minimum compatible version for C# 12 features. |
| **C# Language** | 12.0 | Primary programming language; uses top-level statements, primary constructors, and expression-bodied members. |
| `Microsoft.Extensions.Hosting` | `8.0.0` | Application host setup for DI, logging, and configuration in CLI. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `8.0.0` | Inversion of Control (IoC) and dependency injection abstractions for `App.Core`. |
| `Microsoft.Extensions.Http` | `8.0.0` | Provides `IHttpClientFactory` for managed HTTP connections. |
| `Microsoft.Extensions.Logging.Abstractions` | `8.0.0` | Logging interfaces for core library logic. |
| `Microsoft.Extensions.Logging.Console` | `8.0.0` | Console logging provider for CLI output. |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `8.0.0` | Strongly-typed configuration binding from `IConfiguration`. |

### 3. Infrastructure
| Component | Version | Role in Project |
|-----------|---------|----------------|
| **PostImages.org API** | Current | Target image hosting platform; endpoint: `POST https://postimages.org/json` |
| **postimg.cc CDN** | — | Image delivery CDN; generated links follow `https://postimg.cc/{id}/{hash}` |
| **HTTPS / TLS** | TLS 1.2+ | Enforces secure transport layer encryption for all communications. |

### 4. Toolchain
| Tool | Version | Role in Project |
|------|---------|----------------|
| **dotnet CLI** | 8.0.x+ | Primary CLI toolchain for building, running, testing, and publishing. |
| **MSBuild** | 17.x+ (bundled) | Configures and compiles the project and test dependencies. |
| **Moq** | `4.20.72` | Mocking framework for generating test doubles in unit tests. |
| **xUnit** | `2.5.3` | Testing framework used to run validation test cases. |
| **xunit.runner.visualstudio** | `2.5.3` | Test runner for executing xUnit tests within the .NET CLI. |
| **Microsoft.NET.Test.Sdk** | `17.8.0` | MSBuild test targets and infrastructure. |
| **coverlet.collector** | `6.0.0` | Code coverage data collection tool during testing. |
| **Dafny** | `4.x` (4.5+ recommended) | Formal verification toolchain; verifies the specification models in the [`formal/`](formal) directory (`dafny verify`). |

---

## Environment Dependencies

| Dependency | Minimum Version | Notes |
|------------|----------------|-------|
| **.NET SDK** | **8.0.100** | Required to compile and run the project. [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Dafny** (optional) | **4.x** | Only required to verify the formal models in `formal/`. [Download](https://dafny.org/downloads) |
| **Operating System** | Windows 10 / macOS 12 / Ubuntu 20.04 | Any OS with .NET 8 support |
| **Internet Access** | — | Required to reach `postimages.org` and `postimg.cc` |

---

## Local Deployment & Setup

### Windows (PowerShell / Command Prompt)

```powershell
# 1. Clone or download the project
git clone https://github.com/your-org/PostImageApp.git
cd PostImageApp

# 2. Verify .NET SDK version (must be 8.0+)
dotnet --version

# 3. Build the entire solution (both CLI and Tests)
dotnet build PostImageUploader.sln --configuration Release

# 4. Run CLI with a test image
dotnet run --project App.Cli.csproj -- testdata\test_valid.jpg
```

### macOS (Terminal / zsh)

```bash
# 1. Install .NET SDK (if not installed)
brew install --cask dotnet-sdk

# 2. Clone the project
git clone https://github.com/your-org/PostImageApp.git
cd PostImageApp

# 3. Build the solution
dotnet build PostImageUploader.sln --configuration Release

# 4. Run CLI with a test image
dotnet run --project App.Cli.csproj -- testdata/test_valid.jpg
```

### Linux (Ubuntu / Debian)

```bash
# 1. Install .NET SDK 8
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0

# 2. Clone the project
git clone https://github.com/your-org/PostImageApp.git
cd PostImageApp

# 3. Build the solution
dotnet build PostImageUploader.sln --configuration Release

# 4. Run CLI with a test image
dotnet run --project App.Cli.csproj -- testdata/test_valid.jpg
```

### Publish as Self-Contained Single Executable

```bash
# Windows x64
dotnet publish App.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/win

# macOS Apple Silicon (M1/M2/M3)
dotnet publish App.Cli.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish/mac-arm

# Linux x64
dotnet publish App.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux
```

---

## Project Structure

```
PostImageApp/
├── PostImageUploader.sln          # Visual Studio solution
├── App.Cli.csproj                 # CLI application project
├── Program.cs                     # CLI entry point (parses args, routes to core)
├── App.Core/                      # Core class library containing pure business logic
│   ├── App.Core.csproj            # Core project with abstractions and services
│   ├── Extensions/                # Dependency Injection extension methods
│   ├── Abstractions/              # Interfaces (IPostImageClient, IFileSystem, etc.)
│   └── ...                        # Core services, models, configurations
├── README.md                      # English documentation (this file)
├── ReadMe-ZhCn.md                 # Chinese documentation
├── documents/                     # Project documentation
├── formal/                        # Dafny formal-verification models (*.dfy)
│   ├── StringRepr.dfy             # String representation layer: seq<char> modeling of C# string
│   └── charprobe.dfy              # Axiomatic char-lowering probe (LowerPair / idempotence)
└── PostImageUploader.Tests/       # xUnit test project
    ├── PostImageUploader.Tests.csproj # Test project referencing App.Core and Moq
    ├── Unit/                      # Unit tests
    └── Integration/               # Integration tests
```

---

## Development Guidelines

1. **Architecture**: Keep business logic in `App.Core`. `App.Cli` is a thin wrapper handling DI setup, configuration, and console rendering.
2. **Configuration**: Utilize strongly-typed `IOptions<T>` classes. Avoid hardcoding magic strings.
3. **Abstractions**: External dependencies (Time, File System, Network) must be abstracted and injected for robust testing.
4. **Testing**: Add unit tests using `xUnit` and `Moq`. Write failing tests before fixing bugs.
5. **Code Style**: Ensure null safety is enabled. Add informative XML doc comments to public methods and classes.

### Running Tests

```bash
# Run all tests in the solution
dotnet test PostImageUploader.sln

# Run unit tests only
dotnet test --filter "Category=Unit" PostImageUploader.sln
```

---

## Formal Verification with Dafny

The [`formal/`](formal) directory contains **Dafny** specification models that formally describe the pure string-level logic used by `App.Core` (file-extension checks, case-insensitive URL prefix matching, MIME-related helpers, blank-string handling). Since Dafny has no built-in string type, C# `string` values are modeled as `seq<char>` character sequences.

### Model Files

| File | Module | Contents |
|------|--------|----------|
| `formal/StringRepr.dfy` | `StringRepr` | `Lower` (≈ `ToLowerInvariant`), `StartsWithCI` (≈ `OrdinalIgnoreCase` prefix match), `IsBlank` (≈ `IsNullOrWhiteSpace`), `ContainsSeq`, `GetExtension` (≈ `Path.GetExtension`), plus lemmas such as `LowerIdempotent`, `LowerConcat`, `PrefixTransitive`, and `ExtensionEmptyIffNoDot`. |
| `formal/charprobe.dfy` | `CharProbe` | An axiomatic probe of character lowering: a `LowerPair` map with an axiomatized domain/range and the idempotence lemma `ProbeIdem`. |

### Prerequisites

- **Dafny CLI 4.x** (4.5 or later recommended): install from the [official downloads page](https://dafny.org/downloads) (`npm install -g dafny`, the GitHub release archives, or your OS package manager).
- Dafny internally invokes **Z3** for SMT solving; the official release bundles a compatible Z3, so no separate installation is normally required.
- The .NET SDK is needed only if you additionally want to *compile/run* Dafny code; verification alone does not require it.

### Running Verification

From the repository root:

```bash
# Verify every .dfy file under formal/ (recommended — verify all models together)
dafny verify formal/*.dfy

# Verify a single model file
dafny verify formal/StringRepr.dfy

# Increase resource limits if proofs time out on slower machines
dafny verify formal/*.dfy --resource-limit 100
```

A successful run prints per-file summaries such as `0 errors, 21 verified` (the exact count depends on the current model contents). Any non-zero error count means a proof obligation failed and should be fixed in the model or its lemmas before merging changes to the corresponding C# logic.

> Note: each `.dfy` file is self-contained (e.g., `charprobe.dfy` declares its own axiomatic `LowerPair` map), so single-file verification works without extra include flags. These files use Dafny 4 syntax (`predicate`, `function`, `lemma`, `ensures Result ...`); Dafny 3.x is not supported.

### Optional: Compile & Execute Dafny Code

The current models are specification-only, but if you add an executable `method Main`, you can compile and run them like this:

```bash
dafny run formal/StringRepr.dfy          # JIT-style execution via C# backend
dafny build formal/StringRepr.dfy        # emit a compiled assembly/binary
```

---

## Troubleshooting

### `dotnet: command not found`

Install the .NET 8 SDK from the [official downloads page](https://dotnet.microsoft.com/download/dotnet/8.0) and ensure `dotnet` is on your `PATH`.

### Build warning `CS8604`

Check if you are correctly handling nullability. Leverage the explicit null-forgiving operator (`!`) only if you are certain a value is not null, or add proper null checks.

### Upload returns HTTP 403 Forbidden

Rate limiting might be in effect by PostImages.org. Wait a minute and try again.

### Dependency Injection Errors

Ensure you are registering core logic through the provided `IServiceCollection` extension methods in `App.Core` (e.g., `services.AddMyAppCore()`).
