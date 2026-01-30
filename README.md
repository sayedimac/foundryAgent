# Azure AI Foundry Agent SDK Demo

> A comprehensive ASP.NET Core demo application showcasing **Azure AI Foundry Agent SDK** features using the **Microsoft Agent Framework**.

## 🌟 Features Demonstrated

This demo showcases two types of Azure AI Foundry agents with seamless toggling in the UI:

### 🐙 GitHub Agent (Self-Hosted)
A self-hosted agent running locally in the ASP.NET Core application using the Microsoft Agent Framework.

| Feature | Description | Status |
|---------|-------------|--------|
| **AIProjectClient** | Modern, recommended approach for creating agents | ✅ Enabled |
| **Function Tools** | Custom C# functions (GetWeather, Calculate, SearchProducts, GetCurrentTime) | ✅ Enabled |
| **OpenTelemetry Tracing** | Observability with distributed tracing | ✅ Enabled |
| **Streaming Responses** | Real-time SSE streaming | ✅ Enabled |
| **Multi-turn Conversations** | Thread-based conversation history | ✅ Enabled |

### ✈️ Travel Agent (Foundry-Hosted)
A pre-deployed agent configured in the Azure AI Foundry portal (e.g., "Margies Travel Agent") accessed via the Responses API.

| Feature | Description | Status |
|---------|-------------|--------|
| **Foundry Hosted Agent** | Agent deployed and managed in Azure AI Foundry portal | 🔧 Available |
| **Bing Grounding** | Web search integration configured in portal | 🔧 Available |
| **Code Interpreter** | Execute Python code configured in portal | 🔧 Available |
| **Azure AI Search** | Enterprise knowledge base configured in portal | 🔧 Available |
| **Multi-turn Conversations** | Conversation continuity using previous_response_id | ✅ Enabled |

## 📋 Prerequisites

- **.NET 10 SDK** or later
- **Azure AI Foundry Project** with a deployed model (e.g., `gpt-4o`, `gpt-4o-mini`)
- **Azure CLI** authenticated (`az login`)
- (Optional) **Bing Connection** for web search grounding
- (Optional) **Azure AI Search** connection for enterprise search

## 📦 NuGet Packages

```bash
# Core packages (already included)
dotnet add package Azure.AI.Projects --prerelease
dotnet add package Microsoft.Agents.AI.AzureAI --prerelease
dotnet add package Azure.Identity

# OpenTelemetry for observability
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
```

> **Note:** The `--prerelease` flag is required while the Agent Framework is in preview.

## ⚙️ Configuration

The application supports two agent configurations that can be toggled in the UI:

### 1. GitHub Agent (Self-Hosted) - `LocalHostedAgent`

This agent runs locally in your application with custom C# function tools.

### 2. Travel Agent (Foundry-Hosted) - `FoundryHostedAgent`

This agent is pre-deployed in the Azure AI Foundry portal (e.g., "Margies Travel Agent").

### appsettings.json

```json
{
  "LocalHostedAgent": {
    "ProjectEndpoint": "https://your-resource.services.ai.azure.com/api/projects/your-project",
    "DeploymentName": "gpt-4o",
    "UseDefaultAzureCredential": true,
    "Instructions": "You are a helpful AI assistant.",
    "BingConnectionId": "",
    "AzureAISearchConnectionId": "",
    "AzureAISearchIndexName": "",
    "EnableTelemetry": true,
    "OtlpEndpoint": "",
    "ApplicationInsightsConnectionString": ""
  },
  "FoundryHostedAgent": {
    "ApplicationName": "Margies Travel Agent",
    "DisplayName": "Travel Agent",
    "ResponsesApiEndpoint": "https://your-resource.services.ai.azure.com/api/projects/your-project/applications/your-app/protocols/openai/responses",
    "ApiVersion": "2025-11-15-preview"
  }
}
```

### Environment Variables

```powershell
# For GitHub Agent (Self-Hosted)
$env:LocalHostedAgent__ProjectEndpoint = "https://your-resource.services.ai.azure.com/api/projects/your-project"
$env:LocalHostedAgent__DeploymentName = "gpt-4o"

# Optional - for Bing grounding in GitHub Agent
$env:LocalHostedAgent__BingConnectionId = "your-bing-connection-id"

# Optional - for Azure AI Search in GitHub Agent
$env:LocalHostedAgent__AzureAISearchConnectionId = "your-search-connection-id"
$env:LocalHostedAgent__AzureAISearchIndexName = "your-index-name"

# For Travel Agent (Foundry-Hosted)
$env:FoundryHostedAgent__ApplicationName = "Margies Travel Agent"
$env:FoundryHostedAgent__ResponsesApiEndpoint = "https://your-resource.services.ai.azure.com/api/projects/your-project/applications/your-app/protocols/openai/responses"

# Optional - for OTLP tracing
$env:LocalHostedAgent__OtlpEndpoint = "http://localhost:4317"
```

## 🚀 Running the Application

```bash
# Restore dependencies
dotnet restore

# Run the application
dotnet run --project src/FoundryAgent.Web

# The app will be available at:
# - HTTP:  http://localhost:5116
# - HTTPS: https://localhost:7116
```

### Using the Web UI

1. Open your browser to `http://localhost:5116`
2. Use the toggle buttons at the top to switch between agents:
   - **🐙 GitHub Agent** - Self-hosted with custom C# function tools
   - **✈️ Travel Agent** - Foundry-hosted Margies Travel Agent with Bing grounding and Azure AI Search
3. The agent indicator will update to show which agent is active
4. Start chatting! Each agent maintains its own conversation history

![Agent Toggle UI](https://github.com/user-attachments/assets/527f2275-989e-4380-889d-f7efa3dfb984)

*The UI showing both agent options with the GitHub Agent selected*

![Travel Agent Active](https://github.com/user-attachments/assets/4141da62-7d0e-4726-bf92-e8da1a17eb13)

*Switched to Travel Agent - system message confirms the switch*


## 🔌 API Endpoints

### Chat Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/chat` | Send a message and receive a response |
| `POST` | `/api/chat/stream` | Stream a response using SSE |
| `POST` | `/api/chat/upload` | Send files for analysis |
| `GET` | `/api/chat/capabilities` | Get agent capabilities |

### Agent Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/agents` | Get agent information |
| `GET` | `/api/agents/features` | Get SDK and feature details |
| `POST` | `/api/agents/demo?scenario=weather` | Run a demo scenario |

### Other Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/mcp/discover` | Discover MCP tools |
| `GET` | `/health` | Health check |

## 📝 Example Requests

### Basic Chat

```bash
curl -X POST http://localhost:5116/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What is the weather like in Seattle?"}'
```

### Streaming Chat

```bash
curl -X POST http://localhost:5116/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"message": "Tell me about Azure AI Foundry"}'
```

### Run Demo Scenarios

```bash
# Weather demo
curl -X POST "http://localhost:5116/api/agents/demo?scenario=weather"

# Calculator demo
curl -X POST "http://localhost:5116/api/agents/demo?scenario=calculate"

# Multi-tool demo
curl -X POST "http://localhost:5116/api/agents/demo?scenario=multi-tool"
```

## 🛠️ SDK Architecture

### Recommended: AIProjectClient

This demo uses the modern `AIProjectClient` approach, which is the **recommended** way to build agents:

```csharp
// Create the client
AIProjectClient projectClient = new(
    endpoint: new Uri(projectEndpoint),
    tokenProvider: new DefaultAzureCredential());

// Define function tools
var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather),
    AIFunctionFactory.Create(Calculate)
};

// Create and run the agent
var agent = await projectClient.CreateAIAgentAsync(
    name: "MyAgent",
    model: "gpt-4o",
    instructions: "You are a helpful assistant.",
    tools: tools);

var response = await agent.RunAsync("What's the weather in Seattle?");
```

### Legacy: PersistentAgentsClient (Not Recommended)

The legacy `PersistentAgentsClient` is still available but **not recommended** for new development:

```csharp
// ⚠️ Legacy approach - use AIProjectClient instead
var client = new PersistentAgentsClient(endpoint, credential);
```

## 📊 OpenTelemetry Tracing

The application includes comprehensive OpenTelemetry instrumentation:

```csharp
// Tracing is configured in TelemetryConfiguration.cs
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("FoundryAgent.Web")
            .AddSource("Microsoft.Agents.AI.*")
            .AddSource("Azure.AI.Projects.*")
            .AddConsoleExporter()  // or OTLP
    });
```

### Viewing Traces

- **Console**: Traces are logged to console in development
- **OTLP**: Set `Foundry:OtlpEndpoint` to export to Jaeger, Zipkin, etc.
- **Application Insights**: Set the connection string for Azure Monitor

## 🔧 Function Tools

The demo includes several custom function tools:

| Function | Description |
|----------|-------------|
| `GetWeather` | Get weather for a location |
| `Calculate` | Perform math calculations |
| `GetCurrentTime` | Get current date/time |
| `SearchProducts` | Search product catalog |

### Adding Custom Functions

```csharp
[Description("Your function description")]
private static string MyFunction(
    [Description("Parameter description")] string param)
{
    // Your implementation
    return result;
}

// Register with AIFunctionFactory
_tools.Add(AIFunctionFactory.Create(MyFunction));
```

## 📚 Documentation Links

- [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/)
- [Azure AI Foundry Documentation](https://learn.microsoft.com/azure/ai-foundry/)
- [Azure AI Agents Quickstart](https://learn.microsoft.com/azure/ai-foundry/agents/quickstart)
- [Agent Framework GitHub](https://github.com/microsoft/agent-framework)
- [Azure AI Foundry MCP Integration](https://learn.microsoft.com/azure/ai-foundry/agents/how-to/tools-classic/model-context-protocol-samples)

## 🔒 Security Notes

- Use `DefaultAzureCredential` for production (supports Managed Identity)
- Never commit API keys or connection strings to source control
- Use Azure Key Vault for secrets management
- Review all data flowing to AI services

## 📄 License

This project is for demonstration purposes. See the Azure AI Foundry terms of service for usage guidelines.