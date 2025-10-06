namespace ai.lab.service.Options;

public class OtelOptions
{
    public bool Enabled { get; set; } = false;
    public bool EnableConsoleExporter { get; set; } = false;
    public string ExporterType { get; set; } = string.Empty;
    public ExporterOptions ExporterOptions { get; set; } = new();
}

public class ExporterOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
}