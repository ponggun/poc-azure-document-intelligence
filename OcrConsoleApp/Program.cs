using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Configuration;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;
using System.Text;
using System.Text.Json;

namespace OcrConsoleApp;

public static class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            string rootDocumentPath = Path.Combine(AppContext.BaseDirectory, "docs");
            string inputFolder = Path.Combine(rootDocumentPath, "input");
            string[] inputFiles = Directory.GetFiles(inputFolder, "*.pdf");
            if (inputFiles.Length == 0)
            {
                Console.WriteLine($"No PDF files found in {inputFolder}");
                return;
            }

            // Load configuration
            var configuration = LoadConfiguration();
            var endpoint = configuration["AzureDocumentIntelligence:Endpoint"] ?? throw new InvalidOperationException("Azure endpoint not configured");
            var apiKey = configuration["AzureDocumentIntelligence:ApiKey"] ?? throw new InvalidOperationException("Azure API key not configured");
            var mistralApiKey = configuration["MistralOCR:ApiKey"] ?? throw new InvalidOperationException("Mistral API key not configured");
            var mistralEndpoint = configuration["MistralOCR:Endpoint"] ?? "https://api.mistral.ai/v1/chat/completions";

            foreach (var inputPath in inputFiles)
            {
                string inputFileName = Path.GetFileName(inputPath);
                string outputPath = Path.Combine(rootDocumentPath, "output", Path.GetFileNameWithoutExtension(inputPath));

                Console.WriteLine($"Processing: {inputFileName}");
                Console.WriteLine($"Input path: {inputPath}");

                // Check if input file exists
                if (!File.Exists(inputPath))
                {
                    Console.WriteLine($"Error: Input file not found at {inputPath}");
                    continue;
                }

                // Split PDF into images
                Console.WriteLine($"Splitting PDF into images...");
                string imagesOutputDir = Path.Combine(outputPath, "images");
                string jsonAzureOutputDir = Path.Combine(outputPath, "AzureDocumentIntelligence", "json");
                string textAzureOutputDir = Path.Combine(outputPath, "AzureDocumentIntelligence", "text");
                string jsonMistralOutputDir = Path.Combine(outputPath, "MistralOCR", "json");
                string textMistralOutputDir = Path.Combine(outputPath, "MistralOCR", "text");
                string mdMistralOutputDir = Path.Combine(outputPath, "MistralOCR", "markdown");

                Directory.CreateDirectory(imagesOutputDir);
                Directory.CreateDirectory(jsonAzureOutputDir);
                Directory.CreateDirectory(textAzureOutputDir);
                Directory.CreateDirectory(jsonMistralOutputDir);
                Directory.CreateDirectory(mdMistralOutputDir);

                var pageImageFiles = SplitPdfToImages(inputPath, imagesOutputDir);

                foreach (var pageImageFile in pageImageFiles)
                {
                    // Create Azure Document Intelligence client
                    var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

                    // Perform OCR with Azure Document Intelligence
                    Console.WriteLine("Performing OCR with Azure Document Intelligence...");
                    var ocrResult = await PerformOcrAsync(client, pageImageFile);

                    // Save Azure OCR result as text file
                    string textOutputPath = Path.Combine(textAzureOutputDir, $"{Path.GetFileNameWithoutExtension(pageImageFile)}.txt");
                    await SaveOcrTextToFileAsync(textOutputPath, ocrResult);

                    // Save Azure OCR result as JSON file
                    string jsonOutputPath = Path.Combine(jsonAzureOutputDir, $"{Path.GetFileNameWithoutExtension(pageImageFile)}.json");
                    await SaveOcrJsonToFileAsync(jsonOutputPath, ocrResult);

                    // Perform OCR with Mistral
                    Console.WriteLine("Performing OCR with Mistral...");
                    var (mistralResultRaw, mistralResultObj) = await PerformMistralOcrAsync(mistralApiKey, mistralEndpoint, pageImageFile);

                    // Save Mistral OCR result as JSON file
                    string mistralJsonOutputPath = Path.Combine(jsonMistralOutputDir, $"{Path.GetFileNameWithoutExtension(pageImageFile)}.json");
                    await SaveMistralOcrJsonToFileAsync(mistralJsonOutputPath, mistralResultRaw);

                    // Save Mistral OCR result as md file
                    string mistralMdOutputPath = Path.Combine(mdMistralOutputDir, $"{Path.GetFileNameWithoutExtension(pageImageFile)}.md");
                    await SaveMistralOcrMarkdownToFileAsync(mistralMdOutputPath, mistralResultObj);
                }

                Console.WriteLine("Processing completed successfully!");
            }
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
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }

    private static async Task<AnalyzeResult> PerformOcrAsync(DocumentAnalysisClient client, string filePath)
    {
        using var stream = File.OpenRead(filePath);

        var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-layout", stream);
        var result = operation.Value;

        return result;
    }

    private static async Task SaveOcrTextToFileAsync(string outputPath, AnalyzeResult ocrResult)
    {
        var sb = new StringBuilder();
        foreach (var page in ocrResult.Pages)
        {
            Console.WriteLine($"Processing page {page.PageNumber}...");

            foreach (var line in page.Lines)
            {
                sb.AppendLine(line.Content);
            }
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString());
    }

    private static async Task SaveOcrJsonToFileAsync(string outputPath, AnalyzeResult ocrResult)
    {
        // serialize the OCR result to JSON
        var json = System.Text.Json.JsonSerializer.Serialize(ocrResult, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(outputPath, json);
    }

    private static async Task<(string, MistralOcrResponse)> PerformMistralOcrAsync(string apiKey, string endpoint, string filePath)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        // Convert image to base64
        var imageBytes = await File.ReadAllBytesAsync(filePath);
        var base64Image = Convert.ToBase64String(imageBytes);
        var imageExtension = Path.GetExtension(filePath).ToLowerInvariant();
        var mimeType = imageExtension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/png"
        };

        var requestBody = new
        {
            model = "mistral-ocr-latest",
            document = new
            {
                type = "image_url",
                image_url = $"data:{mimeType};base64,{base64Image}"
            },
            include_image_base64 = true
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(endpoint, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var ocrResponse = JsonSerializer.Deserialize<MistralOcrResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (ocrResponse == null)
            {
                throw new Exception("Failed to deserialize Mistral OCR response.");
            }

            return (responseContent, ocrResponse);
        }

        throw new Exception($"Mistral OCR API request failed: {response.StatusCode} - {responseContent}");
    }

    private static async Task SaveMistralOcrJsonToFileAsync(string outputPath, string ocrResult)
    {
        await File.WriteAllTextAsync(outputPath, ocrResult);
    }

    // save mistral as md
    private static async Task SaveMistralOcrMarkdownToFileAsync(string outputPath, MistralOcrResponse ocrResult)
    {
        await File.WriteAllTextAsync(outputPath, ocrResult.pages[0].markdown);
    }

    private static Task CreateSearchablePdfAsync(string outputPath, string ocrText)
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
                gfx.DrawString(line, font, PdfSharp.Drawing.XBrushes.Black, leftMargin.Point, yPosition.Point);
                yPosition = PdfSharp.Drawing.XUnit.FromPoint(yPosition.Point + lineHeight.Point);

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
    
    static List<string> SplitPdfToImages(string pdfFilePath, string outputDir)
    {
        var outputFiles = new List<string>();

        byte[] pdfBytes = File.ReadAllBytes(pdfFilePath);

        using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1080, 1440));
        int pageCount = docReader.GetPageCount();

        for (int i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);

            var rawBytes = pageReader.GetImage();
            int width = pageReader.GetPageWidth();
            int height = pageReader.GetPageHeight();

            using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            System.Runtime.InteropServices.Marshal.Copy(rawBytes, 0, bitmap.GetPixels(), rawBytes.Length);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            string outputPath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(pdfFilePath)}-{i + 1}.png");

            using var fs = File.OpenWrite(outputPath);
            data.SaveTo(fs);

            Console.WriteLine($"Saved: {outputPath}");
            outputFiles.Add(outputPath);
        }

        Console.WriteLine("✅ All pages converted.");

        return outputFiles;
    }
}