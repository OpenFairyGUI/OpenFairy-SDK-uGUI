using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    public class Component : UIBehaviour
    {
        [SerializeField, HideInInspector]
        internal Component[] controllerChildren = Array.Empty<Component>();
        [NonSerialized]
        private bool _controllersInitialized;

        protected override void Awake()
        {
            base.Awake();
            if (Application.isPlaying)
                Initialize();
        }

        internal void Initialize()
        {
            if (_controllersInitialized)
                return;
            _controllersInitialized = true;
            foreach (var child in controllerChildren)
                child.Initialize();
            InitializeControllers();
        }

        protected virtual void InitializeControllers() { }

        protected internal virtual int GetControllerPage(int controller) => -1;

        protected internal virtual bool IsControllerChanging(int controller) => false;

        protected internal virtual void SetControllerPage(int controller, int page)
        {
        }
    }
}
