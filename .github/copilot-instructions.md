# GitHub Copilot Instructions

## Project
- Build an **ASP.NET Core (net10.0, C#) web app** demoing the **Azure AI Foundry Bot SDK** (preview) with a simple chat UI.
- Keep code minimal, production-friendly (DI, Options pattern, async, logging, retries).

## Stack & Packages
- `Azure.AI.Projects` (Agents/Bots), `Azure.Identity`
- `Microsoft.Azure.Cosmos` (chat history, optional)
- `Azure.Monitor.OpenTelemetry.AspNetCore`
- `Polly.Extensions.Http`
- Frontend: Razor Pages; use SignalR or SSE for streaming.

## Layout

## Testing
- You are going to use the  UI for web app (web browser) built into the VS Code editor and send some samlpe prompts to the app in the chat box when we test