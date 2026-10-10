using UnityEngine;

namespace BrainsAI
{
	public class BlendshapeCorruption : MonoBehaviour
	{
		public SkinnedMeshRenderer skinnedMesh;
		public int blendShapeIndex = 0;
		public float minWeight = 70f; // 0.7 * 100
		public float maxWeight = 100f;
		public float shakeTime = 2f;

		float timer;

		void Update()
		{
			if (skinnedMesh == null) return;
			timer += Time.deltaTime;
			if (timer >= shakeTime)
			{
				timer = 0;

				skinnedMesh.SetBlendShapeWeight(blendShapeIndex, Random.Range(minWeight, maxWeight));
			}
		}
	}
}
