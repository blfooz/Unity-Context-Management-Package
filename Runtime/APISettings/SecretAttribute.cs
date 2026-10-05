using UnityEngine;

namespace ContextManagement
{
	/// <summary>
	/// Draw a string field as a secret in the inspector: masked, with a reveal toggle, so a
	/// key is not readable over a shoulder or in a screenshot.
	/// </summary>
	public class SecretAttribute : PropertyAttribute { }
}
