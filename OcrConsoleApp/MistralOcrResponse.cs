using System.Collections.Generic;

namespace OcrConsoleApp
{
    public class MistralOcrPage
    {
        public int index { get; set; }
        public string markdown { get; set; } = string.Empty;
        public List<string> images { get; set; } = new();
        public MistralOcrDimensions dimensions { get; set; } = new();
    }

    public class MistralOcrDimensions
    {
        public int dpi { get; set; }
        public int height { get; set; }
        public int width { get; set; }
    }

    public class MistralOcrUsageInfo
    {
        public int pages_processed { get; set; }
        public int doc_size_bytes { get; set; }
    }

    public class MistralOcrResponse
    {
        public List<MistralOcrPage> pages { get; set; } = new();
        public string model { get; set; } = string.Empty;
        public object? document_annotation { get; set; }
        public MistralOcrUsageInfo usage_info { get; set; } = new();
    }
}
