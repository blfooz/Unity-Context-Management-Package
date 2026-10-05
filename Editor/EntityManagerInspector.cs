
using UnityEditor;
using UnityEngine;

namespace ContextManagement
{
	[CustomEditor(typeof(EntityManager<>), true)]
	public class EntityManagerInspector : Editor
	{

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			if (GUILayout.Button("View Entities"))
			{
				EntitiesWindow.Open((IEntityManager)target);
			}
		}


	}
}