using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Reflection;

namespace AdvancedControls.Theming
{
    /// <summary>
    /// 속성 창에서 <see cref="AdvCorners"/>를 펼쳐 모서리별로 편집할 수 있게 한다.
    /// 구조체는 값 형식이라 하위 속성을 고쳐도 원본에 반영되지 않으므로,
    /// 편집할 때마다 새 인스턴스를 만들어 돌려주는 CreateInstance가 반드시 필요하다.
    /// </summary>
    public class AdvCornersConverter : ExpandableObjectConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var text = value as string;
            if (text == null) return base.ConvertFrom(context, culture, value);

            text = text.Trim();
            if (text.Length == 0) return new AdvCorners(-1);

            char sep = culture == null ? ',' : culture.TextInfo.ListSeparator[0];
            string[] parts = text.Split(sep);

            try
            {
                if (parts.Length == 1)
                    return new AdvCorners(int.Parse(parts[0].Trim(), culture));

                if (parts.Length == 4)
                    return new AdvCorners(
                        int.Parse(parts[0].Trim(), culture),
                        int.Parse(parts[1].Trim(), culture),
                        int.Parse(parts[2].Trim(), culture),
                        int.Parse(parts[3].Trim(), culture));
            }
            catch (FormatException ex)
            {
                throw new ArgumentException(
                    "모서리 반경은 숫자 1개 또는 4개여야 합니다. 예: 4 또는 4, 4, 0, 0", ex);
            }

            throw new ArgumentException(
                "모서리 반경은 숫자 1개 또는 4개여야 합니다. 예: 4 또는 4, 4, 0, 0");
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture,
                                         object value, Type destinationType)
        {
            if (!(value is AdvCorners))
                return base.ConvertTo(context, culture, value, destinationType);

            var c = (AdvCorners)value;

            if (destinationType == typeof(string))
            {
                string sep = (culture == null ? "," : culture.TextInfo.ListSeparator) + " ";
                return c.TopLeft + sep + c.TopRight + sep + c.BottomRight + sep + c.BottomLeft;
            }

            // 디자이너가 InitializeComponent에 생성자 호출로 써 넣을 수 있게 한다
            if (destinationType == typeof(InstanceDescriptor))
            {
                var ctor = typeof(AdvCorners).GetConstructor(
                    new[] { typeof(int), typeof(int), typeof(int), typeof(int) });

                if (ctor != null)
                    return new InstanceDescriptor(ctor,
                        new object[] { c.TopLeft, c.TopRight, c.BottomRight, c.BottomLeft });
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override bool GetCreateInstanceSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override object CreateInstance(ITypeDescriptorContext context, IDictionary propertyValues)
        {
            if (propertyValues == null) throw new ArgumentNullException("propertyValues");

            return new AdvCorners(
                (int)propertyValues["TopLeft"],
                (int)propertyValues["TopRight"],
                (int)propertyValues["BottomRight"],
                (int)propertyValues["BottomLeft"]);
        }

        public override bool GetPropertiesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        private static readonly string[] EditableNames =
            { "TopLeft", "TopRight", "BottomRight", "BottomLeft" };

        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context,
                                                                   object value, Attribute[] attributes)
        {
            // 편집 가능한 네 개만 시계 방향으로 돌려준다.
            // 전체를 정렬해서 넘기면 IsZero·Max 같은 계산 속성까지 속성 창에 딸려 나온다.
            var all = TypeDescriptor.GetProperties(typeof(AdvCorners), attributes);
            var picked = new List<PropertyDescriptor>(EditableNames.Length);

            foreach (string name in EditableNames)
            {
                var pd = all[name];
                if (pd != null) picked.Add(pd);
            }

            return new PropertyDescriptorCollection(picked.ToArray(), true);
        }
    }
}
