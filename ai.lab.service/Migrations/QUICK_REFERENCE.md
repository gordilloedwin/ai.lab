# Quick Reference: Chat System Schema

## Key Decisions Summary

| Aspect | Decision | Rationale |
|--------|----------|-----------|
| **Max Participants** | 30 (hard limit) | Configurable per room in DB |
| **AI Representation** | Messages only (not participant) | Cleaner, AI appears in message stream |
| **Leave Mechanism** | Explicit button required | Prevents accidental room exit on disconnect |
| **Read Receipts** | Yes, via `last_read_message_id` | Enables "X unread messages" feature |
| **Room Privacy** | None (public rooms) | Anyone can join if space available |
| **Message Persistence** | All messages (user + AI) | Full history in `chat_messages` table |
| **Presence Tracking** | Real-time via `is_currently_connected` | Updated on SignalR connect/disconnect |

---

## Table Relationships

```
users (existing)
  └─> chat_rooms (1:many) via created_by_email
       ├─> chat_participants (1:many)
       │    └─> users (many:1) via user_email
       ├─> chat_messages (1:many)
       │    └─> users (many:1) via sender_email [NULLABLE for AI]
       └─> chat_read_receipts (1:many)
            ├─> users (many:1) via user_email
            └─> chat_messages (many:1) via last_read_message_id
```

---

## Critical Columns

### `chat_participants`
- `is_currently_connected` → Real-time presence (SignalR)
- `left_at` → NULL = still in room, NOT NULL = explicitly left
- `connection_id` → SignalR connection ID for push notifications

### `chat_messages`
- `sender_type` → ENUM('user', 'ai')
- `sender_email` → NULL for AI messages, user email for user messages

### `chat_rooms`
- `max_participants` → Default 30, enforced by application logic
- `is_active` → Soft delete flag

---

## To Run Migration

```bash
# From PowerShell in project directory
cd x:\Github\gordilloedwin\ai.lab\ai.lab.service

# Run migration
mysql -h localhost -u root -p ai_lab_db < Migrations/001_CreateChatTables.sql
```

**Or via MySQL Workbench:**
1. Open `Migrations/001_CreateChatTables.sql`
2. Select `ai_lab_db` schema
3. Execute script

---

## Key Queries (Already in Views)

### Get Active Participants in Room
```sql
SELECT * FROM vw_active_chat_participants WHERE chat_room_id = ?;
```

### Get Room Statistics
```sql
SELECT * FROM vw_chat_room_stats WHERE room_id = ?;
```

### Get Unread Count for User
```sql
SELECT unread_count FROM vw_unread_message_counts 
WHERE room_id = ? AND user_email = ?;
```

---

## Next: Service Layer

**Create:** `Services/Common/IChatService.cs`

**Must Have Methods:**
1. `CreateChatRoomAsync()` - Create new room
2. `JoinChatRoomAsync()` - Add participant (check 30-user limit)
3. `LeaveChatRoomAsync()` - Set `left_at` timestamp
4. `AddUserMessageAsync()` - Insert user message
5. `AddAiMessageAsync()` - Insert AI message (sender_email=NULL)
6. `GetActiveParticipantCountAsync()` - For limit enforcement
7. `UpdateReadReceiptAsync()` - Mark messages as read

**Then:** Extend `AiLabHub.cs` with chat methods

---

## Files Created

- ✅ `Migrations/001_CreateChatTables.sql`
- ✅ `Model/Database/ChatRoom.cs`
- ✅ `Model/Database/ChatParticipant.cs`
- ✅ `Model/Database/ChatMessage.cs`
- ✅ `Model/Database/ChatReadReceipt.cs`
- ✅ `Model/Outbound/ChatRoomResponse.cs`
- ✅ `Model/Outbound/ChatMessageResponse.cs`
- ✅ `Model/Outbound/ChatParticipantResponse.cs`
- ✅ `Model/Inbound/CreateChatRoomRequest.cs`
- ✅ `Model/Inbound/SendChatMessageRequest.cs`
- ✅ `Migrations/IMPLEMENTATION_GUIDE.md` (detailed docs)

**Status:** Database layer complete, ready to run migration.
