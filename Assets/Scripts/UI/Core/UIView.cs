using System;
using UnityEngine;

namespace Projectiles.UI
{
	[RequireComponent(typeof(CanvasGroup))]
	public abstract class UIView : UIWidget
	{
		// PUBLIC MEMBERS

		public event Action HasOpened;
		public event Action HasClosed;

		public bool         IsOpen         { get; private set; }
		public bool         IsInteractable { get { return CanvasGroup.interactable; } set { CanvasGroup.interactable = value; } }
		public int          Priority       { get; private set; }

		public virtual bool NeedsCursor    => _needsCursor;

		// PRIVATE MEMBERS

		[SerializeField]
		private bool _needsCursor;

		// PUBLIC METHODS

		public void Open()
		{
			SceneUI.Open(this);
		}

		public void Close()
		{
			if (SceneUI == null)
			{
				Debug.Log($"Closing view {gameObject.name} without SceneUI");
				Close_Internal();
			}
			else
			{
				SceneUI.Close(this);
			}
		}

		public void SetState(bool isOpen)
		{
			if (isOpen == true)
			{
				Open();
			}
			else
			{
				Close();
			}
		}

		public bool IsTopView(bool interactableOnly = false)
		{
			return SceneUI.IsTopView(this, interactableOnly);
		}

		// INTERNAL METHODS

		internal void SetPriority(int priority)
		{
			Priority = priority;
		}

		// UIWidget INTERFACE

		protected override void OnInitialize()
		{
		}

		protected override void OnDeinitialize()
		{
			Close_Internal();

			HasOpened = null;
			HasClosed = null;
		}

		public void Tick(float deltaTime)
		{
		}

		// INTERNAL METHODS

		internal void Open_Internal()
		{
			if (IsOpen == true)
				return;
			
			IsOpen = true;

			gameObject.SetActive(true);

			OnOpen();

			if (HasOpened != null)
			{
				HasOpened();
				HasOpened = null;
			}
		}

		internal void Close_Internal()
		{
			if (IsOpen == false)
				return;

			IsOpen = false;

			OnClose();

			gameObject.SetActive(false);

			if (HasClosed != null)
			{
				HasClosed();
				HasClosed = null;
			}
		}

		// UIView INTERFACE

		protected virtual void OnOpen()  { }
		protected virtual void OnClose() { }

		// PROTECTED METHODS

		protected T Switch<T>() where T : UIView
		{
			Close();

			return SceneUI.Open<T>();
		}

		protected T Open<T>() where T : UIView
		{
			return SceneUI.Open<T>();
		}

		protected void Open(UIView view)
		{
			SceneUI.Open(view);
		}
	}
}
