using System;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ContextManagement
{
	[Serializable]
	public class Timestamps
	{

		public long sent;
		/// <summary>
		/// Wall-clock time the provider reported the response was created, or <c>0</c> when it
		/// did not report one. Zero is not a plausible creation time, so it doubles as
		/// "not recorded" and is left out of the persisted form.
		/// </summary>
		public long created;
		public long received;
		public long Latency => received - sent;

		/// <summary>Whether the provider reported a creation time.</summary>
		public bool HasCreated => created > 0;

		public static Timestamps FromJSON(JObject json)
		{
			return new()
			{
				sent = json["sent"]?.ToObject<long>() ?? 0,
				created = json["created"]?.ToObject<long>() ?? 0,
				received = json["received"]?.ToObject<long>() ?? 0
			};
		}

		public JObject ToJSON()
		{
			var json = new JObject
			{
				["sent"] = sent,
				["received"] = received
			};
			// Omitted rather than written as zero when the provider did not report one.
			if (HasCreated) json["created"] = created;
			return json;
		}

		public override string ToString()
		{
			var sentDto = DateTimeOffset.FromUnixTimeSeconds(sent);
			var receivedDto = DateTimeOffset.FromUnixTimeSeconds(received);
			bool sameDayAsSent = receivedDto.Date == sentDto.Date;

			string createdText = "-";
			if (HasCreated)
			{
				var createdDto = DateTimeOffset.FromUnixTimeSeconds(created);
				string format = createdDto.Date == sentDto.Date && sameDayAsSent
					? "HH:mm:ss"
					: "yyyy-MM-dd-HH:mm:ss";
				createdText = createdDto.ToString(format);
			}

			StringBuilder sb = new($"Time Sent: {sentDto:yyyy-MM-dd-HH:mm:ss}");
			sb.Append(" | ");
			sb.Append($"Time Created: {createdText}");
			sb.Append(" | ");
			sb.Append($"Time Received: {receivedDto.ToString(sameDayAsSent ? "HH:mm:ss" : "yyyy-MM-dd-HH:mm:ss")}");
			return sb.ToString();
		}
	}
}
