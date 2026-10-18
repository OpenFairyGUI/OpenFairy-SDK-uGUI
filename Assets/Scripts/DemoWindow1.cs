using UnityEngine;

namespace OpenFairy.UGUI.Example
{
    public sealed class DemoWindow1 : Window
    {
        protected override void OnInit()
        {
            DemoFont.Apply(gameObject);
            Center();
        }

        protected override void OnShown()
        {
            contentPane.GetComponentInChildren<ListSource>().Fill(6, (item, i) =>
                item.GetComponent<ButtonBase>().title = i.ToString());
        }
    }
}
