using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;
using Common.Diagnostics;

namespace Common.Diagnostics.UI
{
    public class LogEntryItem : MonoBehaviour, IPointerClickHandler
    {
        private TextMeshProUGUI _textComponent;
        private Image _background;
        private LogData _data;
        private Action<LogData, bool> _onClick; // bool = isDoubleClick

        private static readonly Color ColorInfo = new Color(1f, 1f, 1f, 1f);
        private static readonly Color ColorWarn = new Color(1f, 0.8f, 0.2f, 1f);
        private static readonly Color ColorError = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color ColorSelected = new Color(0.2f, 0.4f, 0.8f, 0.5f);
        private static readonly Color ColorNormal = new Color(0, 0, 0, 0);

        public void Initialize(TextMeshProUGUI textComp, Image bg)
        {
            _textComponent = textComp;
            _background = bg;
        }

        public void Setup(LogData data, bool isSelected, Action<LogData, bool> onClick)
        {
            _data = data;
            _onClick = onClick;

            _textComponent.text = $"[{data.Timestamp:HH:mm:ss}] {data.Message}";
            
            switch (data.Level)
            {
                case LogLevel.Warn:
                    _textComponent.color = ColorWarn;
                    break;
                case LogLevel.Error:
                    _textComponent.color = ColorError;
                    break;
                default:
                    _textComponent.color = ColorInfo;
                    break;
            }

            _background.color = isSelected ? ColorSelected : ColorNormal;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_onClick != null)
            {
                bool isDoubleClick = eventData.clickCount >= 2;
                _onClick(_data, isDoubleClick);
            }
        }
    }
}
