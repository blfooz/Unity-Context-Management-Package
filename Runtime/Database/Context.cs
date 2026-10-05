using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

namespace ContextManagement
{
	[CreateAssetMenu(fileName = "Context", menuName = "Context Management/Context")]
	public class Context : ScriptableObject
	{
		public TextAsset sourceDocument;

		[TextArea]
		public List<ContextEntry> contextEntries = new();

		[Serializable]
		public class ContextEntry
		{
			public string title;
			public int depth;
			public TextAsset sourceDocument;
			public int start, end;
			public bool enabled = true;

			[CreateProperty]
			public string Content
			{
				get
				{
					if (sourceDocument == null || string.IsNullOrEmpty(sourceDocument.text))
						return string.Empty;

					int safeStart = Mathf.Clamp(start, 0, sourceDocument.text.Length);
					int safeEnd = Mathf.Clamp(end, safeStart, sourceDocument.text.Length);
					return sourceDocument.text[safeStart..safeEnd];
				}
			}
		}

		public int Slice(int i = 0, int depth = 0, string titlePrefix = null)
		{
			if (sourceDocument == null || string.IsNullOrEmpty(sourceDocument.text))
			{
				if (depth == 0)
					contextEntries.Clear();
				return i;
			}

			if (depth == 0)
				contextEntries.Clear();

			var text = sourceDocument.text;
			int lineStart = i;
			while (lineStart < text.Length)
			{

				int lineEnd = text.IndexOf('\n', lineStart);
				if (lineEnd < 0) lineEnd = text.Length; //No more next line

				var headingLevel = GetHeadingLevel(text, lineStart, lineEnd);
				if (headingLevel > depth)
				{
					if (headingLevel != depth + 1)
						throw new Exception("Malformed Markdown(.md) document. Nonconsecutive levels of headers");

					string title = text[(lineStart + headingLevel)..lineEnd].Trim();
					if (!string.IsNullOrWhiteSpace(titlePrefix))
						title = $"{titlePrefix} > {title}";

					int contentStart = lineEnd < text.Length ? lineEnd + 1 : text.Length;

					ContextEntry entry = new() { title = title, depth = headingLevel, sourceDocument = sourceDocument, start = contentStart };
					contextEntries.Add(entry);

					int contentEnd = Slice(contentStart, depth + 1, title);
					if (entry != null)
						entry.end = contentEnd;
					lineStart = contentEnd;
				}
				else if (headingLevel > 0) return lineStart; //Sibling or ancestor heading, section ends
				else lineStart = lineEnd + 1; //Next line
			}
			return text.Length;
		}

		int GetHeadingLevel(string text, int start, int end)
		{
			int level = 0;
			while (start < end && text[start] == '#')
			{
				level++;
				start++;
			}

			if (start >= end)
				return 0;

			if (text[start] == ' ')
				return level;

			return 0;
		}

	}
}
