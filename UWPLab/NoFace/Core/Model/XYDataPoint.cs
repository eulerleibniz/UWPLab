namespace NoFace.Core.Models
{
    using System.Globalization;

    public class XYDataPoint
    {
        public XYDataPoint(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }

        public double X { get; set; }

        public double Y { get; set; }

        public string ToolTip
        {
            get
            {
                CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
                string formatString = "F";
                string result = "X: " + this.X.ToString(formatString, culture) + "\n" + "Y: " + this.Y.ToString(formatString, culture);
                return result;
            }
        }
    }
}
