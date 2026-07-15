# LabInvoiceSystem

<p align="center">
  English | <a href="./README.md">简体中文</a>
</p>

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.3.9-8B44AC)](https://avaloniaui.net/)
[![MVVM](https://img.shields.io/badge/Pattern-MVVM-0F766E)](https://learn.microsoft.com/dotnet/architecture/maui/mvvm)
[![Platforms](https://img.shields.io/badge/Platforms-Windows%20%7C%20Linux%20%7C%20macOS-0078D4)](https://github.com/MIGO-OvO/LabInvoiceSystem/releases)
[![Baidu OCR](https://img.shields.io/badge/OCR-Baidu%20VAT%20Invoice-2563EB)](https://cloud.baidu.com/product/ocr)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)

## Overview

LabInvoiceSystem is a desktop invoice management tool for laboratory, research group, and reimbursement
workflows. It uses Avalonia for the UI and .NET 8 for the application logic, with a workflow centered on
uploading invoices, recognizing them with OCR, reviewing extracted fields, archiving files, exporting
daily reimbursement packages, and reviewing spending statistics.

Version 2.0.0 ships self-contained x64/arm64 packages for Windows, Linux, and macOS. Platform-specific file
opening, deletion warnings, and OCR credential persistence follow the current operating system.

## Core Features

| Area | Capability |
| --- | --- |
| Invoice import | Upload PDF, JPG, JPEG, and PNG invoices through a file picker or drag and drop |
| PDF preview | Render the first PDF page with `PDFtoImage` and `SkiaSharp`; zoom, pan, and reset previews |
| OCR extraction | With explicit user consent, send the invoice image to Baidu VAT Invoice OCR and extract all supported fields |
| Manual review | Correct every exported field, or use local manual entry without uploading the image |
| Local archive | Store original invoice files by `YYYY-MM` and write same-name JSON metadata for each invoice |
| Reimbursement export | Export invoices by date as a ZIP package with an Excel detail sheet included |
| Statistics dashboard | Show total amount, invoice count, last-30-days amount, and a one-year spending heatmap |
| API management | Configure, test, and save Baidu OCR API Key and Secret Key from the dashboard |
| Operation log | Record upload, archive, delete, and export actions as a JSON log in the user profile |

## Project Status

| Metric | Current Value |
| --- | --- |
| Application type | Desktop invoice OCR, archive, and export tool |
| Target framework | `net8.0` |
| UI framework | Avalonia 11.3.9, Fluent theme |
| Architecture pattern | MVVM, `CommunityToolkit.Mvvm` |
| Main workspaces | Invoice import, invoice export, dashboard |
| C# source files | 31 |
| AXAML files | 8 |
| Icon assets | 8 |
| Release targets | Windows, Linux, and macOS `x64` / `arm64` self-contained packages |

## Getting Started

### Requirements

- Windows 10+, a mainstream Linux desktop distribution, or macOS 12+.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for running, debugging, and publishing.
- A Baidu Cloud OCR application's `API Key` and `Secret Key` for real invoice recognition; manual entry still works when OCR is not configured.

### Installation

```bash
git clone https://github.com/MIGO-OvO/LabInvoiceSystem.git
cd LabInvoiceSystem
```

### Run From Source

```bash
dotnet restore
dotnet run --project LabInvoiceSystem/LabInvoiceSystem.csproj
```

On Windows, you can also run the root launcher:

```bat
start.bat
```

### Download a Release

Download the archive for your operating system and architecture from [GitHub Releases](https://github.com/MIGO-OvO/LabInvoiceSystem/releases).

### Local Publish Example

```bash
dotnet publish LabInvoiceSystem/LabInvoiceSystem.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Replace `win-x64` with `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, or `osx-arm64` for another target.
Tags matching `v*` run the [GitHub Actions release workflow](./.github/workflows/release.yml), which builds all six packages, creates SHA-256 checksums, and publishes the Release.

## Usage Workflow

1. Open the app and start in "Invoice Import".
2. Click "Upload File" or drag invoice files into the left panel.
3. On first use, explicitly accept cloud OCR or choose manual entry; manual entry does not upload the image.
4. After OCR finishes, review the invoice preview and extracted fields on the right.
5. Correct date, amount, item name, payment method, invoice number, seller name, and seller tax ID.
6. Click "Confirm and Archive", or use "Archive All" for a complete batch.
7. Open "Invoice Export" to search, review, or edit archived records and export ZIP files. Windows deletion uses the Recycle Bin; Linux/macOS clearly warn before permanent deletion.
8. Open "Dashboard" to view total amount, invoice count, last-30-days amount, and the yearly heatmap.

## OCR Configuration

OCR is handled by [OcrService.cs](./LabInvoiceSystem/Services/OcrService.cs). The app obtains an `access_token`
through Baidu OAuth and then calls the VAT invoice OCR endpoint.

> Privacy: when cloud OCR is enabled, the invoice image required for recognition is sent to Baidu Cloud. The app asks for explicit consent before the first upload. Choosing manual entry keeps the image local. Archives, metadata, and exports remain on the device.

Use the "Configure Baidu API" button in the dashboard:

1. Enter the Baidu OCR `API Key` and `Secret Key`. The source code does not include default credentials.
2. Click "Test Connection" to verify token access.
3. Click "Save Configuration" to persist the settings. Windows protects the Secret Key with user-scoped DPAPI. Linux/macOS keep it for the current session only by default; use `LABINVOICESYSTEM_BAIDU_SECRET_KEY` to provide it through the environment. Leave the field blank to keep the current value.

When OCR is not configured, uploaded invoices skip network recognition and remain available for manual review. Do not commit production OCR credentials to the repository. Runtime settings are saved under the OS user application-data directory at:

```text
LabInvoiceSystem/appsettings.json
```

Operation logs are saved in the same profile directory:

```text
LabInvoiceSystem/upload_logs.json
```

## Archive Rules

Default paths come from `AppSettings`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `ArchiveDirectory` | `archive_data` | Root directory for archived invoices |
| `TempUploadDirectory` | `temp_uploads` | Temporary directory before archive |
| `ExportDirectory` | `export_data` | ZIP and Excel export directory |

Archived invoices are written as:

```text
archive_data/YYYY-MM/YYYYMMDD-item-paymentMethod-amount元.ext
archive_data/YYYY-MM/YYYYMMDD-item-paymentMethod-amount元.json
```

The JSON metadata is used to recover invoice date, amount, item, payment method, invoice number,
seller name, and seller tax ID more reliably than parsing the file name alone.

## Architecture

```mermaid
flowchart LR
    A["File picker or drag-and-drop upload"] --> B["FileManagerService saves temporary file"]
    B --> C{"Is it a PDF?"}
    C -- "Yes" --> D["PdfService renders first page"]
    C -- "No" --> E["Read image bytes directly"]
    D --> F["OcrService calls Baidu VAT Invoice OCR"]
    E --> F
    F --> G["InvoiceInfo awaiting review"]
    G --> H["Confirm and archive"]
    H --> I["archive_data/YYYY-MM file and JSON metadata"]
    I --> J["ZIP + Excel export"]
    I --> K["StatisticsService metrics and heatmap"]
```

## Repository Structure

```text
LabInvoiceSystem/
|-- README.md
|-- README.en.md
|-- LabInvoiceSystem.sln
|-- start.bat
|-- archive_data/                         # Default archive directory, runtime data
`-- LabInvoiceSystem/
    |-- App.axaml                         # Avalonia app styles, resources, and ViewLocator
    |-- Program.cs                        # Desktop app entry point
    |-- Assets/                           # ICO, SVG, and PNG icon assets
    |-- Models/                           # InvoiceInfo, ArchiveItem, StatisticsData, AppSettings
    |-- ViewModels/                       # Main window, import, export, and dashboard state/commands
    |-- Views/                            # AXAML pages and window
    |-- Services/                         # OCR, PDF, file, settings, statistics, and logging services
    |-- Styles/                           # Icons, theme colors, and shared control styles
    |-- Converters/                       # UI binding converters
    `-- Properties/PublishProfiles/        # Windows publish configuration
```

## Technology Stack

| Dependency | Purpose |
| --- | --- |
| `Avalonia`, `Avalonia.Desktop` | Desktop UI framework |
| `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter` | Fluent styling and font |
| `CommunityToolkit.Mvvm` | Observable properties, RelayCommand, and MVVM support |
| `Avalonia.Controls.DataGrid` | Archived invoice table display |
| `LiveChartsCore.SkiaSharpView.Avalonia` | Charts and statistics visualization |
| `MiniExcel` | Excel detail export |
| `PDFtoImage` | First-page PDF image rendering |
| `SkiaSharp` | Image rendering and processing |
| `Svg.Controls.Skia.Avalonia` | SVG resource rendering |

## Development Notes

- The active page is managed by [MainWindowViewModel.cs](./LabInvoiceSystem/ViewModels/MainWindowViewModel.cs).
- Views and view models are linked through [ViewLocator.cs](./LabInvoiceSystem/ViewLocator.cs).
- Theme changes are persisted to `AppSettings.ThemeMode`.
- ZIP exports use `YYYYMMDD+paymentMethod.zip` for one payment method, otherwise `YYYYMMDD_发票.zip`.
- This repository currently has no automated test project. Run at least `dotnet build` after logic changes.

## FAQ

### What should I check if OCR fails?

Verify network access to Baidu Cloud, confirm the API Key and Secret Key are valid, and review the error
message shown in the app. If OCR fails, the invoice still moves into review state, so you can fill in the
amount and item manually before archiving.

### What should I check if a PDF cannot be previewed or recognized?

Make sure the file is a real PDF, starts with `%PDF-`, is not empty, and is not corrupted. The app only
renders the first page for preview and OCR, so multi-page PDFs should have the invoice on the first page.

### Why is the dashboard empty?

The dashboard reads archived files. Finish "Confirm and Archive" in the import workspace first, then open
the dashboard and click "Refresh Data".

### Where does ZIP export output go?

By default, exports go to `export_data`. You can change the destination from the "Invoice Export" page with
"Set Export Path".

## Contributing

This repository does not currently include a dedicated contribution guide. Before submitting changes, keep
the existing MVVM layering, avoid committing local runtime data, OCR credentials, `bin` or `obj` outputs, and
run at least a project build verification.

## Contact and Issues

Use GitHub Issues for bugs, improvements, or usage questions:

[https://github.com/MIGO-OvO/LabInvoiceSystem/issues](https://github.com/MIGO-OvO/LabInvoiceSystem/issues)

Helpful reports include steps to reproduce, expected result, actual result, Windows version, invoice file type,
and any relevant screenshots or logs.

## License

This project is open source under the [MIT License](./LICENSE). You may use, copy, modify, merge, publish,
distribute, sublicense, and sell copies of the project under the MIT terms.

## Acknowledgements

This project is built with the following open source projects and services:

- [Avalonia UI](https://avaloniaui.net/)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- [LiveChartsCore](https://github.com/beto-rodriguez/LiveCharts2)
- [MiniExcel](https://github.com/mini-software/MiniExcel)
- [PDFtoImage](https://github.com/sungaila/PDFtoImage)
- [SkiaSharp](https://github.com/mono/SkiaSharp)
- [Baidu Cloud OCR](https://cloud.baidu.com/product/ocr)
