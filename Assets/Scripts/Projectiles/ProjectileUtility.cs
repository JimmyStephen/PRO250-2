using Fusion;
using System.Collections.Generic;
using UnityEngine;

namespace Projectiles
{
	public static class ProjectileUtility
	{
		public static bool ProjectileCast(NetworkRunner runner, PlayerRef owner, Vector3 firePosition, Vector3 direction, float distance, LayerMask hitMask, out LagCompensatedHit hit)
		{
			var hitOptions = HitOptions.IncludePhysX | HitOptions.SubtickAccuracy | HitOptions.IgnoreInputAuthority;
			return runner.LagCompensation.Raycast(firePosition, direction, distance, owner, out hit, hitMask, hitOptions);
		}

		public static bool ProjectileCastAll(NetworkRunner runner, PlayerRef owner, int ownerObjectInstanceID, Vector3 firePosition, Vector3 direction, float distance, LayerMask hitMask, List<LagCompensatedHit> validHits)
		{
			return ProjectileCastAll(runner, owner, ownerObjectInstanceID, firePosition, direction, 0f, distance, hitMask, validHits);
		}

		public static bool ProjectileCastAll(NetworkRunner runner, PlayerRef owner, int ownerObjectInstanceID, Vector3 firePosition, Vector3 direction, float distanceToTarget, float raycastDistance, LayerMask hitMask, List<LagCompensatedHit> validHits)
		{
			validHits.Clear();

			var hits = ListPool.Get<LagCompensatedHit>(16);

			var hitOptions = HitOptions.IncludePhysX | HitOptions.SubtickAccuracy | HitOptions.IgnoreInputAuthority;
			int hitCount = runner.LagCompensation.RaycastAll(firePosition, direction, raycastDistance, owner, hits, hitMask, true, hitOptions);

			if (hitCount <= 0)
			{
				ListPool.Return(hits);
				return false;
			}

			var hitRoots = ListPool.Get<int>(16);

			float ignoreDistanceMin = 3f;
			float ignoreDistanceMax = Mathf.Max(distanceToTarget - 1f, ignoreDistanceMin);

			hits.SortDistance();

			for (int i = 0; i < hits.Count; i++)
			{
				var hit = hits[i];

				if (distanceToTarget > 0f)
				{
					// For 3rd person cast it is possible that we want to shoot through environment objects a little bit if we already know the destination point
					// => ray from camera is different than ray from 3rd person character
					if (hit.Distance >= ignoreDistanceMin && hit.Distance < ignoreDistanceMax && hit.GameObject != null && ObjectLayerMask.Environment.value.IsBitSet(hit.GameObject.layer))
						continue;
				}

				int hitRootID = hit.Hitbox != null ? hit.Hitbox.Root.gameObject.GetInstanceID() : 0;

				if (hitRootID != 0)
				{
					if (hitRootID == ownerObjectInstanceID)
						continue; // Owner was hit

					if (hitRoots.Contains(hitRootID) == true)
						continue; // Same object was hit multiple times

					hitRoots.Add(hitRootID);
				}

				validHits.Add(hits[i]);
			}

			ListPool.Return(hitRoots);
			ListPool.Return(hits);

			return validHits.Count > 0;
		}

		public static bool CircleCast(NetworkRunner runner, PlayerRef owner, Vector3 firePosition, Vector3 direction, float distance, float radius, int numberOfRays, LayerMask hitMask, out LagCompensatedHit hit)
		{
			hit = default;

			float angleStep = numberOfRays > 2 ? (2f * Mathf.PI) / (numberOfRays - 1) : 0f;
			Matrix4x4 rotationMatrix = Matrix4x4.Rotate(Quaternion.LookRotation(direction));

			for (int i = 0; i < numberOfRays; i++)
			{
				Vector3 position = firePosition;

				// First ray is always directly in the center
				if (i > 0)
				{
					float angle = angleStep * (i - 1);
					var offset = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0f);
					position += rotationMatrix.MultiplyPoint3x4(offset);
				}

				if (ProjectileCast(runner, owner, position, direction, distance, hitMask, out LagCompensatedHit currentHit) == true)
				{
					if (hit.Type == HitType.None || currentHit.Distance < hit.Distance)
					{
						hit = currentHit;
					}
				}
			}

			return hit.Type != HitType.None;
		}
	}
}
