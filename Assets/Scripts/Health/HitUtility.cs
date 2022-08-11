using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	public enum EHitAction : byte
	{
		None,
		Damage,
		Heal,
	}

	public struct HitData
	{
		public EHitAction     Action;
		public float          Amount;
		public bool           IsFatal;
		public Vector3        Position;
		public Vector3        Direction;
		public Vector3        Normal;
		public PlayerRef      InstigatorRef;
		public IHitInstigator Instigator;
		public IHitTarget     Target;
		public EHitType       HitType;
	}

	public enum EHitType
	{
		None,
		Projectile,
		Explosion,
		Suicide,
	}

	public interface IHitTarget
	{
		bool      IsActive    { get; }
		Transform HeadPivot   { get; }
		Transform BodyPivot   { get; }
		Transform GroundPivot { get; }

		void ProcessHit(ref HitData hit);
	}

	public interface IHitInstigator
	{
		void HitPerformed(HitData hit);
	}

	public static class HitUtility
	{
		// PUBLIC METHODS

		public static void GetAllTargets(NetworkRunner runner, List<IHitTarget> targets, bool onlyActive = true)
		{
			targets.Clear();

			var healths = ListPool.Get<Health>(targets.Count);

			// TODO: Best would be to get IHitTargets directly, but that is not possible for now
			runner.GetAllBehaviours(healths);

			if (onlyActive == true)
			{
				for (int i = 0; i < healths.Count; i++)
				{
					var target = healths[i] as IHitTarget;

					if (target.IsActive == true)
						targets.Add(target);
				}
			}
			else
			{
				targets.AddRange(healths);
			}

			ListPool.Return(healths);
		}

		public static HitData ProcessHit(PlayerRef instigatorRef, Vector3 direction, LagCompensatedHit hit, float baseDamage, EHitType hitType)
		{
			IHitTarget target = hit.Hitbox != null ? hit.Hitbox.Root.GetComponent<IHitTarget>() : null;
			if (target == null)
				return default;

			HitData hitData = default;

			hitData.Action        = EHitAction.Damage;
			hitData.Amount        = baseDamage;
			hitData.Position      = hit.Point;
			hitData.Normal        = hit.Normal;
			hitData.Direction     = direction;
			hitData.Target        = target;
			hitData.InstigatorRef = instigatorRef;
			hitData.HitType       = hitType;

			return ProcessHit(ref hitData);
		}

		public static HitData ProcessHit(NetworkBehaviour instigator, Vector3 direction, LagCompensatedHit hit, float baseDamage, EHitType hitType)
		{
			IHitTarget target = hit.Hitbox != null ? hit.Hitbox.Root.GetComponent<IHitTarget>() : null;
			if (target == null)
				return default;

			if (hit.Hitbox.Root.gameObject == instigator)
				return default;

			HitData hitData = default;

			hitData.Action        = EHitAction.Damage;
			hitData.Amount        = baseDamage;
			hitData.Position      = hit.Point;
			hitData.Normal        = hit.Normal;
			hitData.Direction     = direction;
			hitData.Target        = target;
			hitData.InstigatorRef = instigator != null ? instigator.Object.InputAuthority : default;
			hitData.Instigator    = instigator != null ? instigator.GetComponent<IHitInstigator>() : null;
			hitData.HitType       = hitType;

			return ProcessHit(ref hitData);
		}

		public static HitData ProcessHit(NetworkBehaviour instigator, Collider collider, float damage, EHitType hitType)
		{
			var target = collider.GetComponentInParent<IHitTarget>();
			if (target == null)
				return default;

			HitData hitData = default;

			hitData.Action        = EHitAction.Damage;
			hitData.Amount        = damage;
			hitData.InstigatorRef = instigator.Object.InputAuthority;
			hitData.Instigator    = instigator.GetComponent<IHitInstigator>();
			hitData.Position      = collider.transform.position;
			hitData.Normal        = (instigator.transform.position - collider.transform.position).normalized;
			hitData.Direction     = -hitData.Normal;
			hitData.Target        = target;
			hitData.HitType       = hitType;

			return ProcessHit(ref hitData);
		}

		public static HitData ProcessHit(ref HitData hitData)
		{
			hitData.Target.ProcessHit(ref hitData);

			// For local debug targets we show hit feedback immediately
			// if (hitData.Instigator != null && hitData.Target is Health == false)
			// {
			// 	hitData.Instigator.HitPerformed(hitData);
			// }

			return hitData;
		}
	}
}
