using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    public class Loader : MovieClip
    {
        public enum FillType
        {
            None,
            Scale,
            ScaleMatchHeight,
            ScaleMatchWidth,
            ScaleFree,
            ScaleNoBorder,
        }

        public enum AlignType
        {
            Left,
            Center,
            Right,
        }

        public enum VertAlignType
        {
            Top,
            Middle,
            Bottom,
        }

        public FillType fill;
        public AlignType align;
        public VertAlignType vAlign;
        public bool autoSize;
        [SerializeField] internal Component componentContent;
        [SerializeField] internal Vector2 componentSize;
        [SerializeField] private bool _showComponent;

        public Component content => _showComponent ? componentContent : null;

        public new Sprite sprite
        {
            get => base.sprite;
            set
            {
                playing = false;
                SetImage(value);
            }
        }

        public void ShowContent()
        {
            playing = false;
            base.sprite = null;
            _showComponent = true;
            componentContent.gameObject.SetActive(true);
            if (autoSize) SetNativeSize();
            LayoutContent();
            SetVerticesDirty();
        }

        protected override void SetFrameSprite(Sprite value) => SetImage(value);

        private void SetImage(Sprite value)
        {
            _showComponent = false;
            if (componentContent != null) componentContent.gameObject.SetActive(false);
            base.sprite = value;
            if (autoSize) SetNativeSize();
            SetVerticesDirty();
        }

        public override void SetNativeSize()
        {
            if (!_showComponent && overrideSprite != null)
            {
                base.SetNativeSize();
                return;
            }
            rectTransform.anchorMax = rectTransform.anchorMin;
            rectTransform.sizeDelta = _showComponent ? componentSize : new Vector2(50, 30);
            SetAllDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_showComponent) LayoutContent();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (_showComponent) LayoutContent();
        }

        private void LayoutContent()
        {
            var child = (RectTransform)componentContent.transform;
            var scale = Scale(rectTransform.rect.size, componentSize);
            child.localScale = new Vector3(scale.x, scale.y, 1);
            var offset = Offset(rectTransform.rect.size, Vector2.Scale(componentSize, scale));
            child.anchoredPosition = new Vector2(offset.x, -offset.y);
        }

        private Vector2 Scale(Vector2 available, Vector2 source)
        {
            if (fill == FillType.None) return Vector2.one;
            var scale = available / source;
            return fill switch
            {
                FillType.ScaleMatchHeight => Vector2.one * scale.y,
                FillType.ScaleMatchWidth => Vector2.one * scale.x,
                FillType.Scale => Vector2.one * Mathf.Min(scale.x, scale.y),
                FillType.ScaleNoBorder => Vector2.one * Mathf.Max(scale.x, scale.y),
                _ => scale,
            };
        }

        private Vector2 Offset(Vector2 available, Vector2 size) => new(
            align switch { AlignType.Center => (available.x - size.x) / 2, AlignType.Right => available.x - size.x, _ => 0 },
            vAlign switch { VertAlignType.Middle => (available.y - size.y) / 2, VertAlignType.Bottom => available.y - size.y, _ => 0 });

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (_showComponent || overrideSprite == null)
            {
                vh.Clear();
                return;
            }
            if (type != Type.Simple)
            {
                base.OnPopulateMesh(vh);
                return;
            }

            var rect = GetPixelAdjustedRect();
            var size = overrideSprite.rect.size;
            var displayed = Vector2.Scale(size, Scale(rect.size, size));
            var offset = Offset(rect.size, displayed);
            var w = displayed.x;
            var h = displayed.y;
            var x = rect.xMin + offset.x;
            var y = rect.yMax - offset.y;
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(overrideSprite);
            var color32 = (Color32)color;

            vh.Clear();
            vh.AddVert(new Vector3(x, y - h), color32, new Vector2(uv.x, uv.y));
            vh.AddVert(new Vector3(x, y), color32, new Vector2(uv.x, uv.w));
            vh.AddVert(new Vector3(x + w, y), color32, new Vector2(uv.z, uv.w));
            vh.AddVert(new Vector3(x + w, y - h), color32, new Vector2(uv.z, uv.y));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }
    }
}
