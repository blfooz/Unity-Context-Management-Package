using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

namespace ContextManagement.EditorTests
{
	/// <summary>
	/// The scenes and prefabs that ship in <c>Samples~</c> are the ones a user imports, so every
	/// asset they reference by GUID has to resolve in a project that has the package and nothing
	/// else. A reference that only resolves in this working copy is invisible to the compiler -
	/// which is how a sample once shipped pointing at a file it does not carry.
	/// </summary>
	[TestFixture]
	public class SampleReferencesTests
	{
		const string PackageRoot = "Packages/com.blfooz.contextmanagement";
		const string PackageSamples = PackageRoot + "/Samples~";

		static readonly Regex GuidPattern = new("guid: ([0-9a-f]{32})");
		static readonly Regex GuidAttributePattern = new("guid=([0-9a-f]{32})");
		static readonly Regex BuiltInGuidPattern = new("^0{16}[ef]0{15}$");

		/// <summary>
		/// The serialized sample assets. UI Toolkit documents (<c>.uxml</c>) and style sheets
		/// (<c>.uss</c>, <c>.tss</c>) reference assets too, with a different syntax
		/// (<c>guid=...</c>) than the YAML assets (<c>guid: ...</c>), so they are scanned as
		/// well: a UI Toolkit reference used to slip past this guard.
		/// </summary>
		static IEnumerable<string> SampleAssets()
			=> Directory.EnumerateFiles(PackageSamples, "*", SearchOption.AllDirectories)
				.Where(path => path.EndsWith(".unity")
					|| path.EndsWith(".prefab")
					|| path.EndsWith(".asset")
					|| path.EndsWith(".uxml")
					|| path.EndsWith(".uss")
					|| path.EndsWith(".tss"));

		static IEnumerable<string> Placeholders(string fileName)
			=> Directory.EnumerateFiles(PackageSamples, fileName, SearchOption.AllDirectories);

		/// <summary>
		/// Every asset the package ships. A reference to one of these travels with the package,
		/// whichever copy of it the AssetDatabase happens to resolve (an imported sample in
		/// Assets/ and the Samples~ copy it came from share a GUID).
		/// </summary>
		static HashSet<string> ShippedGuids()
			=> Directory.EnumerateFiles(PackageRoot, "*.meta", SearchOption.AllDirectories)
				.Select(File.ReadAllText)
				.Select(text => GuidPattern.Match(text))
				.Where(match => match.Success)
				.Select(match => match.Groups[1].Value)
				.ToHashSet();

		[Test]
		public void EveryAssetTheSamplesReferenceTravelsWithThePackage()
		{
			var offenders = new List<string>();
			var shipped = ShippedGuids();

			foreach (var sampleAsset in SampleAssets())
			{
				string text = File.ReadAllText(sampleAsset);

				foreach (Match match in GuidPattern.Matches(text))
					Check(sampleAsset, match.Groups[1].Value);

				foreach (Match match in GuidAttributePattern.Matches(text))
					Check(sampleAsset, match.Groups[1].Value);
			}

			CollectionAssert.IsEmpty(offenders.Distinct(), string.Join("\n", offenders.Distinct()));

			void Check(string sampleAsset, string guid)
			{
				if (shipped.Contains(guid)) return;
				if (BuiltInGuidPattern.IsMatch(guid)) return;

				string path = AssetDatabase.GUIDToAssetPath(guid);

				// Another Unity package is installed with this one.
				if (path.StartsWith("Packages/")) return;

				offenders.Add(string.IsNullOrEmpty(path)
					? $"{sampleAsset}: {guid} does not resolve"
					: $"{sampleAsset}: {guid} -> {path}");
			}
		}

		[Test]
		public void SamplesDoNotSerializeAnApiKey()
		{
			var offenders = SampleAssets()
				.Where(path => File.ReadAllText(path)
					.Split('\n')
					.Any(line => line.TrimStart().StartsWith("apiKey:")
						|| line.TrimStart().StartsWith("key:")))
				.ToArray();

			CollectionAssert.IsEmpty(offenders,
				"Sample assets must not serialize an API key. The key belongs in the player's "
				+ "settings file or a custom credential store, not in a scene or prefab: "
				+ string.Join(", ", offenders));
		}

		[Test]
		public void ThePanelSettingsReferenceAThemeThePackageShips()
		{
			var shipped = ShippedGuids();
			var missing = new List<string>();

			foreach (var path in Placeholders("Sample Panel Settings.asset"))
			{
				var match = Regex.Match(File.ReadAllText(path), @"themeUss: \{fileID: -?\d+, guid: ([0-9a-f]{32})");
				Assert.IsTrue(match.Success, $"{path} has no theme assigned");

				string themeGuid = match.Groups[1].Value;
				if (!shipped.Contains(themeGuid))
					missing.Add($"{path} -> theme {themeGuid}");
			}

			CollectionAssert.IsEmpty(missing, string.Join("\n", missing));
		}
	}
}
