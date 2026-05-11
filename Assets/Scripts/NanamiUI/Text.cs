using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NanamiUI
{
    [UxmlElement]
    public partial class Text : VisualElement
    {
        private readonly TextElement _face = Layer(0, 0);
        private readonly List<TextElement> _outline = new();
        private string _text;
        private string _font;
        private string _strokeColor;
        private float _strokeSize;

        public Text()
        {
            pickingMode = PickingMode.Ignore;
            Add(_face);
        }

        [UxmlAttribute]
        public string text
        {
            get => _text;
            set
            {
                _text = value;
                _face.text = value;
                foreach (var label in _outline)
                    label.text = value;
            }
        }

        [UxmlAttribute("font")]
        public string font
        {
            get => _font;
            set
            {
                _font = value;
                style.unityFont = new StyleFont(Font.CreateDynamicFontFromOSFont(_font.Split(',').Select(x => x.Trim()).ToArray(), 16));
            }
        }

        [UxmlAttribute("stroke-color")]
        public string strokeColor
        {
            get => _strokeColor;
            set
            {
                _strokeColor = value;
                RebuildOutline();
            }
        }

        [UxmlAttribute("stroke-size")]
        public float strokeSize
        {
            get => _strokeSize;
            set
            {
                _strokeSize = value;
                RebuildOutline();
            }
        }

        private void RebuildOutline()
        {
            foreach (var label in _outline)
                label.RemoveFromHierarchy();
            _outline.Clear();

            if (_strokeSize == 0 || _strokeColor == null)
                return;

            ColorUtility.TryParseHtmlString(_strokeColor, out var color);
            foreach (var offset in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                var label = Layer(offset.Item1 * _strokeSize, offset.Item2 * _strokeSize);
                label.text = _text;
                label.style.color = color;
                Insert(0, label);
                _outline.Add(label);
            }
        }

        private static TextElement Layer(float left, float top)
        {
            var label = new TextElement
            {
                pickingMode = PickingMode.Ignore,
                enableRichText = false,
                parseEscapeSequences = false,
                style =
                {
                    position = Position.Absolute,
                    left = left,
                    top = top,
                    width = Length.Percent(100),
                    height = Length.Percent(100),
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0
                }
            };
            return label;
        }
    }
}
