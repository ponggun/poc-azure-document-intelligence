# Azure Document Intelligence OCR Console App

A .NET 8 Console Application that performs OCR on scanned-image PDF documents using Azure Document Intelligence (Layout OCR) and generates searchable PDFs.

## Features

- **OCR Processing**: Uses Azure Document Intelligence Layout OCR to extract text and layout from scanned PDF documents
- **Searchable PDF Output**: Generates a new PDF with selectable/searchable text layer
- **Text Output**: Optionally saves OCR results as plain text (.txt) files
- **Simple Configuration**: Easy-to-configure Azure credentials via `appsettings.json`
- **Command-line Interface**: Simple command-line usage for processing individual files

## Prerequisites

- .NET 8.0 SDK
- Azure Document Intelligence service (Cognitive Services)
- Azure subscription with Document Intelligence endpoint and API key

## Setup

### 1. Clone and Build

```bash
git clone <repository-url>
cd poc-azure-document-intelligence
cd OcrConsoleApp
dotnet build
```

### 2. Configure Azure Credentials

Edit `OcrConsoleApp/appsettings.json` and provide your Azure Document Intelligence service details:

```json
{
  "AzureDocumentIntelligence": {
    "Endpoint": "https://your-document-intelligence-endpoint.cognitiveservices.azure.com/",
    "ApiKey": "your-api-key-here"
  }
}
```

To get these credentials:
1. Go to [Azure Portal](https://portal.azure.com)
2. Create or navigate to your Document Intelligence service
3. Go to "Keys and Endpoint" section
4. Copy the endpoint URL and one of the API keys

### 3. Prepare Input Files

Place your scanned PDF files in the `/docs/input/` directory (relative to project root).

## Usage

### Basic Usage

From the `OcrConsoleApp` directory:

```bash
dotnet run -- filename.pdf
```

### Example

```bash
# Place your PDF in docs/input/sample.pdf
dotnet run -- sample.pdf
```

This will:
1. Read `/docs/input/sample.pdf`
2. Perform OCR using Azure Document Intelligence
3. Save searchable PDF to `/docs/output/sample.pdf`
4. Save extracted text to `/docs/output/sample.txt`

## Output

The application generates two files in `/docs/output/`:

1. **Searchable PDF** (`filename.pdf`) - A new PDF with selectable text content
2. **Text File** (`filename.txt`) - Plain text extracted from the OCR process

## Project Structure

```
poc-azure-document-intelligence/
├── OcrConsoleApp/
│   ├── Program.cs              # Main application logic
│   ├── OcrConsoleApp.csproj    # Project file with dependencies
│   └── appsettings.json        # Configuration file
├── docs/
│   ├── input/                  # Place input PDF files here
│   └── output/                 # Generated output files
└── README.md                   # This file
```

## Dependencies

- **Azure.AI.FormRecognizer** (4.1.0) - Azure Document Intelligence SDK
- **Microsoft.Extensions.Configuration** (9.0.5) - Configuration management
- **Microsoft.Extensions.Configuration.Json** (9.0.5) - JSON configuration provider
- **PdfSharp** (6.2.0) - PDF processing and generation

## Limitations (POC Scope)

- Processes one file at a time
- Simple PDF generation (creates new PDF with text, doesn't overlay on original images)
- Basic error handling
- No batch processing support

## Troubleshooting

### Common Issues

1. **"Azure endpoint not configured"** - Ensure `appsettings.json` has correct endpoint URL
2. **"Azure API key not configured"** - Ensure `appsettings.json` has valid API key
3. **"Input file not found"** - Ensure PDF file exists in `/docs/input/` directory
4. **Azure authentication errors** - Verify endpoint URL format and API key validity

### Getting Help

- Check Azure Document Intelligence service status in Azure Portal
- Verify your Azure subscription has sufficient quota
- Ensure the PDF file is not corrupted or password-protected

## Future Enhancements

- Batch processing support
- Enhanced PDF generation with image overlay
- Support for other document formats
- Advanced error handling and logging
- Configuration via environment variables
- Docker containerization