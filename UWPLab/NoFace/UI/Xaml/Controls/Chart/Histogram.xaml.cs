// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace NoFace.UI.Xaml.Controls.Chart
{
    using MathNet.Numerics.Statistics;
    using NoFace.Core.Models;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Windows.UI.Xaml.Controls;
    using static System.Math;

    public enum BinLimitsMode
    {
        Auto,
        Manual,
    }

    /// <summary>
    /// Algorithm used for determining <see cref="BinWidth"/>  and <see cref="NumberOfBins"/>.
    /// </summary>
    public enum BinningAlgorithm
    {
        Auto,

        Manual,

        /// <summary>
        /// Scott's rule is optimal if the data is close to being normally distributed.
        ///
        /// This rule is appropriate for most other distributions, as well.
        ///
        /// It uses a bin width of:
        ///
        /// 3.5 * std(X(:)) * numel(X) ^ (-1/3).
        /// </summary>
        ScottRule,

        /// <summary>
        /// The Freedman-Diaconis rule is less sensitive to outliers in the data,
        /// and might be more suitable for data with heavy - tailed distributions.
        ///
        /// It uses a bin width of:
        ///
        /// 2 * IQR(X(:)) * numel(X) ^ (-1 / 3),
        ///
        /// where IQR is the interquartile range of X.
        /// </summary>
        FreedmanDiaconisRule,

        /// <summary>
        /// The integer rule is useful with integer data, as it creates a bin for each integer.
        ///
        /// It uses a bin width of 1 and places bin edges halfway between integers.
        ///
        /// To avoid accidentally creating too many bins, you can use this rule to create a limit of 65536 bins (2 ^ 16).
        ///
        /// If the data range is greater than 65536, then the integer rule uses wider bins instead.
        /// </summary>
        IntegerRule,

        /// <summary>
        /// Sturges' rule is popular due to its simplicity.
        ///
        /// It chooses the number of bins to be:
        ///
        /// ceil(1 + log2(numel(X)))
        /// </summary>
        SturgesRule,

        /// <summary>
        /// The Square Root rule is widely used in other software packages.
        ///
        /// It chooses the number of bins to be:
        ///
        /// ceil(sqrt(numel(X)))
        /// </summary>
        SquareRootRule,
    }

    /// <summary>
    /// Type of normalization, specified as one of the values in the enum. Normalization only effects <see cref="binValuesNormlized"/>.
    /// </summary>
    public enum NormalizationType
    {
        /// <summary>
        /// Default normalization scheme. The height of each bar is the number of observations in each bin. The sum of the bar heights is numel(X).
        /// For categorical histograms, the sum of the bar heights is either numel(X) or sum(ismember(X(:), Categories))
        /// </summary>
        Count,

        /// <summary>
        /// The height of each bar is the relative number of observations, (number of observations in bin / total number of observations). The sum of the bar heights is 1.
        /// For categorical histograms, the height of each bar is, (number of elements in category / total number of elements in all categories). The sum of the bar heights is 1.
        /// </summary>
        Probability,

        /// <summary>
        /// The height of each bar is, (number of observations in bin / width of bin). The area (height * width) of each bar is the number of observations in the bin. The sum of the bar areas is numel(X).
        /// For categorical histograms, this is the same as 'count'.
        /// </summary>
        CountDensity,

        /// <summary>
        /// Probability density function estimate. The height of each bar is, (number of observations in the bin) / (total number of observations * width of bin). The area of each bar is the relative number of observations. The sum of the bar areas is 1.
        /// For categorical histograms, this is the same as 'probability'.
        /// </summary>
        PDF,

        /// <summary>
        /// The height of each bar is the cumulative number of observations in each bin and all previous bins. The height of the last bar is numel(X).
        /// For categorical histograms, the height of each bar is equal to the cumulative number of elements in each category and all previous categories. The height of the last bar is numel(X) or sum(ismember(X(:), Categories))
        /// </summary>
        CummulativeCount,

        /// <summary>
        /// Cumulative density function estimate. The height of each bar is equal to the cumulative relative number of observations in the bin and all previous bins. The height of the last bar is 1.
        /// For categorical data, the height of each bar is equal to the cumulative relative number of observations in each category and all previous categories. The height of the last bar is 1.
        /// </summary>
        CDF,
    }

    public sealed partial class Histogram : UserControl, INotifyPropertyChanged
    {
        private List<double> data = new List<double>();
        private int numberOfBins;
        private BinningAlgorithm binningAlgorithm;
        private NormalizationType normalizationType;
        private double binWidth;
        private BinLimitsMode binLimitsMode;
        private List<double> binCenters;
        private List<double> binEdges;
        private List<double> binValuesNormlized;

        private ObservableCollection<XYDataPoint> chartSource = new ObservableCollection<XYDataPoint>();

        public Histogram()
        {
            this.InitializeComponent();

            // this.scatterLineSeries.XValueBinding = new PropertyNameDataPointBinding() { PropertyName = nameof(XYDataPoint.X) };
            // this.scatterLineSeries.YValueBinding = new PropertyNameDataPointBinding() { PropertyName = nameof(XYDataPoint.Y) };
            // this.scatterLineSeries.ItemsSource = this.chartSource;

            // this.data = series.Where(d => !(double.IsNaN(d) || double.IsInfinity(d))).ToList();
            // this.BinningAlgorithm = BinningAlgorithm.ScottRule; // Calculates everything
            // this.NormalizationType = NormalizationType.PDF; // Calculates everything
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int NumberOfBins
        {
            get => this.numberOfBins;

            set
            {
                this.numberOfBins = value;
                this.binningAlgorithm = BinningAlgorithm.Manual;
                this.UpperLimit = this.data.Max();
                this.LowerLimit = this.data.Min();
                this.binWidth = (this.UpperLimit - this.LowerLimit) / this.numberOfBins;
                this.RecalculateHistogramData();
            }
        }

        /// <summary>
        /// Gets or Sets Algorithm used for determining <see cref="BinWidth"/>  and <see cref="NumberOfBins"/>.
        /// </summary>
        public BinningAlgorithm BinningAlgorithm
        {
            get => this.binningAlgorithm;

            set
            {
                this.binningAlgorithm = value;
                if (value.Equals(BinningAlgorithm.Manual))
                {
                    return; // We should do Nothing
                }

                this.LowerLimit = this.data.Min();
                this.UpperLimit = this.data.Max();
                this.binWidth = this.CalculateBinWidth(this.data, this.binningAlgorithm, this.LowerLimit, this.UpperLimit);
                this.RecalculateHistogramData();
            }
        }

        /// <summary>
        /// Gets or Sets Type of Histogram Normalizion Which Effects <see cref="BinValuesNormlized"/>.
        /// </summary>
        public NormalizationType NormalizationType
        {
            get => this.normalizationType;

            set
            {
                this.normalizationType = value;
                this.RecalculateHistogramData();
            }
        }

        public IReadOnlyList<double> BinCenters => new ReadOnlyCollection<double>(this.binCenters);

        public IReadOnlyList<double> BinEdges => new ReadOnlyCollection<double>(this.binEdges);

        public ReadOnlyCollection<double> BinValuesNormlized => new ReadOnlyCollection<double>(this.binValuesNormlized);

        public double BinWidth
        {
            get => this.binWidth;

            set
            {
                this.binWidth = value;
                this.binningAlgorithm = BinningAlgorithm.Manual;
                this.binLimitsMode = Chart.BinLimitsMode.Manual;
                this.numberOfBins = (int)Ceiling((this.UpperLimit - this.LowerLimit) / this.binWidth);
                double extraLimitNeeded = (this.numberOfBins * this.binWidth) - (this.data.Max() - this.data.Min());
                this.UpperLimit = this.data.Max() + (extraLimitNeeded / 2);
                this.LowerLimit = this.data.Min() - (extraLimitNeeded / 2);
                this.RecalculateHistogramData();
            }
        }

        public BinLimitsMode BinLimitsMode
        {
            get => this.binLimitsMode;

            set
            {
                this.binLimitsMode = value;
                switch (value)
                {
                    case BinLimitsMode.Auto:

                        // TODO
                        this.BinWidth = this.binWidth; // Keeps binWidth as it is an recalculates everything, including but not limited to lowerLimit, upperLimit
                        break;

                    case Chart.BinLimitsMode.Manual:
                        // Do Nothing
                        break;

                    default:
                        break;
                }
            }
        }

        public double LowerLimit
        {
            get; set;
        }

        public double UpperLimit
        {
            get; set;
        }

        public static List<double> BasicHistogram(List<double> data, double min, double max, int nBins)
        {
            List<double> histogram = new List<double>(nBins);
            double binWidth = (max - min) / nBins;

            int nCounts = 0;
            for (int j = 0; j < data.Count; j++)
            {
                if (data[j] < min + binWidth)
                {
                    nCounts++;
                }
            }

            histogram[0] = nCounts;

            for (int i = 1; i < (nBins - 1); i++)
            {
                nCounts = 0;
                for (int j = 0; j < data.Count; j++)
                {
                    if (data[j] >= min + (i * binWidth) && data[j] < min + ((i + 1) * binWidth))
                    {
                        nCounts++;
                    }
                }

                histogram[i] = nCounts;
            }

            nCounts = 0;
            for (int j = 0; j < data.Count; j++)
            {
                if (data[j] >= min + ((nBins - 1) * binWidth))
                {
                    nCounts++;
                }
            }

            histogram[nBins - 1] = nCounts;

            return histogram;
        }

        public IReadOnlyList<double> GetData()
        {
            return this.data;
        }

        public void SetData(List<double> values)
        {
            this.data = values;
        }

        public bool AddData(double value)
        {
            this.data.Add(value);
            this.chartSource.Add(new XYDataPoint(this.chartSource.Count, value));
            return true;
        }

        public bool AddData(double[] values)
        {
            this.data.AddRange(values.ToList());
            foreach (var value in values)
            {
                this.chartSource.Add(new XYDataPoint(this.chartSource.Count, value));
            }

            return true;
        }

        public bool AddData(List<double> value)
        {
            this.data.AddRange(value);
            foreach (var item in value)
            {
                this.chartSource.Add(new XYDataPoint(this.chartSource.Count, item));
            }

            this.RecalculateHistogramData();
            return true;
        }

        private double CalculateBinWidth(List<double> data, BinningAlgorithm binningAlgorithm)
        {
            return this.CalculateBinWidth(data, binningAlgorithm, data.Min(), data.Max());
        }

        private double CalculateBinWidth(List<double> data, BinningAlgorithm binningAlgorithm, double lowerLimit, double upperLimit)
        {
            double binWidth = double.NaN;
            switch (binningAlgorithm)
            {
                case BinningAlgorithm.Auto:
                    binWidth = this.CalculateBinWidth(data, BinningAlgorithm.ScottRule, lowerLimit, upperLimit);
                    break;

                case BinningAlgorithm.Manual:
                    // throw new Exception(BinningAlgorithms.Manual.ToString() + " Can not Be Used for CalculateBinWidth");
                    break;

                case BinningAlgorithm.ScottRule:
                    {
                        // Scott's rule is optimal if the data is close to being normally distributed.
                        // This rule is appropriate for most other distributions, as well.
                        // It uses a bin width of:
                        // 3.5 * std(X(:)) * numel(X) ^ (-1/3).
                        binWidth = 3.5 * data.StandardDeviation() * Pow(data.Count, -1.0 / 3.0);
                        this.numberOfBins = (int)Ceiling((upperLimit - lowerLimit) / binWidth);
                    }

                    break;

                case BinningAlgorithm.FreedmanDiaconisRule:
                    {
                        // The Freedman-Diaconis rule is less sensitive to outliers in the data,
                        // and might be more suitable for data with heavy - tailed distributions.
                        // It uses a bin width of:
                        // 2 * IQR(X(:)) * numel(X) ^ (-1 / 3),
                        // where IQR is the interquartile range of X.
                        binWidth = 2 * data.InterquartileRange() * Pow(data.Count, -1.0 / 3.0);
                        this.numberOfBins = (int)Ceiling((upperLimit - lowerLimit) / binWidth);
                    }

                    break;

                case BinningAlgorithm.IntegerRule:
                    {
                        // The integer rule is useful with integer data,
                        // as it creates a bin for each integer.
                        // It uses a bin width of 1 and places bin edges halfway between integers.
                        // To avoid accidentally creating too many bins, you can use this rule to create a limit of 65536 bins (2 ^ 16).
                        // If the data range is greater than 65536,
                        // then the integer rule uses wider bins instead.
                        binWidth = 1.0;
                        this.numberOfBins = (int)Ceiling((upperLimit - lowerLimit) / binWidth);
                    }

                    break;

                case BinningAlgorithm.SturgesRule:
                    {
                        // Sturges' rule is popular due to its simplicity.
                        // It chooses the number of bins to be:
                        // ceil(1 + log2(numel(X)))
                        this.numberOfBins = (int)Ceiling(1 + Log(data.Count, 2.0));
                        binWidth = (upperLimit - lowerLimit) / this.numberOfBins;
                    }

                    break;

                case BinningAlgorithm.SquareRootRule:
                    {
                        // The Square Root rule is widely used in other software packages.
                        // It chooses the number of bins to be:
                        // ceil(sqrt(numel(X)))
                        this.numberOfBins = (int)Ceiling(Sqrt(data.Count));
                        binWidth = (upperLimit - lowerLimit) / this.numberOfBins;
                    }

                    break;

                default:
                    break;
            }

            return binWidth;
        }

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            this.OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RecalculateHistogramData()
        {
            if (this.data == null)
            {
                return;
            }

            this.binCenters = new List<double>(this.numberOfBins);
            this.binEdges = new List<double>(this.numberOfBins + 1);
            for (int i = 0; i < this.numberOfBins; i++)
            {
                this.binEdges[i] = this.LowerLimit + (i * this.binWidth);
                this.binCenters[i] = this.LowerLimit + (i * this.binWidth) + (this.binWidth / 2);
            }

            this.binEdges[this.numberOfBins] = this.UpperLimit;

            int dataCount = this.data.Count;
            List<double> basic_Histogram = BasicHistogram(this.data, this.LowerLimit, this.UpperLimit, this.numberOfBins);
            this.binValuesNormlized = new List<double>(dataCount);
            for (int i = 0; i < this.numberOfBins; i++)
            {
                switch (this.normalizationType)
                {
                    case NormalizationType.Count:
                        this.binValuesNormlized[i] = basic_Histogram[i];
                        break;

                    case NormalizationType.Probability:
                        this.binValuesNormlized[i] = basic_Histogram[i] / dataCount;
                        break;

                    case NormalizationType.CountDensity:
                        this.binValuesNormlized[i] = basic_Histogram[i] / this.binWidth;
                        break;

                    case NormalizationType.PDF:
                        this.binValuesNormlized[i] = basic_Histogram[i] / (dataCount * this.binWidth);
                        break;

                    case NormalizationType.CummulativeCount:
                        if (i == 0)
                        {
                            this.binValuesNormlized[i] = basic_Histogram[i];
                        }
                        else
                        {
                            this.binValuesNormlized[i] = basic_Histogram[i] + this.binValuesNormlized[i - 1];
                        }

                        break;

                    case NormalizationType.CDF:
                        if (i == 0)
                        {
                            this.binValuesNormlized[i] = basic_Histogram[i] / dataCount;
                        }
                        else
                        {
                            this.binValuesNormlized[i] = (basic_Histogram[i] / dataCount) + this.binValuesNormlized[i - 1];
                        }

                        break;

                    default:
                        break;
                }
            }
        }
    }
}
