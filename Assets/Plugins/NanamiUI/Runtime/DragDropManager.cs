using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    // 复刻 FairyGUI DragDropManager：拖一个跟随指针的 agent 图标，松手时向命中的 DropTarget 派发 sourceData。
    // agent 放顶层 canvas 最后一个兄弟（= 画在最上，对标 GRoot int.MaxValue sortingOrder），raycastTarget=false 不自挡命中。
    public sealed class DragDropManager
    {
        public static DragDropManager inst { get; } = new();

        private Image _agent;
        private RectTransform _agentRt;
        private GraphicRaycaster _raycaster;
        private object _sourceData;

        public bool dragging => _agent != null && _agent.enabled;

        public void StartDrag(Canvas root, GraphicRaycaster raycaster, Sprite icon, object sourceData, PointerEventData e)
        {
            _sourceData = sourceData;
            _raycaster = raycaster;
            EnsureAgent(root);
            _agent.sprite = icon;
            _agent.enabled = true;
            _agentRt.SetAsLastSibling();
            MoveAgent(e);
        }

        public void MoveAgent(PointerEventData e)
        {
            if (_agentRt == null)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_agentRt.parent, e.position, e.pressEventCamera, out var p);
            _agentRt.anchoredPosition = p;
        }

        public void Drop(PointerEventData e)
        {
            if (_agent != null)
                _agent.enabled = false;
            if (_raycaster != null)
            {
                var results = new List<RaycastResult>();
                _raycaster.Raycast(e, results);
                foreach (var r in results) // agent raycastTarget=false → 不自命中；向上找 DropTarget（复刻 DragDropManager 走 parent 链）
                {
                    var target = r.gameObject.GetComponentInParent<DropTarget>();
                    if (target != null)
                    {
                        target.Fire(_sourceData);
                        break;
                    }
                }
            }
            _sourceData = null;
        }

        private void EnsureAgent(Canvas root)
        {
            if (_agent != null)
            {
                if (_agentRt.parent != root.transform)
                    _agentRt.SetParent(root.transform, false);
                return;
            }
            var go = new GameObject("DragDropAgent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _agentRt = (RectTransform)go.transform;
            _agentRt.SetParent(root.transform, false);
            _agentRt.pivot = new Vector2(0.5f, 0.5f);
            _agentRt.sizeDelta = new Vector2(100, 100);
            _agent = go.GetComponent<Image>();
            _agent.raycastTarget = false;
            _agent.preserveAspect = true;
        }
    }
}
