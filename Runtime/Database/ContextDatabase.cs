using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static ContextManagement.Context;

namespace ContextManagement
{
    public class ContextDatabase : MonoBehaviour, IEnumProvider, IToolProvider
    {
        public List<Context> contexts = new();

        // A title can appear in more than one document, or more than once in the same
        // document. The entries are kept together, in document order, and their content is
        // joined when the title is queried.
        private readonly Dictionary<string, List<ContextEntry>> _index = new();
        private bool _indexBuilt;
        private int _indexedContextCount = -1;

        public ToolDescriptor tool;
        public string ToolGetContext(JObject args)
        {
            EnsureIndex();

            string title = null;
            if (args != null && args.TryGetValue("title", out var token))
                title = token.Value<string>();

            return Query(title);
        }

        public IEnumerable<ToolDescriptor> GetToolDescriptors()
        {
            tool ??= new(
            new ToolInfo("get_context", new() { { SchemaEntry.DynamicEnumEntry(this, "title") } }, true,
                "Get contexts about a specific subject."), ToolGetContext);
            yield return tool;
        }

        public IEnumerable<string> GetEnums()
        {
            EnsureIndex();
            return _index.Keys;
        }


        public string Query(string title)
        {
            EnsureIndex();

            if (string.IsNullOrEmpty(title) || !_index.TryGetValue(title, out var entries))
                return "Invalid title, no context found.";

            var sb = new StringBuilder($"# {title}:\n");
            foreach (var entry in entries)
            {
                var content = entry.Content;
                if (string.IsNullOrEmpty(content)) continue;

                sb.Append(content);
                if (content[^1] != '\n') sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Rebuild the index when it is missing or the list of documents changed. Entries
        /// edited in place need an explicit <see cref="BuildIndex"/>.
        /// </summary>
        private void EnsureIndex()
        {
            if (!_indexBuilt || _indexedContextCount != contexts.Count)
                BuildIndex();
        }

        public void BuildIndex()
        {
            _index.Clear();
            foreach (var ctx in contexts)
            {
                if (ctx == null) continue;

                foreach (var entry in ctx.contextEntries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.title)) continue;

                    if (!_index.TryGetValue(entry.title, out var entries))
                        _index[entry.title] = entries = new List<ContextEntry>();

                    entries.Add(entry);
                }
            }

            _indexBuilt = true;
            _indexedContextCount = contexts.Count;
        }

    }
}

