using UnityEngine.EventSystems;

namespace NanamiUI
{
    public class Component : UIBehaviour
    {
        protected internal virtual int GetControllerPage(int controller) => -1;

        protected internal virtual void SetControllerPage(int controller, int page)
        {
        }
    }
}
