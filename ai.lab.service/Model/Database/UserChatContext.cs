namespace ai.lab.service.Model.Database;

public class UserChatContext
{
    public string Model { get; set; } = "None";

    public List<int> AiContext { get; set; } = new List<int>();
}