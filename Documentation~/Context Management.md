# Context Management

The Context Management package provides a framework for building LLM-driven experiences in Unity. It connects your game to LLM APIs while keeping a persistent, structured world context across your AI agents.

## Installation

Install the package from the Unity Package Manager using **Add package from git URL**. See the [README](../README.md) for step-by-step instructions.

## Core concepts

### API Settings

`APISetting` is a component that stores provider configuration (setting name, endpoint, model, request options, and the API key) and implements the request. The provider is selected by adding the matching subclass to the same GameObject as the manager:

- `DeepSeekAPISetting`
- `OpenAIAPISetting`
- `OpenRouterAPISetting`
- `OpenAICompatibleAPISetting` (requires a base URL)

`LLMRequestManager` uses the `APISetting` on its GameObject. Without one it logs a warning and sends nothing.

#### API keys

The key is not serialized by Unity. By default a setting writes it into its settings file under `Application.persistentDataPath/APISettings` as plain text, so a player can enter a key once and have the game remember it; the file is loaded when the setting wakes up. To keep the key elsewhere, subclass the provider setting and override `Credential` with an `IApiCredentialStore`; the file then omits the key and the store supplies it.

The settings file keeps the key in plain text, so treat it as belonging to the person running the game rather than as a secret hidden from them. For a game you ship, use `OpenAICompatibleAPISetting` against your own backend or proxy and keep the provider key there. Leave `logOnSend` and `logOnReceive` off in a build: both print whole request and response bodies.

### Request Managers

Components that orchestrate requests to the API:

| Component | Purpose |
| --- | --- |
| `LLMRequestManager` | Sends structured prompts to an LLM; the base for agent-style components. |
| `ChatRequestManager` | Conversation-style requests with message history and streaming (`MessageStreaming`). |

A manager assembles its system prompt from the `PromptComponent`s on the same GameObject. When none of them supplies any text it logs a warning and starts with a default system prompt, so a manager added without one still runs.

The manager is itself a `PromptComponent` and accepts a list of `TextAsset`s.

### Context and tools managers

A request manager drives two components, assigned to it in the inspector. The same component can be assigned to several managers to share it between them:

| Component | Holds |
| --- | --- |
| `ContextsManager` | The `IContextProvider`s queried for context before a message is sent. |
| `ToolsManager` | The `IToolProvider`s whose tools the model can call, and the tool calls it makes. |

Each has an `autoDiscoverProviders` toggle. While it is on (the default), the component registers every provider on its own GameObject when it wakes up; call `DiscoverProviders()` again after adding providers at runtime. Turn it off to control the list yourself.

Only providers on the same GameObject are discovered. A provider on another GameObject, or one that is not a component, goes in the component's `providerObjects` list, or is registered with `AddProviders`.

A manager with no `ToolsManager` assigned sends no tools, and logs an error if the model answers with a tool call; with no `ContextsManager` assigned it sends no context.

### Streaming

`LLMRequestManager.SendRequestStreaming(onDelta, ct)` sends the request with `stream: true`. Chunks are reported to `onDelta` as they arrive on the main thread, and the call returns the response assembled from them once the request completes — the same object `SendRequest` returns, so `finish_reason`, `usage` and tool calls go through the same post-processing. `ChatRequestManager.MessageStreaming` exposes that as an async sequence of `(reply, reasoning)` chunks, and only ends once the reply has been recorded in the history.

Cancelling the token aborts the connection. `recordTokenUsage` on the manager controls whether the request asks for token usage with `stream_options`.

### Context Providers

Plug into the prompt-building pipeline to supply structured context — entities, message history, and utility data — serialized into JSON schemas so models return reliable, typed output.

### Entities
`EntityManager<T>` — typed manager for game entities; entities are serialized into the context and can be registered/queried by id.

### Message history and serialization

A `MessageHistory` is an ordered list of `MessageEntry` objects. It is serialized in two different shapes, depending on where it is going.

| Path | Entry point | Used for |
| --- | --- | --- |
| Request | `MessageHistory.ToJSON()`, `MessageEntry.GetJObjects(mode)` | The `messages` array of an API request. |
| Local | `MessageHistory.ToJSONFull()`, `MessageEntry.GetFullJObjects()` | `Save()` / `SaveAs()`, and anything else that has to reproduce the history later. |

The request payload is shaped for the model:

- `reasoning` is dropped, except on assistant messages that carry tool calls, where providers expect `reasoning_content` to be echoed back so the round can continue.
- Contexts are not sent as data. They are expanded for the anchor entry only - appended to its content or emitted as a separate `<contexts>` user message - and hidden on every other entry.
- `usage` and `timestamps` are local bookkeeping and are never sent.

The local form keeps everything the history needs to reload:

- Every field, including `usage`, `timestamps`, and the original `contexts` array as supplied by the context providers.
- The archived entries owned by a `SummaryMessage`, nested under `summarized_entries`.
- Tool responses, written as `role: "tool"` objects directly after the assistant message that requested them, the same way the API represents them.

Because contexts are persisted as data rather than as the expanded prompt, loading a saved history restores exactly what was saved. Contexts return to the message that carried them and are expanded again the next time a request is built, so a save/load round trip never adds or drops entries.

#### Message entry types

`MessageEntry` is abstract so that a history can hold entries which are more than one API message:

- `Message` - a single API message: role, content, optional reasoning, tool calls, contexts, usage and timestamps.
- `SummaryMessage` - a `Message` standing in for an archived range of entries, owning the originals so they can be recovered with `RecoverArchived()`.

Add a new entry type only when an entry carries state of its own that has to survive a save. Role is not a reason to subclass: user, assistant, system and tool messages share the same fields and differ only in which of them are populated. A subtype is marked in the local form with its own key (for example `is_summary`), and any entry without that key loads as a plain `Message`, so files written before a subtype existed - or by hand as a plain API array - still load.

### Tools

Declare tools via `IToolProvider` / `ToolDescriptor`; the LLM can invoke them during a request, and results are fed back into the conversation. A `ToolsManager` collects the providers (see **Context and tools managers**) and runs the calls.

### Schema-based prompts

Attributes such as `[SchemaDescription]` and `[SchemaSerialize]` convert plain C# classes into JSON schemas, guiding the model to produce structured output.

## Samples

The package includes runnable samples (import from the Package Manager):

- **Chat** — chat UI with streaming responses.
- **Visual Novel** — roleplay scene with character entities and tool-driven NPCs.
- **NPC** — player/NPC controller context.

## Quick start

1. Add a `ChatRequestManager` to a GameObject.
2. Add a `DeepSeekAPISetting` to the same GameObject and set its setting name and model. The samples use `deepseek-flash`.
3. Press **Save** and enter the player's key in the popup, or set `setting.key` at runtime.
4. Import the Chat Sample from the Package Manager, open its scene, and press Play.

## Requirements

This version of the package is compatible with the Unity Editor 6000.4 and later.

The package declares Newtonsoft Json and the Test Framework for the runtime and its tests, and Input System with uGUI for the samples. A Unity package cannot declare a dependency for its samples alone, so all four are installed with the package; only the samples use Input System and uGUI, and the runtime keeps working if the two are removed from the package's dependencies.

## Package contents

The following table indicates the location of the important folders in this package:

| Location | Description |
| --- | --- |
| `Runtime/` | Runtime code: API settings, context managers/providers, entities, messages, tools, schema. |
| `Editor/` | Editor-only code: inspectors and windows for entities, messages, and context managers. |
| `Samples~/` | Importable samples. |
| `Documentation~/` | This documentation. |
| `Tests/` | EditMode and PlayMode test assemblies. |
