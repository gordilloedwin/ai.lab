# Chat Service Testing Guide

## Prerequisites
1. Database migration applied (`001_CreateChatTables.sql`)
2. Application running (`dotnet run`)
3. Valid JWT token (from `/api/Auth/signin`)

## Test Flow

### 1. Sign In
```bash
POST http://localhost:5000/api/Auth/signin
Content-Type: application/json

{
  "email": "your-email@example.com",
  "password": "your-password"
}
```

Copy the `token` from the response.

### 2. Create a Chat Room
```bash
POST http://localhost:5000/api/Chat/rooms
Authorization: Bearer YOUR_TOKEN_HERE
Content-Type: application/json

{
  "title": "Test Chat Room",
  "aiModel": "deepseek-coder:6.7b",
  "maxParticipants": 30
}
```

**Expected Response:**
```json
{
  "id": 1,
  "title": "Test Chat Room",
  "createdByEmail": "your-email@example.com",
  "createdByName": "Your Name",
  "aiModel": "deepseek-coder:6.7b",
  "maxParticipants": 30,
  "currentParticipantCount": 0,
  "totalMessageCount": 0,
  "unreadMessageCount": 0,
  "createdAt": "2025-10-16T...",
  "lastMessageAt": null,
  "isActive": true,
  "isFull": false,
  "isUserInRoom": false
}
```

### 3. Get All Chat Rooms
```bash
GET http://localhost:5000/api/Chat/rooms
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected:** Array of all active chat rooms

### 4. Join the Chat Room
```bash
POST http://localhost:5000/api/Chat/rooms/1/join
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected Response:**
```json
{
  "message": "Successfully joined chat room"
}
```

### 5. Get My Chat Rooms
```bash
GET http://localhost:5000/api/Chat/rooms/mine
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected:** Array with the room you just joined, `isUserInRoom: true`

### 6. Get Participants
```bash
GET http://localhost:5000/api/Chat/rooms/1/participants?activeOnly=true
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected Response:**
```json
[
  {
    "userEmail": "your-email@example.com",
    "userName": "Your Name",
    "avatarUri": null,
    "joinedAt": "2025-10-16T...",
    "leftAt": null,
    "isCurrentlyConnected": true,
    "lastSeenAt": "2025-10-16T...",
    "timeInRoomSeconds": 45,
    "isCurrentUser": true
  }
]
```

### 7. Send a Message
```bash
POST http://localhost:5000/api/Chat/rooms/1/messages
Authorization: Bearer YOUR_TOKEN_HERE
Content-Type: application/json

{
  "content": "Hello from the chat service test!"
}
```

**Expected Response:**
```json
{
  "id": 1,
  "chatRoomId": 1,
  "senderEmail": "your-email@example.com",
  "senderName": "Your Name",
  "senderAvatarUri": null,
  "senderType": "user",
  "content": "Hello from the chat service test!",
  "createdAt": "2025-10-16T...",
  "isOwnMessage": true
}
```

### 8. Get Messages
```bash
GET http://localhost:5000/api/Chat/rooms/1/messages?limit=50
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected:** Array with your message

### 9. Get Unread Count
```bash
GET http://localhost:5000/api/Chat/rooms/1/unread
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected Response:**
```json
{
  "unreadCount": 1
}
```

### 10. Update Read Receipt
```bash
POST http://localhost:5000/api/Chat/rooms/1/read
Authorization: Bearer YOUR_TOKEN_HERE
Content-Type: application/json

{
  "lastReadMessageId": 1
}
```

**Expected Response:**
```json
{
  "message": "Read receipt updated"
}
```

### 11. Verify Unread Count is Now Zero
```bash
GET http://localhost:5000/api/Chat/rooms/1/unread
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected Response:**
```json
{
  "unreadCount": 0
}
```

### 12. Leave the Chat Room
```bash
POST http://localhost:5000/api/Chat/rooms/1/leave
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected Response:**
```json
{
  "message": "Successfully left chat room"
}
```

### 13. Verify Room Status After Leaving
```bash
GET http://localhost:5000/api/Chat/rooms/1
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected:** `isUserInRoom: false`, `currentParticipantCount: 0`

### 14. Delete Chat Room (Creator Only)
```bash
DELETE http://localhost:5000/api/Chat/rooms/1
Authorization: Bearer YOUR_TOKEN_HERE
```

**Expected Response:**
```json
{
  "message": "Chat room deleted successfully"
}
```

## Testing AI Messages (Direct Database Insert for Now)

Until we integrate SignalR hub, you can test AI messages directly:

```sql
-- Add an AI message
INSERT INTO chat_messages (chat_room_id, sender_email, sender_type, content)
VALUES (1, NULL, 'ai', 'This is an AI-generated response.');

-- Verify it appears in the API
-- GET http://localhost:5000/api/Chat/rooms/1/messages
```

## Swagger UI Testing

You can also test all endpoints using Swagger UI:
1. Navigate to: http://localhost:5000/swagger
2. Click "Authorize" button
3. Enter: `Bearer YOUR_TOKEN_HERE`
4. Click "Authorize"
5. Test endpoints interactively

## Next Steps

After REST API tests pass:
1. ✅ **Database Layer** - Complete
2. ✅ **Service Layer** - Complete
3. ✅ **REST API** - Complete
4. 🔄 **SignalR Hub** - Extend `AiLabHub` with chat methods
5. 🔄 **Blazor UI** - Create `Chat.razor` component
6. 🔄 **AI Integration** - Connect AI streaming to chat

## Database Verification Queries

```sql
-- Check chat rooms
SELECT * FROM chat_rooms;

-- Check participants
SELECT * FROM chat_participants;

-- Check messages
SELECT * FROM chat_messages;

-- Check read receipts
SELECT * FROM chat_read_receipts;

-- Check active participants view
SELECT * FROM vw_active_chat_participants;

-- Check room stats view
SELECT * FROM vw_chat_room_stats;

-- Check unread messages view
SELECT * FROM vw_unread_message_counts;
```

## Common Issues

### Issue: "User email not found in claims"
**Solution:** Make sure you're passing `Authorization: Bearer YOUR_TOKEN` header

### Issue: "Unable to join room. Room may be full or inactive."
**Solutions:**
- Verify room exists: `SELECT * FROM chat_rooms WHERE id = 1;`
- Check if room is active: `is_active = TRUE`
- Check participant count vs max_participants

### Issue: "Unable to leave room. You may not be in this room."
**Solutions:**
- Verify you're in the room: `SELECT * FROM chat_participants WHERE user_email = 'your-email' AND chat_room_id = 1;`
- Check if you've already left: `left_at IS NULL`

### Issue: Connection string error
**Solution:** Verify `appsettings.Development.json` has correct `MariaDbConnectionString`

## Success Criteria

✅ All 14 REST API tests pass
✅ Database tables populated correctly
✅ Views return expected data
✅ Read receipts update unread counts
✅ 30-user limit enforced
✅ Explicit leave mechanism works
✅ User and AI messages persist separately

**You're ready for SignalR integration when all tests pass!** 🚀
