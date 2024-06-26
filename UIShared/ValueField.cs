﻿using ColossalFramework.UI;
using ModsCommon.Utilities;
using System;
using System.ComponentModel;
using UnityEngine;

namespace ModsCommon.UI
{
    public interface IValueChanger<TypeValue> : IReusable
    {
        event Action<TypeValue> OnValueChanged;
        TypeValue Value { get; set; }
        string Format { set; }
    }
    public abstract class UITextField<TypeValue> : CustomUITextField, IValueChanger<TypeValue>, IReusable
    {
        private static string DefaultFormat => "{0}";
        public static float DefaultTextScale => 0.7f;

        public event Action<TypeValue> OnValueChanged;

        private TypeValue value;
        private string format;
        bool IReusable.InCache { get; set; }
        Transform IReusable.CachedTransform { get => m_CachedTransform; set => m_CachedTransform = value; }

        private bool InProcess { get; set; } = false;
        public TypeValue Value
        {
            get => value;
            set => ValueChanged(value, false);
        }
        public string Format
        {
            private get => !string.IsNullOrEmpty(format) ? format : DefaultFormat;
            set
            {
                format = value;
                RefreshText();
            }
        }
        public void SimulateEnterValue(TypeValue value) => ValueChanged(value, true);

        protected virtual void ValueChanged(TypeValue value, bool callEvent = true)
        {
            if (!InProcess)
            {
                InProcess = true;

                this.value = value;
                if (callEvent)
                    OnValueChanged?.Invoke(this.value);

                RefreshText();

                InProcess = false;
            }
        }
        protected void RefreshText() => text = hasFocus ? GetString(Value) : FormatString(Value);

        public virtual void DeInit()
        {
            OnValueChanged = null;
            Unfocus();
            SetDefault();
        }
        public virtual void SetDefault()
        {
            m_Text = string.Empty;
            value = default;
            format = null;
        }
        protected string FormatString(TypeValue value) => string.Format(Format, GetString(value));
        protected virtual string GetString(TypeValue value) => value?.ToString() ?? string.Empty;

        protected override void OnGotFocus(UIFocusEventParameter p)
        {
            RefreshText();
            base.OnGotFocus(p);
        }
        protected override void OnCancel(OnUnfocus onUnfocus)
        {
            base.OnCancel(onUnfocus);
            RefreshText();
        }
        protected override void OnSubmit(OnUnfocus onUnfocus)
        {
            var force = hasFocus;
            base.OnSubmit(onUnfocus);

            if (!force && text == GetString(Value))
            {
                RefreshText();
                return;
            }

            var newValue = default(TypeValue);
            try
            {
                if (typeof(TypeValue) == typeof(string))
                    newValue = (TypeValue)(object)text;
                else if (!string.IsNullOrEmpty(text))
                    newValue = (TypeValue)TypeDescriptor.GetConverter(typeof(TypeValue)).ConvertFromString(text);
            }
            catch { }

            ValueChanged(newValue);
        }

        public override string ToString() => Value.ToString();
        public static implicit operator TypeValue(UITextField<TypeValue> field) => field.Value;

        [Obsolete]
        public void SetDefaultStyle()
        {
            this.DefaultStyle();
            textScale = DefaultTextScale;
        }
    }
    public abstract class ComparableUITextField<ValueType> : UITextField<ValueType>
        where ValueType : IComparable<ValueType>
    {
        public ValueType MinValue { get; set; }
        public ValueType MaxValue { get; set; }
        public bool CheckMax { get; set; }
        public bool CheckMin { get; set; }
        public bool CyclicalValue { get; set; }
        public bool Limited => CheckMax && CheckMin;

        public bool UseWheel { get; set; }
        public ValueType WheelStep { get; set; }
        public bool WheelTip
        {
            set => tooltip = value ? CommonLocalize.FieldPanel_ScrollWheel : string.Empty;
        }
        public bool CanWheel { get; set; }
        private WheelMode Mode
        {
            get
            {
                if (Utility.ShiftIsPressed)
                    return WheelMode.High;
                else if (Utility.CtrlIsPressed)
                    return WheelMode.Low;
                else if (Utility.AltIsPressed)
                    return WheelMode.VeryLow;
                else
                    return WheelMode.Normal;
            }
        }

        protected override void ValueChanged(ValueType value, bool callEvent = true)
        {
            if (CheckMin && value.CompareTo(MinValue) < 0)
                value = MinValue;

            if (CheckMax && value.CompareTo(MaxValue) > 0)
                value = MaxValue;

            base.ValueChanged(value, callEvent);
        }
        public override void DeInit()
        {
            base.DeInit();
            SetDefault();
        }
        protected override void OnMouseMove(UIMouseEventParameter p)
        {
            base.OnMouseMove(p);
            CanWheel = true;
        }
        protected override void OnMouseLeave(UIMouseEventParameter p)
        {
            base.OnMouseLeave(p);
            CanWheel = false;
        }
        protected sealed override void OnMouseWheel(UIMouseEventParameter p)
        {
            m_TooltipShowing = true;
            tooltipBox.Hide();

            if (UseWheel && (CanWheel || Time.realtimeSinceStartup - m_HoveringStartTime >= UIHelper.PropertyScrollTimeout))
            {
                if (p.wheelDelta < 0)
                    ValueChanged(Decrement(Limited && CyclicalValue && Value.CompareTo(MinValue) == 0 ? MaxValue : Value, WheelStep, Mode));
                else
                    ValueChanged(Increment(Limited && CyclicalValue && Value.CompareTo(MaxValue) == 0 ? MinValue : Value, WheelStep, Mode));

                p.Use();
            }
        }
        protected override void OnTooltipEnter(UIMouseEventParameter p)
        {
            base.OnTooltipEnter(p);

            if (!isEnabled)
                m_TooltipShowing = true;
        }

        protected abstract ValueType Increment(ValueType value, ValueType step, WheelMode mode);
        protected abstract ValueType Decrement(ValueType value, ValueType step, WheelMode mode);

        public ComparableUITextField() => SetDefault();

        public override void SetDefault()
        {
            base.SetDefault();

            MinValue = default;
            MaxValue = default;
            CheckMin = false;
            CheckMax = false;
            CyclicalValue = false;
            UseWheel = false;
            WheelTip = false;
            WheelStep = default;
        }

        protected enum WheelMode
        {
            High,
            Normal,
            Low,
            VeryLow,
        }
    }
    public class FloatUITextField : ComparableUITextField<float>
    {
        static string DefaultNumberFormat => "0.###";
        private string _numberFormat;
        public string NumberFormat
        {
            private get => !string.IsNullOrEmpty(_numberFormat) ? _numberFormat : DefaultNumberFormat;
            set
            {
                _numberFormat = value;
                RefreshText();
            }
        }

        public override string text
        {
            get => base.text.Replace(',', '.');
            set => base.text = value;
        }
        protected override float Decrement(float value, float step, WheelMode mode)
        {
            step = GetStep(step, mode);
            return (value - step).RoundToNearest(step);
        }
        protected override float Increment(float value, float step, WheelMode mode)
        {
            step = GetStep(step, mode);
            return (value + step).RoundToNearest(step);
        }

        private float GetStep(float step, WheelMode mode) => mode switch
        {
            WheelMode.High => step * 10,
            WheelMode.Low => step / 10,
            WheelMode.VeryLow => step / 100,
            _ => step,
        };

        public override void DeInit()
        {
            base.DeInit();
            _numberFormat = null;
        }
        protected override string GetString(float value) => value.ToString(NumberFormat);
    }
    public class IntUITextField : ComparableUITextField<int>
    {
        protected override int Decrement(int value, int step, WheelMode mode) => value == int.MinValue ? value : value - GetStep(step, mode);
        protected override int Increment(int value, int step, WheelMode mode) => value == int.MaxValue ? value : value + GetStep(step, mode);
        private int GetStep(int step, WheelMode mode) => mode switch
        {
            WheelMode.High => step * 10,
            WheelMode.Low => Math.Max(step / 10, 1),
            WheelMode.VeryLow => Math.Max(step / 100, 1),
            _ => step,
        };
    }
    public class ByteUITextField : ComparableUITextField<byte>
    {
        protected override byte Decrement(byte value, byte step, WheelMode mode)
        {
            step = GetStep(step, mode);
            return value < step ? byte.MinValue : (byte)(value - step);
        }
        protected override byte Increment(byte value, byte step, WheelMode mode)
        {
            step = GetStep(step, mode);
            return byte.MaxValue - value < step ? byte.MaxValue : (byte)(value + step);
        }

        private byte GetStep(byte step, WheelMode mode) => mode switch
        {
            WheelMode.High => (byte)Math.Min(step * 10, byte.MaxValue),
            WheelMode.Low => (byte)Math.Max(step / 10, 1),
            WheelMode.VeryLow => (byte)Math.Max(step / 100, 1),
            _ => step,
        };
    }
    public class StringUITextField : UITextField<string> 
    {
        public Func<string, string> CheckValue { private get; set; }
        protected override void ValueChanged(string value, bool callEvent = true)
        {
            if (CheckValue != null)
                value = CheckValue(value);

            base.ValueChanged(value, callEvent);
        }
    }
}
