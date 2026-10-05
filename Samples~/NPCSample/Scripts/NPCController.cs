using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ContextManagement;
using UnityEngine;
using UnityEngine.UI;

public class NPCController : CharacterBaseController, IEnumProvider
{
	public float eachSentenceDelay = 3;
	private RawImage hearingIcon;
	private ChatRequestManager cm;

	protected override void Start()
	{
		base.Start();

		hearingIcon = GetComponentInChildren<RawImage>();
		if (hearingIcon != null)
			hearingIcon.enabled = false;

		cm = GetComponentInChildren<ChatRequestManager>();
	}
	protected override void Update()
	{
		base.Update();
	}

	public IEnumerable<string> GetEnums()
	{
		foreach (var entity in entitiesInRange)
			yield return entity.name;

	}

	bool occupied = false;
	protected override void OnTriggerEnter2D(Collider2D collision)
	{
		base.OnTriggerEnter2D(collision);

		if (hearingIcon != null)
			hearingIcon.enabled = true;
		if (!occupied) Listen("(user have entered conversation range.)");
	}

	protected override void OnTriggerExit2D(Collider2D collision)
	{
		base.OnTriggerExit2D(collision);

		if (hearingIcon != null)
			hearingIcon.enabled = false;
	}

	async public void Listen(string speech)
	{
		(string reply, string reasoning) = await cm.Message(speech);
		if (!string.IsNullOrWhiteSpace(reasoning))
			print(reasoning);
		foreach (var sen in SplitByPunctuation(reply))
		{
			Speak(sen);
			await Awaitable.WaitForSecondsAsync(eachSentenceDelay);
		}
	}

	public static string[] SplitByPunctuation(string input)
	{
		// Punctuation characters to split on – extend as needed.
		string pattern = @"(?<=[.,!?;:])(?=\s|$)";

		// Split and remove any empty entries that can appear at the very end.
		return Regex.Split(input, pattern)
					.Where(s => s.Length > 0)
					.ToArray();
	}

	public async void ToolDeliverItem(ToolCallContext ctx)
	{
		Vector3 origin = transform.position;
		var item = ctx["item"].ToString().Trim();
		var person = ctx["person"].ToString();
		Transform personTransform = null;
		foreach (var entity in entitiesInRange)
			if (entity.name == person) { personTransform = entity.transform; break; }

		if (!Book.books.TryGetValue(item, out var book))
			ctx.Throw(new(item + " does not exist."));
		else if (personTransform == null)
			ctx.Throw(new(person + " does not exist"));
		else
		{
			occupied = true;
			await GoTo(book.transform.position);
			book.transform.SetParent(transform, true);
			await GoTo(personTransform);
			book.transform.SetParent(transform.parent, true);
			ctx.Return(item + " delivered to " + person);
			await GoTo(origin);
			occupied = false;

		}
	}
}
