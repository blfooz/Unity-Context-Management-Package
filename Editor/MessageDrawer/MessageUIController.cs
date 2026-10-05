

using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextManagement
{
	public class MessageUIController
	{
		Message linkedMessage;
		public MessageUIController(object linkedMessage)
		{
			if (linkedMessage is Message msg)
				this.linkedMessage = msg;
			else Debug.LogError("Given object is not a Message");
		}

		[CreateProperty]
		public string TimestampsString
		{
			get
			{
				if (linkedMessage.timestamps == null) return "";
				var ts = linkedMessage.timestamps;
				string created = ts.HasCreated
					? DateTimeOffset.FromUnixTimeSeconds(ts.created).ToString("yyyy-MM-dd HH:mm:ss")
					: "-";
				return
					$"Sent: {DateTimeOffset.FromUnixTimeSeconds(ts.sent):yyyy-MM-dd HH:mm:ss}  |  " +
					$"Created: {created}  |  " +
					$"Received: {DateTimeOffset.FromUnixTimeSeconds(ts.received):yyyy-MM-dd HH:mm:ss}\nLatency: {ts.received - ts.sent}";
			}
		}


		[CreateProperty]
		public string TokenUsageString
		{
			get
			{
				if (linkedMessage.tokenUsage == null) return "";
				var tu = linkedMessage.tokenUsage;
				return $"Prompt tokens: {tu.promptTokens}  |  Completion tokens: {tu.completionTokens}";
			}
		}

		[CreateProperty]
		public string PreContentString
		{
			get
			{
				if (linkedMessage.role == Role.user) return string.Join('\n', linkedMessage.Contexts);
				else return linkedMessage.reasoning;
			}
		}


		[CreateProperty]
		public StyleEnum<DisplayStyle> ShowToolCalls
			=> linkedMessage.HasToolCalls ? DisplayStyle.Flex : DisplayStyle.None;

		[CreateProperty]
		public StyleEnum<DisplayStyle> ShowSummary
					=> linkedMessage is SummaryMessage ? DisplayStyle.Flex : DisplayStyle.None;

	}
}
