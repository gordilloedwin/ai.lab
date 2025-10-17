# Chat System Implementation - Files Created

## Date: 2025-10-16

## Overview
Multi-user chat rooms with AI participant, supporting up to 30 users per room with real-time presence tracking and read receipts.

---

### 1. Database Migration
**File:** `Migrations/001_CreateChatTables.sql`

**Tables Created:**
- `chat_rooms` - Chat room metadata (title, creator, AI model, max participants)
- `chat_participants` - Many-to-many relationship with presence tracking
- `chat_messages` - All messages (user and AI) with timestamps
- `chat_read_receipts` - Unread message tracking per user per room

**Views Created:**
- `vw_active_chat_participants` - Currently connected users per room
- `vw_chat_room_stats` - Room statistics (participant count, message count, etc.)
- `vw_unread_message_counts` - Unread messages per user per room

**Key Features:**
- ✅ Hard limit of 30 participants per room
- ✅ Real-time presence via `is_currently_connected` + `connection_id`
- ✅ AI messages persisted with `sender_type = 'ai'` and `sender_email = NULL`
- ✅ Users explicitly leave via `left_at` timestamp
- ✅ Read receipts for unread message counts
- ✅ No room privacy (anyone can join if space available)

---

### 2. Database Models (`Model/Database/`)

#### `ChatRoom.cs`
- Properties: Id, Title, CreatedByEmail, AiModel, MaxParticipants, CreatedAt, UpdatedAt, IsActive
- Default max participants: 30

#### `ChatParticipant.cs`
- Properties: Id, ChatRoomId, UserEmail, JoinedAt, LeftAt, IsCurrentlyConnected, ConnectionId, LastSeenAt
- Tracks join/leave events and real-time presence

#### `ChatMessage.cs`
- Properties: Id, ChatRoomId, SenderEmail (nullable), SenderType, Content, CreatedAt
- SenderType: "user" or "ai"
- SenderEmail is NULL for AI messages

#### `ChatReadReceipt.cs`
- Properties: Id, ChatRoomId, UserEmail, LastReadMessageId, ReadAt
- Tracks last read message per user per room

---

### 3. Response Models (`Model/Outbound/`)

#### `ChatRoomResponse.cs`
- Extended room info with stats: CurrentParticipantCount, TotalMessageCount, UnreadMessageCount
- Flags: IsActive, IsFull, IsUserInRoom

#### `ChatMessageResponse.cs`
- Message with sender details: SenderName, SenderAvatarUri, IsOwnMessage
- Enriched with user information for UI display

#### `ChatParticipantResponse.cs`
- Participant with user details: UserName, AvatarUri, TimeInRoomSeconds, IsCurrentUser
- Real-time presence: IsCurrentlyConnected, LastSeenAt

---

### 4. Request Models (`Model/Inbound/`)

#### `CreateChatRoomRequest.cs`
- Validation: Title (3-255 chars), AiModel (optional, max 100), MaxParticipants (2-30)
- Defaults to deepseek-coder:6.7b if no model specified

#### `SendChatMessageRequest.cs`
- Validation: Content (1-10000 chars)
- Used for sending user messages to chat room

---

## Database Schema Design Decisions

### 1. **Participant Tracking**
```
Decision: Use `chat_participants` table with temporal tracking
Rationale: 
- Allows historical analysis (who was in room when)
- Supports "X user joined/left" notifications
- Enables "Last seen" functionality
- Users can rejoin same room multiple times
```

### 2. **AI Message Storage**
```
Decision: AI messages in `chat_messages` with sender_type='ai', sender_email=NULL
Rationale:
- Unified message history
- Simplifies chronological ordering
- Easy to query "all messages in room" without unions
- AI appears in message stream, not as fake participant
```

### 3. **Read Receipts**
```
Decision: Track last_read_message_id per user per room
Rationale:
- Efficient unread count: COUNT(*) WHERE message_id > last_read_id
- Single row per user per room (space efficient)
- Updates on every read action
```

### 4. **Hard Limit Enforcement**
```
Decision: max_participants column with application-level checks
Rationale:
- Database stores limit, application enforces
- Can be adjusted per room if needed
- Check before JoinChatRoom: SELECT COUNT(*) WHERE is_currently_connected
```

### 5. **Explicit Leave Mechanism**
```
Decision: left_at timestamp + button action required
Rationale:
- Users don't auto-leave on disconnect (might be network issue)
- Prevents accidental removal from active rooms
- left_at=NULL means "still member" even if offline
- UI button sets left_at and marks is_currently_connected=FALSE
```

---

## SQL Migration Usage

### To Apply Migration:
```bash
# Option 1: Using MySQL command line
mysql -h localhost -u root -p ai_lab_db < Migrations/001_CreateChatTables.sql

# Option 2: Using VS Code or MySQL Workbench
# Open file and execute against ai_lab_db database
```

### To Rollback (if needed):
```sql
DROP VIEW IF EXISTS vw_unread_message_counts;
DROP VIEW IF EXISTS vw_chat_room_stats;
DROP VIEW IF EXISTS vw_active_chat_participants;
DROP TABLE IF EXISTS chat_read_receipts;
DROP TABLE IF EXISTS chat_messages;
DROP TABLE IF EXISTS chat_participants;
DROP TABLE IF EXISTS chat_rooms;
```

---

## Next Steps (Implementation Order)

### Phase 1: Service Layer ✅ NEXT
1. Create `Services/Common/IChatService.cs` interface
2. Create `Services/ChatService.cs` implementation
3. Register in `Program.cs`: `builder.Services.AddScoped<IChatService, ChatService>();`

**Key Methods Needed:**
```csharp
// Room management
Task<ChatRoomResponse> CreateChatRoomAsync(string userEmail, CreateChatRoomRequest request);
Task<List<ChatRoomResponse>> GetUserChatRoomsAsync(string userEmail);
Task<ChatRoomResponse?> GetChatRoomByIdAsync(long chatRoomId, string userEmail);

// Participant management
Task<bool> JoinChatRoomAsync(long chatRoomId, string userEmail, string connectionId);
Task<bool> LeaveChatRoomAsync(long chatRoomId, string userEmail);
Task<List<ChatParticipantResponse>> GetChatParticipantsAsync(long chatRoomId);
Task<int> GetActiveParticipantCountAsync(long chatRoomId);
Task MarkUserAsConnectedAsync(long chatRoomId, string userEmail, string connectionId);
Task MarkUserAsDisconnectedAsync(long chatRoomId, string userEmail, string connectionId);

// Message management
Task<List<ChatMessageResponse>> GetChatMessagesAsync(long chatRoomId, string userEmail, int limit = 100);
Task<ChatMessageResponse> AddUserMessageAsync(long chatRoomId, string userEmail, string content);
Task<ChatMessageResponse> AddAiMessageAsync(long chatRoomId, string content);

// Read receipts
Task UpdateReadReceiptAsync(long chatRoomId, string userEmail, long lastReadMessageId);
Task<int> GetUnreadMessageCountAsync(long chatRoomId, string userEmail);
```

### Phase 2: SignalR Hub Extensions
1. Extend `Hub/AiLabHub.cs` with chat methods:
   - `JoinChatRoom(long chatRoomId)`
   - `LeaveChatRoom(long chatRoomId)`
   - `SendUserMessage(long chatRoomId, string content)`
   - `StreamAiResponse(long chatRoomId, string prompt)`
   - `MarkMessagesAsRead(long chatRoomId, long messageId)`

### Phase 3: API Controllers
1. Create `Controllers/ChatController.cs` with REST endpoints:
   - `POST /chat/rooms` - Create room
   - `GET /chat/rooms` - List user's rooms
   - `GET /chat/rooms/{id}` - Get room details
   - `GET /chat/rooms/{id}/messages` - Get message history
   - `GET /chat/rooms/{id}/participants` - Get participants
   - `POST /chat/rooms/{id}/join` - Join room
   - `POST /chat/rooms/{id}/leave` - Leave room

### Phase 4: UI Components
1. Create `Pages/Chat.razor` - Main chat interface
2. Create `Pages/ChatList.razor` - List of rooms
3. Update `Pages/Dashboard.razor` - Add chat navigation

### Phase 5: Testing & Refinement
1. Test multi-user scenarios
2. Test AI message persistence
3. Test read receipts
4. Test participant limits
5. Test explicit leave functionality

---

## Database Performance Considerations

### Indexes Created:
- ✅ `chat_rooms`: created_by, created_at, is_active
- ✅ `chat_participants`: chat_room_id, user_email, active participants composite
- ✅ `chat_messages`: chat_room_id, sender_email, created_at, room+time composite
- ✅ `chat_read_receipts`: unique (room, user), user+room composite

### Query Optimization:
- Views pre-join commonly needed data
- Composite indexes for frequent WHERE clauses
- Foreign keys with proper ON DELETE actions

---

## Configuration Requirements

### AppSettings (no changes needed):
```json
{
  "DatabaseOptions": {
    "MariaDbConnectionString": "server=localhost;port=3306;database=ai_lab_db;user=root;password=edwin;"
  }
}
```

### SignalR Hub Path (already configured):
```csharp
app.MapHub<AiLabHub>("/ailabchat");
```

---

## Testing the Migration

### 1. Check Table Creation:
```sql
SHOW TABLES LIKE 'chat_%';
-- Should show: chat_messages, chat_participants, chat_read_receipts, chat_rooms
```

### 2. Check Views:
```sql
SHOW FULL TABLES WHERE table_type = 'VIEW';
-- Should show: vw_active_chat_participants, vw_chat_room_stats, vw_unread_message_counts
```

### 3. Verify Constraints:
```sql
SELECT 
    TABLE_NAME,
    CONSTRAINT_NAME,
    CONSTRAINT_TYPE
FROM information_schema.TABLE_CONSTRAINTS
WHERE TABLE_SCHEMA = 'ai_lab_db' 
  AND TABLE_NAME LIKE 'chat_%';
```

### 4. Test Insert (after migration):
```sql
-- Create a test room
INSERT INTO chat_rooms (title, created_by_email, ai_model)
VALUES ('Test Room', 'test@ai.lab', 'deepseek-coder:6.7b');

-- Check views
SELECT * FROM vw_chat_room_stats;
```

---

## Notes

- **Cascade Deletes**: Deleting a chat_room will cascade delete all participants, messages, and read receipts
- **User Deletion**: Deleting a user will cascade delete their chat_rooms and read_receipts, but messages will have sender_email set to NULL (preserving history)
- **AI Messages**: Always have sender_email=NULL and sender_type='ai'
- **Explicit Leave**: Users must click "Leave Room" button to set left_at timestamp
- **Disconnection ≠ Leaving**: SignalR disconnect only sets is_currently_connected=FALSE, not left_at
- **Rejoin Support**: Users can rejoin rooms they previously left (creates new chat_participants row)

---

## Status: ✅ Database Layer Complete

**What's Done:**
- ✅ SQL migration script created
- ✅ C# database models created
- ✅ Response DTOs created
- ✅ Request DTOs created
- ✅ Documentation complete

**Ready to Execute:**
Run the SQL migration script against your ai_lab_db database, then proceed to Phase 1 (Service Layer).
