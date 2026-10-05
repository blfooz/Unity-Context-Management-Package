using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class ContextUtilityTests
	{
		[TestCase("already_snake", "already_snake")]
		[TestCase("camelCase", "camel_case")]
		[TestCase("PascalCase", "pascal_case")]
		[TestCase("HTTPResponse", "http_response")]
		[TestCase("Id", "id")]
		[TestCase("A", "a")]
		public void ConvertsNamesToSnakeCase(string name, string expected)
			=> Assert.AreEqual(expected, ContextUtility.ToSnakeCase(name));

		[Test]
		public void LeavesAnEmptyOrMissingNameAsItIs()
		{
			Assert.IsNull(ContextUtility.ToSnakeCase(null));
			Assert.AreEqual("", ContextUtility.ToSnakeCase(""));
		}

		[Test]
		public void TreatsADigitAsPartOfTheWordBeforeIt()
			=> Assert.AreEqual("field2name", ContextUtility.ToSnakeCase("field2Name"));

		[Test]
		public void StringsAndPrimitivesAreNotCollections()
		{
			Assert.IsFalse(ContextUtility.IsTypeCollection(typeof(string), out var fromString));
			Assert.IsNull(fromString);

			Assert.IsFalse(ContextUtility.IsTypeCollection(typeof(int), out var fromInt));
			Assert.IsNull(fromInt);
		}

		[Test]
		public void ArraysAndGenericListsAreCollections()
		{
			Assert.IsTrue(ContextUtility.IsTypeCollection(typeof(int[]), out var fromArray));
			Assert.AreEqual(typeof(int), fromArray);

			Assert.IsTrue(ContextUtility.IsTypeCollection(typeof(List<string>), out var fromList));
			Assert.AreEqual(typeof(string), fromList);

			Assert.IsTrue(ContextUtility.IsTypeCollection(typeof(string[]), out var fromStrings));
			Assert.AreEqual(typeof(string), fromStrings);
		}

		[Test]
		public void ExtractsAndTrimsTheContentAndReasoning()
		{
			var response = JObject.Parse(
				"{\"choices\":[{\"message\":{\"content\":\" hi \",\"reasoning_content\":\" because \"}}]}");

			var (output, reasoning) = response.ExtractResponse();

			Assert.AreEqual("hi", output);
			Assert.AreEqual("because", reasoning);
		}

		[Test]
		public void FallsBackToTheReasoningField()
		{
			var response = JObject.Parse(
				"{\"choices\":[{\"message\":{\"content\":\"hi\",\"reasoning\":\"why\"}}]}");

			Assert.AreEqual("why", response.ExtractResponse().reasoning);
		}

		[Test]
		public void ReportsMissingContentAndReasoningAsNull()
		{
			var response = JObject.Parse("{\"choices\":[{\"message\":{}}]}");

			var (output, reasoning) = response.ExtractResponse();

			Assert.IsNull(output);
			Assert.IsNull(reasoning);
		}

		[Test]
		public void ReadsTheRequestedChoice()
		{
			var response = JObject.Parse(
				"{\"choices\":[{\"message\":{\"content\":\"a\"}},{\"message\":{\"content\":\"b\"}}]}");

			Assert.AreEqual("b", response.ExtractResponse(1).output);
		}

		[Test]
		public void AFailedRequestExtractsToNothing()
		{
			var (output, reasoning) = ContextUtility.ExtractResponse(null);

			Assert.IsNull(output);
			Assert.IsNull(reasoning);
		}

		[Test]
		public void AResponseWithoutAChoiceExtractsToNothing()
		{
			var response = JObject.Parse("{\"error\":{\"message\":\"rate limited\"}}");

			var (output, reasoning) = response.ExtractResponse();

			Assert.IsNull(output);
			Assert.IsNull(reasoning);
		}

		[Test]
		public void AChoiceThatIsNotThereExtractsToNothing()
		{
			var response = JObject.Parse("{\"choices\":[{\"message\":{\"content\":\"a\"}}]}");

			Assert.IsNull(response.ExtractResponse(3).output);
			Assert.IsNull(response.ExtractResponse(-1).output);
			Assert.IsNull(JObject.Parse("{\"choices\":[{\"message\":42}]}").ExtractResponse().output);
		}
	}
}
