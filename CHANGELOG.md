# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-10-02

### Added
- `APISetting`, a provider component for OpenAI, DeepSeek, OpenRouter, and OpenAI-compatible endpoints: `DeepSeekAPISetting`, `OpenAIAPISetting`, `OpenRouterAPISetting`, and `OpenAICompatibleAPISetting`. A setting saves one JSON file per setting name under `Application.persistentDataPath/APISettings`, keeps the API key in that file by default, and leaves it out when an `IApiCredentialStore` supplies it instead.
- Context managers and context providers, entity and message systems, tool calling, schema-based prompt generation, and streaming responses with SSE framing, cancellation, and an assembled response that goes through the same post-processing as a non-streaming call.
- `LLMRequestManager.DefaultSystemPrompt`, used when no `PromptComponent` on the manager supplies any prompt text: the manager logs a warning and runs with the default instead of throwing while it initializes.
- `EntityManager<T>.GetRandom(int)` returns a random selection of up to `count` entities, in random order, without repeats.
- `MessageHistory.ToJSONFull()` and `MessageEntry.GetFullJObjects()` for local persistence, as a separate shape from the request payload built by `ToJSON()` and `GetJObjects(mode)`.
- `SseStreamReader` (Server-Sent Events framing and UTF-8 decoding), `StreamAccumulator` (merging streamed chunks into one response) and `AsyncPushQueue<T>` (push-to-pull adaptor), with a test assembly covering them.
- The samples configure their providers inline, ship no API key, and include a placeholder panel settings asset and theme so an imported sample has no references outside the package. An editor test fails when a shipped scene or prefab references an asset the package does not carry.
