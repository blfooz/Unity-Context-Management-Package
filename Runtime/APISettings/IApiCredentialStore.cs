namespace ContextManagement
{
	/// <summary>
	/// Resolves API keys outside the settings file.
	/// </summary>
	/// <remarks>
	/// Implement this to keep credentials in a platform key store, a backend, or anywhere
	/// else. When an <see cref="APISetting"/> supplies one through
	/// <see cref="APISetting.Credential"/>, the settings file no longer contains the key: the
	/// store is asked for it when loading and asked to save it when writing.
	/// </remarks>
	public interface IApiCredentialStore
	{
		/// <summary>
		/// Return the key for <paramref name="setting"/>, or null/empty when none is stored.
		/// </summary>
		string GetKey(APISetting setting);

		/// <summary>
		/// Store the key for <paramref name="setting"/>. <paramref name="key"/> may be null or empty.
		/// </summary>
		void SetKey(APISetting setting, string key);
	}
}
