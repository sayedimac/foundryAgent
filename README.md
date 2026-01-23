# Foundry Agent ASP.NET Sample

> ASP.NET Core 8 minimal web app using **Microsoft Agent Framework** with **Azure OpenAI**.

## Prerequisites
- .NET 8 SDK or later
- Azure OpenAI resource (e.g., `gpt-4o-mini` deployment)
- Azure CLI logged in with access (`az login`)
- (Optional) API key if not using Azure CLI credentials

## Packages
```bash
# if you need to re-add/update packages
dotnet add src/FoundryAgent.Web package Azure.AI.OpenAI --prerelease
dotnet add src/FoundryAgent.Web package Azure.Identity
dotnet add src/FoundryAgent.Web package Microsoft.Agents.AI.OpenAI --prerelease
```

## Configuration
Set via `appsettings.json` or environment variables:
- `AzureOpenAI:Endpoint` (e.g., `https://your-resource.openai.azure.com/`)
- `AzureOpenAI:Deployment` (e.g., `gpt-4o-mini`)
- `AzureOpenAI:UseAzureCliCredential` (default `true`)
- `AzureOpenAI:Key` (optional; if set, API key credential is used)
- `AzureOpenAI:Instructions` (agent system prompt)

Environment variable equivalents:
```
set AzureOpenAI__Endpoint=https://your-resource.openai.azure.com/
set AzureOpenAI__Deployment=gpt-4o-mini
set AzureOpenAI__Key=<api-key>  # optional
set AzureOpenAI__Instructions="You are a helpful assistant."
```

## Run
```bash
# restore & run
 dotnet restore
 dotnet run --project src/FoundryAgent.Web
```
App will serve static UI at `http://localhost:5116` (see `launchSettings.json`).

## Endpoints
- `POST /api/chat` `{ "input": "Tell me a joke" }` → `{ "output": "..." }`
- Static UI: `/` (simple chat page calling `/api/chat`)

## Notes
- Microsoft Agent Framework **public preview**; package versions may change. Use `--prerelease`.
- Default credential is Azure CLI. To use API key, set `AzureOpenAI:Key` and `UseAzureCliCredential=false`.

## Links
- [Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)
- [Agent Framework Quick Start (.NET)](https://learn.microsoft.com/en-us/agent-framework/tutorials/quick-start?pivots=programming-language-csharp)
