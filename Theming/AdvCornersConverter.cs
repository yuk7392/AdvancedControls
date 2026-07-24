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
    /// <remarks>
    /// 커스텀 디자이너(AdvSplitContainerDesigner)가 컨트롤과 같은 어셈블리에 있으면 VS 디자이너가
    /// 이 어셈블리를 두 로드 컨텍스트로 올려 AdvCorners 형식이 둘 존재할 수 있다. 그러면 여기 들어오는
    /// value가 "다른 컨텍스트의 AdvCorners"라 <c>value is AdvCorners</c>·캐스트가 실패한다. 그래서
    /// 형식 신원 대신 <b>형식 이름 비교 + 리플렉션</b>으로 값을 다뤄 컨텍스트에 무관하게 동작시킨다.
    /// </remarks>
    public class AdvCornersConverter : ExpandableObjectConverter
    {
        private static readonly Type[] IntCtorArgs = { typeof(int), typeof(int), typeof(int), typeof(int) };

        /// <summary>형식 신원이 아니라 이름으로 판별한다(로드 컨텍스트가 달라도 인식하기 위해).</summary>
        private static bool IsCorners(object value)
        {
            return value != null && value.GetType().FullName == typeof(AdvCorners).FullName;
        }

        /// <summary>어느 컨텍스트의 AdvCorners든 네 반경 값을 리플렉션으로 읽는다.</summary>
        private static void Read(object value, out int tl, out int tr, out int br, out int bl)
        {
            var t = value.GetType();
            tl = (int)t.GetProperty("TopLeft").GetValue(value, null);
            tr = (int)t.GetProperty("TopRight").GetValue(value, null);
            br = (int)t.GetProperty("BottomRight").GetValue(value, null);
            bl = (int)t.GetProperty("BottomLeft").GetValue(value, null);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        /// <summary>
        /// InstanceDescriptor 변환이 가능함을 반드시 알린다. 기본 <see cref="TypeConverter.CanConvertTo"/>는
        /// string에만 true라, 이걸 오버라이드하지 않으면 디자이너가 ConvertTo(InstanceDescriptor)를
        /// 구현해 놨어도 물어보지 않고 Corners를 .resx로 직렬화한다(그 값은 재로드 시 다른 로드 컨텍스트에서
        /// 역직렬화돼 크래시). InstanceDescriptor로 가면 코드가 <c>new AdvCorners(...)</c>로 생성돼 안전하다.
        /// </summary>
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            // 이미 AdvCorners(어느 컨텍스트든)면 그대로 돌려준다.
            if (IsCorners(value)) return value;

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
            // 형식 신원 대신 이름으로 판별 + 리플렉션으로 값을 읽어, value가 다른 로드 컨텍스트의
            // AdvCorners여도(커스텀 디자이너로 인한 이중 로드) 캐스트 실패 없이 처리한다.
            if (IsCorners(value))
            {
                int tl, tr, br, bl;
                Read(value, out tl, out tr, out br, out bl);

                if (destinationType == typeof(string))
                {
                    string sep = (culture == null ? "," : culture.TextInfo.ListSeparator) + " ";
                    return tl + sep + tr + sep + br + sep + bl;
                }

                // 디자이너가 InitializeComponent에 생성자 호출로 써 넣게 한다.
                // 생성자는 value 자신의 형식에서 가져와 코드 생성이 그 컨텍스트를 그대로 따르게 한다.
                if (destinationType == typeof(InstanceDescriptor))
                {
                    var ctor = value.GetType().GetConstructor(IntCtorArgs);
                    if (ctor != null)
                        return new InstanceDescriptor(ctor, new object[] { tl, tr, br, bl });
                }
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
