# GitHub Copilot Instructions

## Project Overview

Build an **ASP.NET Core (net10.0, C#) web app** demonstrating the **Microsoft Agent Framework** with **Azure AI Foundry** for AI agents. This project supports two hosting modes:

1. **Self-Hosted Agent** – Agent runs locally in your ASP.NET Core app using the Microsoft Agent Framework hosting libraries
2. **Azure AI Foundry Hosted Agent** – Agent runs as a persistent, service-managed agent in Azure AI Foundry

Keep code minimal, production-friendly (DI, Options pattern, async, logging, retries).

---

## Architecture Options

### Option 1: Self-Hosted Agent (Local Orchestration)

Use the **Microsoft Agent Framework** (`Microsoft.Agents.AI.*`) to define and run agents locally in your ASP.NET Core app. Agents call Azure OpenAI or Azure AI Foundry models for inference.

**Key Characteristics:**

- Agent logic runs in your application process
- You control thread/session state (in-memory or with Cosmos DB)
- Supports tools, workflows, and multi-agent orchestration
- Ideal for custom integrations and full control

### Option 2: Azure AI Foundry Hosted Agent (Persistent Agents)

Use the **Azure AI Foundry Agents** service with `Azure.AI.Agents.Persistent` SDK. Agents are registered and managed by Azure AI Foundry with service-managed conversation threads.

**Key Characteristics:**

- Agent definitions stored in Azure AI Foundry
- Conversation threads managed by the service
- Built-in tools: Code Interpreter, Image Generation, Web Search
- Supports containerized hosted agents for custom code

---

## Stack & Packages

### Core Packages (Both Options)

```xml
<PackageReference Include="Azure.Identity" Version="1.*" />
<PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.*" />
<PackageReference Include="Microsoft.Azure.Cosmos" Version="3.*" /> <!-- Optional: chat history -->
<PackageReference Include="Polly.Extensions.Http" Version="3.*" />
```

### Self-Hosted Agent Packages

```xml
<PackageReference Include="Microsoft.Agents.AI" Version="*-*" />
<PackageReference Include="Microsoft.Agents.AI.Hosting" Version="*-*" />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="*-*" />
<PackageReference Include="Azure.AI.OpenAI" Version="2.*" />
```

### Azure AI Foundry Hosted Agent Packages

```xml
<PackageReference Include="Microsoft.Agents.AI.AzureAI.Persistent" Version="*-*" />
<PackageReference Include="Azure.AI.Agents.Persistent" Version="*-*" />
```

### Hosting Adapter Packages (for Hosted Agents)

```xml
<PackageReference Include="Azure.AI.AgentServer.Core" Version="*-*" />
<PackageReference Include="Azure.AI.AgentServer.AgentFramework" Version="*-*" />
```

---

## Code Patterns

### Self-Hosted Agent Setup (Program.cs)

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME is not set.");

// Register the chat client
IChatClient chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
builder.Services.AddSingleton(chatClient);

// Register an AI agent with DI
var chatAgent = builder.AddAIAgent(
    name: "assistant",
    instructions: "You are a helpful assistant.",
    chatClientServiceKey: null); // Uses default registered IChatClient

// Optional: Add tools and thread store
chatAgent.WithInMemoryThreadStore();

var app = builder.Build();
// Map endpoints...
app.Run();
```

### Azure AI Foundry Persistent Agent Setup

```csharp
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Agents.AI;

var projectEndpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT");
var modelDeploymentName = Environment.GetEnvironmentVariable("AZURE_AI_MODEL");

var persistentAgentsClient = new PersistentAgentsClient(
    projectEndpoint,
    new DefaultAzureCredential());

// Create or retrieve an AI Agent from Azure AI Foundry
AIAgent agent = await persistentAgentsClient.CreateAIAgentAsync(
    model: modelDeploymentName,
    name: "MyAssistant",
    instructions: "You are a helpful assistant.");

// Reuse existing agent by ID
AIAgent existingAgent = await persistentAgentsClient.GetAIAgentAsync("<agent-id>");
```

---

## Configuration

### appsettings.json

```json
{
  "AZURE_OPENAI_ENDPOINT": "https://<resource>.openai.azure.com/",
  "AZURE_OPENAI_DEPLOYMENT_NAME": "gpt-4o-mini",
  "AZURE_AI_ENDPOINT": "https://<resource>.services.ai.azure.com/api/projects/<project>",
  "AZURE_AI_MODEL": "gpt-4o-mini",
  "CosmosDb": {
    "Endpoint": "https://<account>.documents.azure.com:443/",
    "DatabaseName": "ChatHistory",
    "ContainerName": "Conversations"
  }
}
```

### Environment Variables (for local development)

```
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o-mini
AZURE_AI_ENDPOINT=https://<resource>.services.ai.azure.com/api/projects/<project>
```

---

## Frontend

- Use **Razor Pages** for the chat UI
- Use **SignalR** or **Server-Sent Events (SSE)** for streaming responses
- Keep UI minimal and responsive

---

## Observability

Enable OpenTelemetry for tracing and logging:

```csharp
using Azure.Monitor.OpenTelemetry.AspNetCore;

builder.Services.AddOpenTelemetry().UseAzureMonitor();

// For agent-level telemetry
agent.AsBuilder()
    .UseOpenTelemetry(sourceName: "agent-telemetry")
    .Build();
```

---

## Testing

- Use the **VS Code Simple Browser** to test the web app UI
- Send sample prompts to the chat box
- Verify streaming responses work correctly

### Assumptions

- .NET 10.0 SDK is installed
- Node.js (correct version) is installed
- Azure CLI authenticated (`az login`)
- Azure OpenAI or Azure AI Foundry resources provisioned

---

## References

- [Microsoft Agent Framework Overview](https://learn.microsoft.com/agent-framework/overview/agent-framework-overview)
- [Hosting AI Agents in ASP.NET Core](https://learn.microsoft.com/agent-framework/user-guide/hosting/)
- [Azure AI Foundry Agents](https://learn.microsoft.com/azure/ai-foundry/agents/overview)
- [Azure AI Foundry Hosted Agents](https://learn.microsoft.com/azure/ai-foundry/agents/concepts/hosted-agents)
- [Agent Types Reference](https://learn.microsoft.com/agent-framework/user-guide/agents/agent-types/)
