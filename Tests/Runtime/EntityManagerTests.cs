using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ContextManagement.Tests
{
	[TestFixture]
	public class EntityManagerTests
	{
		private GameObject _go;
		private TestItemManager _manager;

		[SetUp]
		public void SetUp()
		{
			_go = new GameObject("entity manager");
			_go.SetActive(false);
			_manager = _go.AddComponent<TestItemManager>();
			_go.SetActive(true);
		}

		[TearDown]
		public void TearDown()
		{
			LogAssert.ignoreFailingMessages = false;
			if (_go != null) Object.DestroyImmediate(_go);
		}

		[Test]
		public void ReportsItsEntityTypeAndNames()
		{
			Assert.AreEqual(typeof(TestItem), _manager.EntityType);
			Assert.AreEqual("item", _manager.EntityName);
			Assert.AreEqual("items", _manager.EntityNamePlural);
		}

		[Test]
		public void AddedEntitiesAreRetrievableById()
		{
			var item = new TestItem { Name = "first", Value = 1 };

			Assert.IsTrue(_manager.Add("a", item));
			Assert.IsFalse(_manager.Add("a", new TestItem { Name = "second" }));

			Assert.IsTrue(_manager.TryGet("a", out var found));
			Assert.AreSame(item, found);
			Assert.AreSame(item, _manager.Get("a"));
			CollectionAssert.AreEqual(new[] { item }, _manager.GetAll().ToArray());
			CollectionAssert.AreEqual(new object[] { item }, _manager.GetAllUntyped().ToArray());
		}

		[Test]
		public void DeletingReportsWhetherTheEntityWasThere()
		{
			_manager.Add("a", new TestItem());

			Assert.IsTrue(_manager.Delete("a"));
			Assert.IsFalse(_manager.Delete("a"));
			Assert.IsFalse(_manager.TryGet("a", out var missing));
			Assert.IsNull(missing);
		}

		[Test]
		public void AskingForAMissingEntityThrows()
		{
			Assert.Throws<KeyNotFoundException>(() => _manager.Get("missing"));
		}

		[Test]
		public void AddingAnEntityWithAnEmptyIdIsAllowed()
		{
			// The dictionary keys on whatever the caller passes; an empty id is a key like any other.
			Assert.IsTrue(_manager.Add("", new TestItem { Name = "anonymous" }));
			Assert.AreEqual("anonymous", _manager.Get("").Name);
		}

		//==========================================================
		//  Random selection
		//==========================================================

		[Test]
		public void RandomSelectionPicksEntitiesOfTheManager()
		{
			var first = new TestItem { Name = "first" };
			var second = new TestItem { Name = "second" };
			_manager.Add("a", first);
			_manager.Add("b", second);

			var picked = _manager.GetRandom(1).Single();

			Assert.IsTrue(ReferenceEquals(picked, first) || ReferenceEquals(picked, second));
		}

		[Test]
		public void RandomSelectionNeverRepeatsAnEntity()
		{
			_manager.Add("a", new TestItem { Name = "a" });
			_manager.Add("b", new TestItem { Name = "b" });
			_manager.Add("c", new TestItem { Name = "c" });

			var picked = _manager.GetRandom(3).ToArray();

			Assert.AreEqual(3, picked.Length);
			CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, picked.Select(item => item.Name));
		}

		[Test]
		public void RandomSelectionStopsAtTheNumberOfEntities()
		{
			_manager.Add("a", new TestItem { Name = "a" });
			_manager.Add("b", new TestItem { Name = "b" });

			Assert.AreEqual(2, _manager.GetRandom(10).Count());
		}

		[Test]
		public void RandomSelectionIsEmptyWhenThereIsNothingToPick()
		{
			Assert.IsEmpty(_manager.GetRandom(3));

			_manager.Add("a", new TestItem { Name = "a" });

			Assert.IsEmpty(_manager.GetRandom(0));
			Assert.IsEmpty(_manager.GetRandom(-1));
		}

		[Test]
		public void RandomSelectionVariesInsteadOfReturningTheSameEntity()
		{
			_manager.Add("a", new TestItem { Name = "a" });
			_manager.Add("b", new TestItem { Name = "b" });
			_manager.Add("c", new TestItem { Name = "c" });

			var seen = new HashSet<string>();
			for (int i = 0; i < 64; i++)
				seen.Add(_manager.GetRandom(1).Single().Name);

			Assert.Greater(seen.Count, 1,
				"a random selection should not always come back with the same entity");
		}

		[Test]
		public void TheSerializedFormRebuildsTheEntities()
		{
			_manager.Add("a", new TestItem { Name = "first", Value = 1 });
			_manager.Add("b", new TestItem { Name = "second", Value = 2 });

			_manager.OnBeforeSerialize();
			_manager.entities.Clear();
			_manager.OnAfterDeserialize();

			Assert.AreEqual(2, _manager.entities.Count);
			Assert.AreEqual("first", _manager.Get("a").Name);
			Assert.AreEqual(2, _manager.Get("b").Value);
		}

		[Test]
		public void SerializeCapturesTheCurrentEntities()
		{
			_manager.Add("a", new TestItem { Name = "first" });

			_manager.Serialize();
			_manager.entities.Clear();
			_manager.OnAfterDeserialize();

			Assert.AreEqual("first", _manager.Get("a").Name);
		}

		[Test]
		public void AnEmptyManagerSerializesToNothing()
		{
			_manager.OnBeforeSerialize();
			_manager.OnAfterDeserialize();

			Assert.AreEqual(0, _manager.entities.Count);
			Assert.AreEqual(0, _manager.GetAll().Count());
		}

		[Test]
		public void DeserializingWithoutSerializingFirstIsSafe()
		{
			_manager.OnAfterDeserialize();

			Assert.AreEqual(0, _manager.entities.Count);
		}
	}
}
