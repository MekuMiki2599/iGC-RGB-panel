using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace iGCRGB.Converters
{
    /// <summary>把 (值, 最小, 最大, 轨道宽度) 换算成填充宽度。</summary>
    public class RatioToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 4) return 0d;
            double value, min, max, width;
            if (!TryDouble(values[0], out value)) return 0d;
            if (!TryDouble(values[1], out min)) return 0d;
            if (!TryDouble(values[2], out max)) return 0d;
            if (!TryDouble(values[3], out width)) return 0d;
            if (max <= min || width <= 0) return 0d;
            double ratio = (value - min) / (max - min);
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            return ratio * width;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static bool TryDouble(object value, out double result)
        {
            result = 0;
            if (value == null) return false;
            try
            {
                result = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
