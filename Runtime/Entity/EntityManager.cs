using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextManagement
{
	public abstract class EntityManager<T> : PromptComponent, IEntityManager, ISerializationCallbackReceiver where T : class, new()
	{
		public bool logOnOperations = false;

		public readonly Dictionary<string, EntityEntry> entities = new();
		[SerializeReference, HideInInspector] List<EntityEntry> _serializedEntities;

		[Serializable]
		public class EntityEntry
		{
			public string id;
			public T entity;
			public List<string> addendums;
		}

		public void OnBeforeSerialize()
		{
			_serializedEntities = new(entities.Values);
		}
		public void Serialize() => OnBeforeSerialize();

		public void OnAfterDeserialize()
		{
			entities.Clear();
			if (_serializedEntities == null) return;
			foreach (var e in _serializedEntities)
				entities[e.id] = e;
		}

		//Specifiable entity infos
		public abstract string EntityName { get; }
		public virtual string EntityNamePlural => EntityName + 's';

		public Type EntityType => typeof(T);

		//Basic Operations

		/// <returns>returns false if given id already exist.</returns>
		public bool Add(string id, T entity)
		{
			if (entities.TryAdd(id, new() { id = id, entity = entity }))
			{
				if (logOnOperations) Debug.Log($"{id} successfully added.\n{entity}");
				OnAdd(id, entity);
				return true;
			}
			else
			{
				if (logOnOperations) Debug.Log($"{id} already exist:\n{entities[id]}");
				return false;
			}
		}

		protected virtual void OnAdd(string id, T entity) { }

		public IEnumerable<T> GetAll() => entities.Values.Select((e) => e.entity);

		/// <summary>
		/// A random selection of up to <paramref name="count"/> entities, in random order and
		/// without repeats. Fewer are returned when the manager holds fewer, and none when it is
		/// empty or <paramref name="count"/> is zero or negative.
		/// </summary>
		/// <remarks>
		/// The draw uses <see cref="UnityEngine.Random"/>, so seeding it makes a selection
		/// reproducible. The result is a snapshot: changes to the manager afterwards do not
		/// alter it.
		/// </remarks>
		public IEnumerable<T> GetRandom(int count)
		{
			if (count <= 0 || entities.Count == 0) return Array.Empty<T>();

			var selection = entities.Values.Select(entry => entry.entity).ToList();

			// Fisher-Yates, so every entity is equally likely to be drawn, and to land in any
			// position of the result.
			for (int i = selection.Count - 1; i > 0; i--)
			{
				int j = UnityEngine.Random.Range(0, i + 1);
				T swap = selection[i];
				selection[i] = selection[j];
				selection[j] = swap;
			}

			if (count < selection.Count)
				selection.RemoveRange(count, selection.Count - count);

			return selection;
		}

		public IEnumerable<object> GetAllUntyped() => GetAll();

		public bool TryGet(string id, out T entity)
		{
			if (entities.TryGetValue(id, out var entry))
			{
				entity = entry.entity;
				return true;
			}
			entity = null;
			return false;
		}

		public T Get(string id) => entities[id].entity;

		public bool Delete(string id)
		{
			if (entities.ContainsKey(id))
			{
				entities.Remove(id);
				if (logOnOperations) Debug.Log($"id: {id} deleted");
				return true;
			}
			else
			{
				if (logOnOperations) Debug.Log($"id: {id} does not exist");
				return false;
			}
		}

	}
}
