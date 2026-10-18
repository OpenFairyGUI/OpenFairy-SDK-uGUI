using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenFairy.UGUI
{
    [ExecuteAlways, DisallowMultipleComponent]
    public class Grayed : UIBehaviour
    {
        [SerializeField] internal ColorAdjust filter;
        internal bool active { get; private set; }

        protected override void OnEnable()
        {
            base.OnEnable();
            active = true;
            Refresh();
        }

        protected override void OnDisable()
        {
            active = false;
            Refresh();
            base.OnDisable();
        }

        // AddComponent invokes OnEnable before the importer supplies this direct reference.
        internal void Refresh() { if (filter != null) filter.RefreshGrayed(); }
    }
}
