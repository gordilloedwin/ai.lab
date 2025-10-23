# AI.Lab - Intelligent Multi-User Chat with RAG

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![MariaDB](https://img.shields.io/badge/MariaDB-11.8-003545?logo=mariadb)](https://mariadb.org/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A production-ready, AI-powered multi-user chat application with **Retrieval-Augmented Generation (RAG)**, real-time collaboration, and intelligent code understanding. Built with Blazor Server, SignalR, and MariaDB VECTOR support.

---

## 🌟 Features

### 🤖 AI & RAG System
- **Intelligent Code Understanding**: Automatically indexes and understands your codebase
- **Multi-Language Support**: C#, Python, JavaScript, Java, Ruby, SQL, PowerShell, and more
- **Semantic Search**: Vector embeddings with MariaDB VECTOR type (dimension 4096)
- **Context-Aware RAG**: Smart tag extraction from user prompts for filtered searches
- **Streaming Responses**: Real-time AI responses with proper markdown rendering
- **Dual Storage**: MariaDB for production, Qdrant for high-performance vector search

### 💬 Real-Time Chat
- **Multi-User Rooms**: Create and join chat rooms with customizable limits (default: 30 users)
- **SignalR Integration**: Real-time message delivery with presence tracking
- **Read Receipts**: Track unread messages per user per room
- **Message Management**: Edit, delete, and soft-delete messages
- **User Presence**: See who's currently connected and their connection status
- **AI Participants**: Optional AI assistant in chat rooms

### 🔐 Security & Authentication
- **JWT Authentication**: Secure token-based authentication with localStorage
- **Admin Controls**: Role-based access for room management
- **Pre-rendering Support**: Proper handling of Blazor Server SSR/CSR transitions
- **Connection Management**: Automatic reconnection and session handling

### 📊 Smart File Processing
- **Intelligent Filtering**: Automatically excludes build artifacts (`bin`, `obj`, `node_modules`)
- **Hidden Folder Exclusion**: Skips `.git`, `.vs`, `.vscode`, `.idea`
- **Test File Filtering**: Excludes test files and test directories
- **Chunk Generation**: Semantic code chunking with context preservation
- **Incremental Updates**: Only processes changed files
- **Tag-Based Indexing**: Automatic semantic tagging from `semantic-tags.txt`

### 🎨 Modern UI/UX
- **Responsive Design**: Mobile-friendly interface
- **Loading States**: Proper `<Authorizing>` and loading indicators
- **Code Highlighting**: Syntax highlighting with markdown support
- **Avatar Support**: User avatars with fallback UI
- **Unread Indicators**: Badge counters for unread messages
- **Connection Status**: Visual indicators for user presence

### 📊 Observability & Monitoring
- **OpenTelemetry Integration**: Comprehensive metrics, traces, and logs
- **Distributed Tracing**: Track requests across services
- **Custom Metrics**: Monitor RAG performance, embedding generation, and chat activity
- **Structured Logging**: Correlated logs with trace IDs
- **Export Support**: Compatible with Prometheus, Jaeger, and other OTLP backends

---

## 🏗️ Architecture

```
ai.lab/
├── ai.lab.service/          # Main Blazor Server application
│   ├── Controllers/         # API controllers
│   ├── Hub/                 # SignalR hubs for real-time chat
│   ├── Managers/            # Business logic layer
│   ├── Services/            # Data access and AI services
│   ├── Pages/               # Blazor pages
│   ├── Helpers/             # Utility classes (VectorHandler, TagMatcher)
│   ├── Model/               # Data models and DTOs
│   ├── Migrations/          # Database migration scripts
│   └── Options/             # Configuration classes
│
├── ai.lab.ragfeed/          # RAG ingestion pipeline
│   ├── ChunkExtractor.cs    # Main chunk extraction orchestrator
│   ├── ChunkGenerators/     # Language-specific extractors
│   │   ├── RoslynChunkExtractor.cs      # C#/VB.NET
│   │   ├── PythonChunkExtractor.cs      # Python
│   │   ├── JavascriptChunkExtractor.cs  # JS/TS
│   │   ├── JavaChunkExtractor.cs        # Java
│   │   ├── RubyChunkExtractor.cs        # Ruby
│   │   ├── PostgresChunkExtractor.cs    # SQL
│   │   ├── PowerShellChunkExtractor.cs  # PowerShell
│   │   ├── CppChunkExtractor.cs         # C/C++
│   │   ├── CssChunkExtractor.cs         # CSS/SCSS
│   │   └── TextChunkExtractor.cs        # Markdown/Text
│   └── Output/              # Output models
│
└── semantic-tags.txt        # Semantic tags for code classification
```

---

## 🚀 Getting Started

### Prerequisites

- **.NET 9.0 SDK** (preview): [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **MariaDB 11.8+** with VECTOR support: [Download](https://mariadb.org/download/)
- **Ollama** (for local AI): [Download](https://ollama.ai/) or use external API
- **Visual Studio 2022** or **VS Code** with C# Dev Kit
- **Linux**: Any distribution with .NET Core support (Ubuntu, Debian, Fedora, CentOS, etc.) or Windows 10/11

> **Note**: This application is cross-platform and runs seamlessly on Linux distributions of your choice, as long as .NET 9.0 runtime is installed.

### Installation

1. **Clone the repository**
	 ```bash
	 git clone https://github.com/gordilloedwin/ai.lab.git
	 cd ai.lab
	 ```

2. **Setup MariaDB**
	 ```bash
	 # Create database
	 mysql -u root -p
	 CREATE DATABASE ai_lab_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   
	 # Run migrations
	 mysql -u root -p ai_lab_db < ai.lab.service/Migrations/001_CreateChatTables.sql
	 ```

3. **Configure appsettings.json**
	 ```json
	 {
		 "ConnectionStrings": {
			 "MariaDb": "Server=localhost;Database=ai_lab_db;User=root;Password=yourpassword;"
		 },
		 "AILabOptions": {
			 "OllamaBaseUrl": "http://localhost:11434",
			 "EmbeddingsModel": "nomic-embed-text",
			 "ChatModel": "llama3.1:latest",
			 "RepositoriesPath": "C:\\Projects\\",
			 "IsRagIngestionEnabled": true,
			 "SaveChunksToMariaDb": true,
			 "WorkerDelaySeconds": 300
		 },
		 "JwtSettings": {
			 "SecretKey": "your-super-secret-key-change-this-in-production",
			 "Issuer": "ai.lab",
			 "Audience": "ai.lab.users"
		 }
	 }
	 ```

4. **Install Ollama Models**
	 ```bash
	 ollama pull nomic-embed-text
	 ollama pull llama3.1:latest
	 ```

5. **Build and Run**
	 ```bash
	 cd ai.lab.service
	 dotnet restore
	 dotnet build
	 dotnet run
	 ```

6. **Access the application**
	 - Navigate to `https://localhost:5001` or `http://localhost:5000`
	 - Default admin: `admin@ai.lab` / `admin123` (change in production!)

---

## 📖 Configuration

### AILabOptions

| Option | Description | Default |
|--------|-------------|---------|
| `OllamaBaseUrl` | Ollama API endpoint | `http://localhost:11434` |
| `EmbeddingsModel` | Model for generating embeddings | `nomic-embed-text` |
| `ChatModel` | Model for chat responses | `llama3.1:latest` |
| `RepositoriesPath` | Root path to scan for repositories | Required |
| `IsRagIngestionEnabled` | Enable/disable background RAG worker | `true` |
| `SaveChunksToMariaDb` | Store embeddings in MariaDB | `true` |
| `SaveChunksToQdrant` | Store embeddings in Qdrant (optional) | `false` |
| `QdrantBaseUrl` | Qdrant server URL | `http://localhost:6333` |
| `WorkerDelaySeconds` | Delay between repository scans | `300` (5 min) |

### Database Configuration

**MariaDB Connection String Format:**
```
Server=localhost;Port=3306;Database=ai_lab_db;User=root;Password=yourpassword;
```

**VECTOR Column:**
```sql
embedding vector(4096) DEFAULT NULL
```

### HttpClient & Resilience

The application uses **IHttpClientFactory** with Polly policies:
- **Retry Policy**: 5 attempts with exponential backoff (2s, 4s, 8s, 16s, 32s)
- **Circuit Breaker**: Opens after 3 consecutive failures, 30s break duration
- **Timeout**: 15 minutes for long-running operations

---

## 🧪 Usage

### Creating a Chat Room

1. Login to the application
2. Navigate to **Dashboard**
3. Click **Create New Room**
4. Enter room title
5. (Optional) Select AI model
6. Set max participants (default: 30)

### Using RAG

**Ask questions about your codebase:**
```
User: "How does authentication work in this project?"
AI: [Retrieves relevant code chunks from DatabaseService.cs, JwtAuthStateProvider.cs]
		"Authentication uses JWT tokens stored in localStorage..."
```

**Filter by technology:**
```
User: "Show me Python error handling examples"
AI: [Filters chunks tagged with 'python', 'exception', 'error']
		"Here are the error handling patterns from your Python code..."
```

### Semantic Tags

Edit `semantic-tags.txt` to customize tag extraction:
```
# Programming Languages
csharp
python
javascript
typescript

# Frameworks
blazor
react
django

# Concepts
authentication
authorization
database
vector
```

Tags are automatically extracted from:
- File paths
- Code content
- User prompts (for RAG search filtering)

---

## 🔧 Development

### Project Structure

#### ai.lab.service (Main Application)

**Key Files:**
- `Program.cs` - Application startup, DI configuration
- `AiLabWorker.cs` - Background service for RAG ingestion
- `DatabaseService.cs` - Data access layer with Dapper
- `AIService.cs` - AI chat service with streaming support
- `EmbeddingManager.cs` - Embedding generation and storage
- `VectorHandler.cs` - Custom Dapper TypeHandler for VECTOR type
- `TagMatcher.cs` - Semantic tag extraction using Aho-Corasick

**Important Services:**
```csharp
// Scoped services (per request)
builder.Services.AddScoped<IChunkExtractor, ChunkExtractor>();
builder.Services.AddScoped<IEmbeddingManager, EmbeddingManager>();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddScoped<IAIService, AIService>();

// Singleton services
builder.Services.AddSingleton<IOllamaClient, OllamaClientManager>();
builder.Services.AddSingleton<IQdrantClient, QdrantClientManager>();
```

### Database Schema

**Key Tables:**
- `users` - User accounts with JWT authentication
- `chat_rooms` - Chat room definitions
- `chat_participants` - Many-to-many with presence tracking
- `chat_messages` - Message history (user and AI)
- `chat_read_receipts` - Unread message tracking
- `chat_chunk_embeddings` - Code chunks with VECTOR embeddings

**Sample Vector Query:**
```sql
SELECT 
		chunk_text,
		(1 - (DOT_PRODUCT(embedding, @Embedding) / 
					(VECTOR_NORM(embedding) * VECTOR_NORM(@Embedding)))) AS distance
FROM chat_chunk_embeddings
WHERE model = @Model
	AND JSON_OVERLAPS(tags, @FilterTags)  -- Optional tag filtering
ORDER BY distance ASC
LIMIT @TopK;
```

### Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReporter=html
```

---

## 🐛 Troubleshooting

### Common Issues

**1. Vector Insert Errors**
```
MySqlException: Incorrect vector value
```
**Solution**: Ensure MariaDB 11.8+ with VECTOR support. The `VectorHandler` uses binary format.

**2. Authentication Fails**
```
Access Denied on dashboard
```
**Solution**: Check `JwtAuthStateProvider` is properly handling pre-rendering. Clear localStorage and re-login.

**3. Duplicate Participants**
```
User appears multiple times in participant list
```
**Solution**: Fixed in current version. The system now updates `connection_id` on reconnect instead of creating duplicates.

**4. HttpClient Not Using Policies**
```
Timeout after 100 seconds instead of 15 minutes
```
**Solution**: Ensure `AILabBaseClient` derived classes implement the `HttpClientName` property correctly.

**5. Markdown Code Blocks Broken**
```
Code blocks render on single line during streaming
```
**Solution**: The `AIService` now buffers content until ``` pairs are closed.

### 6. Blazor Pages Not Loading Under systemd
```
Blazor layout/pages return 404 or blank screen when running as a systemd service
```
**Cause**: Required persistent key directory for data-protection / auth cookies not writable by service user.
**Fix**: Create and chown a dedicated keys directory BEFORE starting the service.

Run these once (replace ailabuser:ailabgroup with your service user/group):
```bash
sudo mkdir -p /var/lib/ailab-keys
sudo chown -R ailabuser:ailabgroup /var/lib/ailab-keys
```
Then ensure your service user has read/write permissions and (optionally) configure ASP.NET DataProtection to use this path if you add custom configuration.

## 🛠️ Deployment (systemd)

To run the service via systemd (after publishing with `dotnet publish -c Release`):
```bash
sudo cp ai.lab.service/ailab.service /etc/systemd/system/ailab.service
sudo systemctl daemon-reload
sudo systemctl enable ailab.service
sudo systemctl start ailab.service
sudo systemctl status ailab.service
```

Make sure you performed the key directory setup above; otherwise Blazor pages may fail to render when hosted headless.

### DataProtection Configuration (systemd)
Add this to `Program.cs` (already present) to ensure cookies & antiforgery work when running as a service:
```csharp
builder.Services.AddDataProtection()
	.PersistKeysToFileSystem(new DirectoryInfo("/var/lib/ailab-keys"))
	.SetApplicationName("ai.lab.service");
```
Be sure the directory exists and is writable by the service user:
```bash
sudo mkdir -p /var/lib/ailab-keys
sudo chown -R ailabuser:ailabgroup /var/lib/ailab-keys
```

### Debug Mode

Enable detailed logging in `appsettings.Development.json`:
```json
{
	"Logging": {
		"LogLevel": {
			"Default": "Debug",
			"ai.lab.service": "Trace",
			"Microsoft.AspNetCore.SignalR": "Debug"
		}
	}
}
```

---

## 🚦 Performance

### Benchmarks (MariaDB VECTOR)

| Operation | Time | Notes |
|-----------|------|-------|
| Insert embedding (4096d) | ~5ms | Binary format |
| Vector similarity search (top 10) | ~50ms | With tag filtering |
| Vector similarity search (top 10) | ~30ms | No filtering |
| Chunk extraction (C# file) | ~100ms | Average file |

### Optimization Tips

1. **Index tags column**: `CREATE INDEX idx_tags ON chat_chunk_embeddings(tags);`
2. **Increase vector cache**: `SET GLOBAL vector_cache_size = 1073741824;` (1GB)
3. **Use connection pooling**: Already configured in connection string
4. **Enable query cache**: `SET GLOBAL query_cache_size = 268435456;` (256MB)

---

## 📝 API Reference

### REST Endpoints

**Authentication:**
- `POST /api/auth/login` - User login (returns JWT)
- `POST /api/auth/register` - User registration

**Chat Rooms:**
- `GET /api/chat/rooms` - Get all active rooms
- `POST /api/chat/rooms` - Create new room
- `DELETE /api/chat/rooms/{id}` - Delete room (creator/admin only)

**AI:**
- `POST /api/ai/chat` - Send message with RAG context
- `GET /api/ai/models` - List available AI models

### SignalR Hub Methods

**Client → Server:**
- `SendMessage(roomId, content)` - Send chat message
- `JoinRoom(roomId)` - Join chat room
- `LeaveRoom(roomId)` - Leave chat room

**Server → Client:**
- `ReceiveMessage(message)` - New message received
- `UserJoined(user)` - User joined notification
- `UserLeft(user)` - User left notification
- `StreamingMessage(content)` - Partial AI response

---

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add amazing feature'`
4. Push to the branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

### Code Style

- Follow C# coding conventions
- Use meaningful variable names
- Add XML documentation to public APIs
- Write unit tests for new features
- Keep methods focused and small

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **Ollama Team** - Local AI inference
- **MariaDB Foundation** - VECTOR type support
- **Microsoft** - .NET 9.0, Blazor, SignalR
- **Dapper** - Micro ORM
- **Polly** - Resilience and fault-handling

---

## 📧 Contact

**Edwin Gordillo**
- GitHub: [@gordilloedwin](https://github.com/gordilloedwin)
- Email: gordilloedwin@hotmail.com

---

## 🗺️ Roadmap

- [ ] **Voice Chat** - WebRTC integration for voice/video
- [ ] **File Attachments** - Upload and share files in chat
- [ ] **Code Execution** - Run code snippets securely
- [ ] **Advanced RAG** - Multi-modal embeddings (code + docs + images)
- [ ] **Analytics Dashboard** - Usage statistics and insights
- [ ] **Mobile App** - React Native or MAUI companion app
- [ ] **Plugins System** - Extensible plugin architecture
- [ ] **CI/CD Pipeline** - GitHub Actions for automated deployment
- [ ] **Docker Support** - Containerized deployment
- [ ] **Kubernetes Helm Charts** - Scalable cloud deployment

---

## ⭐ Star History

If you find this project useful, please consider giving it a star! ⭐

---

<div align="center">
	<sub>Built with ❤️ using Blazor, SignalR, and MariaDB</sub>
</div>
