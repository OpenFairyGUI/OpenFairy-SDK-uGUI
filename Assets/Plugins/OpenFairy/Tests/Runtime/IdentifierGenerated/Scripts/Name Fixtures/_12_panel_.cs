using OpenFairy.UGUI;
using UnityEngine;
using UnityEngine.UI;
#if FAIRYGUI_TMPRO
using OpenFairyTextComponent = TMPro.TextMeshProUGUI;
using OpenFairyInputComponent = TMPro.TMP_InputField;
#else
using OpenFairyTextComponent = OpenFairy.UGUI.TextField;
using OpenFairyInputComponent = OpenFairy.UGUI.TextInput;
#endif

namespace Example._class.Name_Fixtures
{
    public partial class _12_panel_ : global::OpenFairy.UGUI.Component
    {
        public enum _classPages
        {
            _default,
            _classPages,
            value___2,
            状态,
            é,
            _9_first,
        }
        public Controller<_classPages> _class;
        public enum _classPages_2
        {
            _default,
            _classPages,
            value___2,
            状态,
            é,
            _9_first,
        }
        public Controller<_classPages_2> _class_2;
        public enum x_yPages
        {
            _default,
            _classPages,
            value___2,
            状态,
            é,
            _9_first,
        }
        public Controller<x_yPages> x_y;
        public enum x_yPages_2
        {
            _default,
            _classPages,
            value___2,
            状态,
            é,
            _9_first,
        }
        public Controller<x_yPages_2> x_y_2;
        public enum _12_panel_Pages
        {
            _default,
            _classPages,
            value___2,
            状态,
            é,
            _9_first,
        }
        public Controller<_12_panel_Pages> _12_panel__2;
        public enum UnityEnginePages
        {
            _default,
            _classPages,
            value___2,
            状态,
            é,
            _9_first,
        }
        public Controller<UnityEnginePages> UnityEngine_2;
        public enum 状态Pages
        {
            _default,
            _classPages,
            value___2,
            状态,
            é,
            _9_first,
        }
        public Controller<状态Pages> 状态;
        public enum _Pages
        {
            _default,
            _classPages,
            value___2,
            状态,
            é,
            _9_first,
        }
        public Controller<_Pages> _;

        protected internal override int GetControllerPage(int controller) => controller switch
        {
            0 => (int)_class.page,
            1 => (int)_class_2.page,
            2 => (int)x_y.page,
            3 => (int)x_y_2.page,
            4 => (int)_12_panel__2.page,
            5 => (int)UnityEngine_2.page,
            6 => (int)状态.page,
            7 => (int)_.page,
            _ => -1,
        };

        protected internal override bool IsControllerChanging(int controller) => controller switch
        {
            0 => _class.changing,
            1 => _class_2.changing,
            2 => x_y.changing,
            3 => x_y_2.changing,
            4 => _12_panel__2.changing,
            5 => UnityEngine_2.changing,
            6 => 状态.changing,
            7 => _.changing,
            _ => false,
        };

        protected internal override void SetControllerPage(int controller, int page)
        {
            switch (controller)
            {
                case 0: _class.page = (_classPages)page; break;
                case 1: _class_2.page = (_classPages_2)page; break;
                case 2: x_y.page = (x_yPages)page; break;
                case 3: x_y_2.page = (x_yPages_2)page; break;
                case 4: _12_panel__2.page = (_12_panel_Pages)page; break;
                case 5: UnityEngine_2.page = (UnityEnginePages)page; break;
                case 6: 状态.page = (状态Pages)page; break;
                case 7: _.page = (_Pages)page; break;
            }
        }

        protected override void InitializeControllers()
        {
            _class.Initialize();
            _class_2.Initialize();
            x_y.Initialize();
            x_y_2.Initialize();
            _12_panel__2.Initialize();
            UnityEngine_2.Initialize();
            状态.Initialize();
            _.Initialize();
        }
        public global::UnityEngine.RectTransform _class_3;
        public global::UnityEngine.RectTransform _class_4;
        public global::UnityEngine.RectTransform foo_bar;
        public global::UnityEngine.RectTransform foo_bar_2;
        public global::UnityEngine.RectTransform __2;
        public global::UnityEngine.RectTransform name_2;
        public global::UnityEngine.RectTransform m_Name_;
        public global::UnityEngine.RectTransform m_Name__2;
        public global::UnityEngine.RectTransform GetControllerPage_2;
        public global::UnityEngine.RectTransform t__class_3;
        public global::UnityEngine.RectTransform _12_panel__3;
        public global::UnityEngine.RectTransform UnityEngine_3;
        public global::UnityEngine.RectTransform 同名;
        public global::Example._class.Name_Fixtures.my_page_a my_page;
        public global::Example._class.Name_Fixtures_2.my_page my_page_2;
        public global::OpenFairy.UGUI.Transition t__class;
        public enum _classHook { x_y, x_y_2, value___2, _classHook, _classHook_2, 状态 }
        public void Set_classHook(_classHook label, global::System.Action callback)
        {
            switch (label)
            {
                case _classHook.x_y: t__class.SetHook(0, false, callback); break;
                case _classHook.x_y_2: t__class.SetHook(1, false, callback); break;
                case _classHook.value___2: t__class.SetHook(2, false, callback); break;
                case _classHook._classHook: t__class.SetHook(3, false, callback); break;
                case _classHook._classHook_2: t__class.SetHook(4, false, callback); break;
                case _classHook.状态: t__class.SetHook(5, false, callback); break;
            }
        }
        public global::OpenFairy.UGUI.Transition t__class_2;
        public enum _classHook_2 { x_y, x_y_2, value___2, _classHook, _classHook_2, 状态 }
        public void Set_classHook_2(_classHook_2 label, global::System.Action callback)
        {
            switch (label)
            {
                case _classHook_2.x_y: t__class_2.SetHook(0, false, callback); break;
                case _classHook_2.x_y_2: t__class_2.SetHook(1, false, callback); break;
                case _classHook_2.value___2: t__class_2.SetHook(2, false, callback); break;
                case _classHook_2._classHook: t__class_2.SetHook(3, false, callback); break;
                case _classHook_2._classHook_2: t__class_2.SetHook(4, false, callback); break;
                case _classHook_2.状态: t__class_2.SetHook(5, false, callback); break;
            }
        }
        public global::OpenFairy.UGUI.Transition t__;
        public enum _Hook { x_y, x_y_2, value___2, _classHook, _classHook_2, 状态 }
        public void Set_Hook(_Hook label, global::System.Action callback)
        {
            switch (label)
            {
                case _Hook.x_y: t__.SetHook(0, false, callback); break;
                case _Hook.x_y_2: t__.SetHook(1, false, callback); break;
                case _Hook.value___2: t__.SetHook(2, false, callback); break;
                case _Hook._classHook: t__.SetHook(3, false, callback); break;
                case _Hook._classHook_2: t__.SetHook(4, false, callback); break;
                case _Hook.状态: t__.SetHook(5, false, callback); break;
            }
        }
[global::UnityEngine.SerializeField] private OpenFairyTextComponent _localizedText1 = null;
[global::UnityEngine.SerializeReference] private global::OpenFairy.UGUI.GearText<x_yPages_2> _localizedGear1_0 = null;
[global::System.Serializable]
public sealed class Localized_classTexts
{
    public string Text;
    public string GearDefault1;
    public string Gear1_0;
    public string Gear1_1;
}
[global::UnityEngine.SerializeField] private Localized_classTexts _Localized_classTexts = null;
public void ApplyLocalized_classTexts(Localized_classTexts texts)
{
    _Localized_classTexts = texts;
    RefreshLocalized_classText();
}
public void RefreshLocalized_classText()
{
    _localizedText1.text = _Localized_classTexts.Text;
    _localizedGear1_0.defaultValue = _Localized_classTexts.GearDefault1;
    _localizedGear1_0.values[0] = _Localized_classTexts.Gear1_0;
    _localizedGear1_0.values[1] = _Localized_classTexts.Gear1_1;
    _localizedGear1_0.Apply(x_y_2.page);
}

[global::System.Serializable] public sealed class LocalizedTexts
{
public Localized_classTexts Localized_class;
}
public void ApplyLocale(global::Example._class.Localization.LocaleTable texts)
{
my_page.ApplyLocale(texts);
my_page_2.ApplyLocale(texts);
ApplyLocalized_classTexts(texts.Name_Fixtures__12_panel_.Localized_class);
}

    }
}
