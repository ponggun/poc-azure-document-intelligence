using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Text;

namespace OcrConsoleApp;

public class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: dotnet run -- <filename.pdf>");
                Console.WriteLine("Example: dotnet run -- sample.pdf");
                return;
            }

            string inputFileName = args[0];
            string inputPath = Path.Combine("..", "docs", "input", inputFileName);
            string outputPath = Path.Combine("..", "docs", "output", inputFileName);
            string textOutputPath = Path.Combine("..", "docs", "output", Path.ChangeExtension(inputFileName, ".txt"));

            // Load configuration
            var configuration = LoadConfiguration();
            var endpoint = configuration["AzureDocumentIntelligence:Endpoint"] ?? throw new InvalidOperationException("Azure endpoint not configured");
            var apiKey = configuration["AzureDocumentIntelligence:ApiKey"] ?? throw new InvalidOperationException("Azure API key not configured");

            Console.WriteLine($"Processing: {inputFileName}");
            Console.WriteLine($"Input path: {inputPath}");
            Console.WriteLine($"Output path: {outputPath}");

            // Check if input file exists
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Error: Input file not found at {inputPath}");
                return;
            }

            // Create Azure Document Intelligence client
            var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

            // Perform OCR
            Console.WriteLine("Performing OCR with Azure Document Intelligence...");
            var ocrResult = await PerformOcrAsync(client, inputPath);

            // Save OCR result as text file
            Console.WriteLine($"Saving OCR text to: {textOutputPath}");
            await File.WriteAllTextAsync(textOutputPath, ocrResult);

            // Create searchable PDF
            Console.WriteLine($"Creating searchable PDF: {outputPath}");
            await CreateSearchablePdfAsync(inputPath, outputPath, ocrResult);

            Console.WriteLine("Processing completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }

    private static async Task<string> PerformOcrAsync(DocumentAnalysisClient client, string filePath)
    {
        using var stream = File.OpenRead(filePath);
        
        var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-layout", stream);
        var result = operation.Value;

        var textBuilder = new StringBuilder();
        
        foreach (var page in result.Pages)
        {
            Console.WriteLine($"Processing page {page.PageNumber}...");
            
            foreach (var line in page.Lines)
            {
                textBuilder.AppendLine(line.Content);
            }
            
            textBuilder.AppendLine(); // Add line break between pages
        }

        return textBuilder.ToString();
    }

    private static Task CreateSearchablePdfAsync(string inputPath, string outputPath, string ocrText)
    {
        // For this POC, we'll create a simple approach:
        // 1. Create a new PDF with the OCR text as selectable content
        // 2. In a production scenario, you would overlay invisible text on the original images
        
        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
        var font = new PdfSharp.Drawing.XFont("Arial", 12);

        // Split text into lines and add to PDF
        var lines = ocrText.Split('\n');
        var yPosition = PdfSharp.Drawing.XUnit.FromPoint(50);
        var leftMargin = PdfSharp.Drawing.XUnit.FromPoint(50);
        var lineHeight = PdfSharp.Drawing.XUnit.FromPoint(15);
        var bottomMargin = PdfSharp.Drawing.XUnit.FromPoint(50);
        
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                gfx.DrawString(line, font, PdfSharp.Drawing.XBrushes.Black, leftMargin, yPosition);
                yPosition += lineHeight;
                
                // Add new page if needed
                if (yPosition.Point > page.Height.Point - bottomMargin.Point)
                {
                    page = document.AddPage();
                    gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                    yPosition = PdfSharp.Drawing.XUnit.FromPoint(50);
                }
            }
        }

        gfx.Dispose();
        document.Save(outputPath);
        document.Close();
        
        return Task.CompletedTask;
    }
}
