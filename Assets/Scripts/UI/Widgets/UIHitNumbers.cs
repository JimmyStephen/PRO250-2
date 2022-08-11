using System.Collections.Generic;
using UnityEngine;

namespace Projectiles.UI
{
	public class UIHitNumbers : UIWidget
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private UIHitNumber _hitItem;

		private List<UIHitNumber> _activeItems   = new List<UIHitNumber>(32);
		private List<UIHitNumber> _inactiveItems = new List<UIHitNumber>(32);

		private RectTransform _canvasRectTransform;

		private List<HitData> _pendingHits = new List<HitData>(32);

		// PUBLIC METHODS

		public void HitPerformed(HitData hitData)
		{
			for (int i = 0; i < _pendingHits.Count; i++)
			{
				var pending = _pendingHits[i];

				// Try to merge hit data
				if (pending.Target == hitData.Target && pending.Target != null)
				{
					pending.Amount += hitData.Amount;
					pending.IsFatal |= hitData.IsFatal;

					_pendingHits[i] = pending;
					return;
				}
			}

			_pendingHits.Add(hitData);
		}

		// UIWidget INTERFACE

		protected override void OnInitialize()
		{
			_hitItem.SetActive(false);

			_canvasRectTransform = SceneUI.Canvas.transform as RectTransform;
		}

		protected override void OnHidden()
		{
			_pendingHits.Clear();
		}

		protected override void OnTick()
		{
			base.OnTick();

			for (int i = 0; i < _pendingHits.Count; i++)
			{
				ProcessHit(_pendingHits[i]);
			}

			_pendingHits.Clear();
		}

		// MONOBEHAVIOUR

		protected void LateUpdate()
		{
			UpdateActiveItems(_activeItems, _inactiveItems);
		}

		// PRIVATE METHODS

		private void ProcessHit(HitData hitData)
		{
			var hitItem = _inactiveItems.PopLast();
			if (hitItem == null)
			{
				hitItem = Instantiate(_hitItem, _hitItem.transform.parent);
			}

			_activeItems.Add(hitItem);

			var hitPosition = hitData.Position;
			if (hitData.Target != null)
			{
				hitPosition = hitData.Target.HeadPivot.position;
			}

			hitItem.SetNumber(hitData.Amount);
			hitItem.WorldPosition = hitPosition;

			hitItem.SetActive(true);
			hitItem.transform.SetAsLastSibling();
		}

		private void UpdateActiveItems(List<UIHitNumber> activeItems, List<UIHitNumber> inactiveItems)
		{
			for (int i = 0; i < _activeItems.Count; i++)
			{
				var item = activeItems[i];
				if (item.IsFinished == true)
				{
					item.SetActive(false);
					activeItems.RemoveBySwap(i);
					inactiveItems.Add(item);
				}
				else
				{
					item.transform.position = GetUIPosition(item.WorldPosition);
				}
			}
		}

		private Vector3 GetUIPosition(Vector3 worldPosition)
		{
			var screenPoint = Context.Camera.Camera.WorldToScreenPoint(worldPosition);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRectTransform, screenPoint, SceneUI.Canvas.worldCamera, out Vector2 screenPosition);
			return _canvasRectTransform.TransformPoint(screenPosition);
		}
	}
}
