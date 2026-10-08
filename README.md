# Context Management

A framework for building LLM-driven experiences in Unity. It provides the plumbing to connect your game to LLM APIs, keep a persistent structured world context, define agents with tools, and build chat experiences — so you can focus on the game, not on JSON plumbing.

## Features

- **API Settings** — runtime provider settings for OpenAI, DeepSeek, OpenRouter, and any OpenAI-compatible endpoint (streaming supported).
- **Context Managers** — `LLMRequestManager` and `ChatRequestManager` orchestrate requests, streaming, summaries, and tool calls.
- **Context Providers** — structured context (entities, messages, utility) that is serialized into prompts via schema.
- **Schema-based prompts** — `[SchemaDescription]`/`[SchemaSerialize]` attributes turn plain C# classes into JSON schemas for reliable model output.
- **Entity Manager** — define and manage entities that facilitate more complex system-API interactions.
- **Tool calling** — declare tools, execute them from LLM responses, and feed results back into the conversation.

## Installation

Install from the Unity Package Manager:

1. Open **Window ▸ Package Manager**.
2. Click the **+** button and choose **Add package from git URL…**.
3. Paste `https://github.com/blfooz/Unity-Context-Management-Package.git` and click **Add**.

The package is not on a public registry. It installs from that repository, or from a tarball produced with `npm pack` inside the package folder, through **Add package from tarball**.

### Requirements

- Unity 6000.4 or later
- [Newtonsoft Json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@latest) — used by the runtime.
- [Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@latest) — used by the package's test assembly.
- [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest) and [UI (uGUI)](https://docs.unity3d.com/Packages/com.unity.ugui@latest) — used only by the samples; nothing in `Runtime/` references them.

All four are declared in [`package.json`](package.json) and are installed with the package. Unity packages cannot declare a dependency that applies only to their samples, so Input System and uGUI arrive with the package even when the samples are not imported. If a project must avoid them, remove the two entries from the package's `dependencies` when embedding it — the runtime keeps working, but the samples no longer compile.

## API settings and API keys

API settings are a component: add the provider component (`DeepSeekAPISetting`, `OpenAIAPISetting`, `OpenRouterAPISetting`, or `OpenAICompatibleAPISetting`) to the same GameObject as the manager that sends requests. The component both configures the endpoint and implements the request. You can add support for other API providers by extending the base class.

```csharp
var settings = gameObject.AddComponent<DeepSeekAPISetting>();
settings.settingName = "DS";
settings.model = "deepseek-flash";
settings.key = keyFromThePlayer;
```

By default an API setting component saves a plain-text JSON file, named after `settingName`, under `Application.persistentDataPath/APISettings`, and loads it when it wakes up. The file contains the API key by default. To handle the key yourself, subclass the provider setting and override `Credential` with an `IApiCredentialStore` to keep keys in a platform key store, a backend, or anywhere else; the settings file then omits the key and the store supplies it:

```csharp
public class StoredDeepSeekSetting : DeepSeekAPISetting
{
    public override IApiCredentialStore Credential => myCredentialStore;
}
```

## Quick start

1. Add a `ChatRequestManager` component to a GameObject.
2. Add a `DeepSeekAPISetting` component, or any other provider you prefer, to the same GameObject, and set its setting name and model.
3. Press **Save** and enter the API key in the popup window; the component writes a settings file to the default path and loads it when it wakes up.
4. Use `SendRequest()` or `Message(string message)` in script to send requests.

See the [Documentation](Documentation~/Context%20Management.md) for details, and the included samples:

- **Chat** — basic chat UI with streaming support.
- **Visual Novel** — visual novel with character entities and tool-driven agents.
- **NPC** — player/NPC controller context.

## License

[MIT](LICENSE.md)
