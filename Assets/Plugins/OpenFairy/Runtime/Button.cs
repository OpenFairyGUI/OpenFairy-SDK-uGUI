using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if FAIRYGUI_TMPRO
using TextComponent = TMPro.TextMeshProUGUI;
#else
using TextComponent = OpenFairy.UGUI.TextField;
#endif

namespace OpenFairy.UGUI
{
    public enum ButtonMode
    {
        Common,
        Check,
        Radio,
    }

    // 复刻 FairyGUI GButton.downEffect：按下态压暗直接子图形或绕中心缩放。
    public enum ButtonDownEffect
    {
        None,
        Dark,
        Scale,
    }

    // 非泛型按钮基类（复刻 FairyGUI GButton 面）：让 Root/Window/PopupMenu/Migrate 不必知道 T 就能设标题/图标/选中态。
    // 关联控制器（FairyGUI relatedController/relatedPageId）由 Migrate 从 <Button controller=.. page=..> 烘焙；
    // 换页→选中态的反向同步由烘焙进控制器的 GearButton 完成（复刻 GButton.HandleControllerChanged）。
    public abstract class ButtonBase : Component
    {
        public UnityEvent onClick = new();
        public UnityEvent onRightClick = new();
        public UnityEvent onChanged = new(); // 复刻 GButton.onChanged：仅用户点击翻转 Check/Radio 时发

        public ButtonMode mode;
        public string selectedTitle;
        public bool changeStateOnClick = true; // 复刻 GButton：列表项交给 ListSelection 管选择时置 false，禁本体自翻 selected
        public ButtonDownEffect downEffect;
        public float downEffectValue = .8f;
        [SerializeField] internal Graphic[] downEffectTargets = Array.Empty<Graphic>(); // Dark：烘焙的直接子图形（不含文本）
        [SerializeField, HideInInspector] private bool _downScaled;

        [SerializeField] internal AudioClip clickSound;
        [SerializeField] internal float clickSoundVolume = 1;
        public AudioClip sound { get => clickSound; set => clickSound = value; }
        public float soundVolumeScale { get => clickSoundVolume; set => clickSoundVolume = value; }

        protected void PlayClickSound()
        {
            if (clickSound != null)
                Root.Ensure(this).soundSource.PlayOneShot(clickSound, clickSoundVolume);
        }

        // 烘焙接线（Migrate 写入），不属用户 API 面。
        [SerializeField] internal TextComponent titleText;
        [SerializeField] internal Loader iconLoader;
        [SerializeField] internal Component relatedOwner;
        [SerializeField] internal int relatedController = -1;
        [SerializeField] internal int relatedPage = -1;
        [SerializeField] internal bool _selected; // Migrate 烘焙直写（绕过 setter 的关联控制器逻辑）
        [SerializeField] internal bool _grayed;

        [SerializeField]
        private string _title;

        public string title
        {
            get => _title;
            set
            {
                _title = value;
                RefreshTitle();
            }
        }

        [SerializeField] private Sprite _icon;
        [SerializeField] private Sprite _selectedIcon;
        [SerializeField] private bool _useSelectedIcon;

        public Sprite icon
        {
            get => _icon;
            set { _icon = value; RefreshIcon(); }
        }

        public Sprite selectedIcon
        {
            get => _selectedIcon;
            set
            {
                _selectedIcon = value;
                _useSelectedIcon = value != null;
                RefreshIcon();
            }
        }

        // A selected override can intentionally contain no sprite.
        public bool useSelectedIcon
        {
            get => _useSelectedIcon;
            set { _useSelectedIcon = value; RefreshIcon(); }
        }

        internal void RefreshIcon()
        {
            if (iconLoader == null)
                return;
            var sprite = selected && _useSelectedIcon ? _selectedIcon : _icon;
            iconLoader.sprite = sprite;
            iconLoader.enabled = sprite != null;
        }

        // 复刻 GButton.selected 的 setter：Common 忽略选中态、同值早返回；
        // 选中把关联控制器设到本页，Check 取消且当前正在本页则回对页（2 页控制器的 oppositePageId）。
        public bool selected
        {
            get => _selected;
            set
            {
                if (mode == ButtonMode.Common || _selected == value)
                    return;
                _selected = value;
                RefreshState();
                if (_useSelectedIcon) RefreshIcon();
                if (HasRelatedController && Application.isPlaying) // 烘焙期选中态由控制器初始页经 GearButton 驱动，不反向写页
                {
                    if (_selected)
                        relatedOwner.SetControllerPage(relatedController, relatedPage);
                    else if (mode == ButtonMode.Check && relatedOwner.GetControllerPage(relatedController) == relatedPage)
                        relatedOwner.SetControllerPage(relatedController, relatedPage == 0 ? 1 : 0);
                }
            }
        }

        // Graying selects the disabled visual pages; it does not disable pointer input.
        public bool grayed
        {
            get => _grayed;
            set
            {
                _grayed = value;
                RefreshState();
            }
        }

        internal bool HasRelatedController =>
            relatedOwner != null && relatedController >= 0 && relatedPage >= 0;

        public abstract void RefreshState();

        protected void RefreshTitle()
        {
            if (titleText == null)
                return;
            _title ??= titleText.text;
            titleText.text = selected && !string.IsNullOrEmpty(selectedTitle) ? selectedTitle : _title;
        }

        // 复刻 GButton.SetState 的 downEffect：按下态即 down/selectedOver/selectedDisabled 页，运行时生效。
        private protected void ApplyDownEffect(bool pressed)
        {
            if (!Application.isPlaying)
                return;
            if (downEffect == ButtonDownEffect.Dark)
            {
                var color = pressed ? new Color(downEffectValue, downEffectValue, downEffectValue) : Color.white;
                foreach (var graphic in downEffectTargets)
                    graphic.color = color;
            }
            else if (downEffect == ButtonDownEffect.Scale && pressed != _downScaled)
            {
                // FairyGUI 把此类按钮的轴心设为中心；列表项等轴心不在中心时同样绕中心缩放。
                _downScaled = pressed;
                var rt = (RectTransform)transform;
                var center = rt.TransformPoint(rt.rect.center);
                var factor = pressed ? downEffectValue : 1 / downEffectValue;
                rt.localScale = Vector3.Scale(rt.localScale, new Vector3(factor, factor, 1));
                rt.position += center - rt.TransformPoint(rt.rect.center);
            }
        }
    }

    public abstract class Button<T> : ButtonBase, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler where T : struct, Enum
    {
        protected enum VisualState
        {
            Up,
            Down,
            Over,
            SelectedOver,
            Disabled,
            SelectedDisabled,
        }

        // 复刻 Controller.selectedPage：按页名选页，缺页回落到首页（default(T)）。
        private protected static class StatePages
        {
            public static readonly T Up, Down, Over, SelectedOver, Disabled, SelectedDisabled;
            public static readonly bool HasOver, HasDisabled;

            static StatePages()
            {
                Enum.TryParse("up", out Up);
                Enum.TryParse("down", out Down);
                HasOver = Enum.TryParse("over", out Over);
                Enum.TryParse("selectedOver", out SelectedOver);
                HasDisabled = Enum.TryParse("disabled", out Disabled);
                Enum.TryParse("selectedDisabled", out SelectedDisabled);
            }
        }

        public Controller<T> controller;

        private protected bool _down, _over;

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyDownEffect(mode == ButtonMode.Common ? _down : selected);
        }

        protected override void OnDisable()
        {
            _down = _over = false;
            base.OnDisable();
            RefreshState();
        }

        // Keep pointer state while grayed, including the authored pressed-disabled page.
        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) // 复刻 GButton.__touchBegin：仅左键
                return;
            _down = true;
            if (mode == ButtonMode.Common)
                SetState(grayed && StatePages.HasDisabled ? VisualState.SelectedDisabled : VisualState.Down);
        }

        // 复刻 GButton.__touchEnd：含拖出按钮后在外松开，回到当前逻辑态。
        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !_down)
                return;
            _down = false;
            RefreshState();
        }

        public virtual void OnPointerEnter(PointerEventData eventData) => SetOver(true);

        public virtual void OnPointerExit(PointerEventData eventData) => SetOver(false);

        // 复刻 GButton.__rollover/__rollout：没有 over 页的按钮不跟踪悬浮。
        private void SetOver(bool over)
        {
            if (!StatePages.HasOver)
                return;
            _over = over;
            if (!_down)
                RefreshState();
        }

        // 复刻 GButton.__click：Check/Radio 经 Selected setter 翻选并驱动关联控制器；Common 直接换页。
        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                onRightClick.Invoke();
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            PlayClickSound();
            if (mode == ButtonMode.Check)
            {
                if (changeStateOnClick)
                {
                    selected = !selected;
                    onChanged.Invoke();
                }
            }
            else if (mode == ButtonMode.Radio)
            {
                if (changeStateOnClick && !selected)
                {
                    if (!HasRelatedController)
                        DeselectSiblings(); // 无关联控制器的 radio 组回退：同父兄弟互斥
                    selected = true;
                    onChanged.Invoke();
                }
            }
            else if (HasRelatedController)
                relatedOwner.SetControllerPage(relatedController, relatedPage);
            onClick.Invoke();
        }

        private void DeselectSiblings()
        {
            var parent = transform.parent;
            for (var i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).GetComponent<ButtonBase>() is { } sibling
                    && !ReferenceEquals(sibling, this) && sibling.mode == ButtonMode.Radio && sibling.selected)
                    sibling.selected = false;
        }

        // 复刻 GButton.SetCurrentState：按 grayed/selected/over 择页，不含瞬时按下态（按下由 OnPointerDown 单独处理）。
        public override void RefreshState()
        {
            RefreshTitle();
            if (grayed && StatePages.HasDisabled)
                SetState(selected ? VisualState.SelectedDisabled : VisualState.Disabled);
            else if (selected)
                SetState(_over ? VisualState.SelectedOver : VisualState.Down);
            else
                SetState(_over ? VisualState.Over : VisualState.Up);
        }

        protected void SetState(VisualState state)
        {
            controller.page = state switch
            {
                VisualState.Up => StatePages.Up,
                VisualState.Down => StatePages.Down,
                VisualState.Over => StatePages.Over,
                VisualState.SelectedOver => StatePages.SelectedOver,
                VisualState.Disabled => StatePages.Disabled,
                _ => StatePages.SelectedDisabled,
            };
            ApplyDownEffect(state is VisualState.Down or VisualState.SelectedOver or VisualState.SelectedDisabled);
        }
    }
}
