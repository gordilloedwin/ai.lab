namespace ai.lab.service.Model.Outbound;

public class ChatRoomInitResponse
{
    public ChatRoomResponse? Room { get; set; }
    public List<ChatParticipantResponse> Participants { get; set; } = new();
    public List<ChatMessageResponse> Messages { get; set; } = new();
}