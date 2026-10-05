using System;
using System.Collections.Generic;

namespace ContextManagement
{
	public interface IEntityManager
	{
		string EntityName { get; }
		string EntityNamePlural { get; }

		Type EntityType{get;}

		IEnumerable<object> GetAllUntyped();

		bool Delete(string id);

		void Serialize();
	}
}
