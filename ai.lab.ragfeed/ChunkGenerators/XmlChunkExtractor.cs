namespace ai.lab.ragfeed.ChunkGenerators;

public class XmlChunkExtractor
{
    public List<string> ExtractXmlChunks(string filePath)
    {
        var xmlContent = File.ReadAllText(filePath);
        var chunks = new List<string>();

        try
        {
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(xmlContent);

            if (xmlDoc.DocumentElement != null)
            {
                foreach (System.Xml.XmlNode node in xmlDoc.DocumentElement.ChildNodes)
                {
                    var chunkText = node.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(chunkText))
                    {
                        chunks.Add(chunkText);
                    }
                }
            }
        }
        catch (System.Xml.XmlException)
        {
            System.Console.WriteLine("Invalid XML format.");
        }

        return chunks;
    }
}