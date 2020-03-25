using System;
using System.Linq;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;

using OpenCvSharp;

namespace SDKTemplate
{
    public class AlgorithmProperty : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Name of the parameter.
        private string parameterName;
        public string ParameterName
        {
            get { return this.parameterName; }
            set
            {
                this.parameterName = value;
                this.NotifyPropertyChanged("ParameterName");
            }
        }

        // Description of the parameter.
        private string description;
        public string Description
        {
            get { return this.description; }
            set
            {
                this.description = value;
                this.NotifyPropertyChanged("Description");
            }
        }

        // Value setting panel visibility
        private Visibility sliderVisibility;
        public Visibility SliderVisibility
        {
            get { return this.sliderVisibility; }
            set
            {
                this.sliderVisibility = value;
                this.NotifyPropertyChanged("SliderVisibility");
            }
        }

        // Value setting panel visibility
        private Visibility comboBoxVisibility;
        public Visibility ComboBoxVisibility
        {
            get { return this.comboBoxVisibility; }
            set
            {
                this.comboBoxVisibility = value;
                this.NotifyPropertyChanged("ComboBoxVisibility");
            }
        }

        // Value setting panel visibility
        private Visibility detailsVisibility;
        public Visibility DetailsVisibility
        {
            get { return this.detailsVisibility; }
            set
            {
                this.detailsVisibility = value;
                this.NotifyPropertyChanged("DetailsVisibility");
            }
        }

        // Current value of the parameter
        private double currentValue;
        public object CurrentValue
        {
            get
            {
                if (this.ParamType == typeof(int))
                {
                    return (int)this.currentValue;
                }

                if (this.ParamType == typeof(double))
                {
                    return this.currentValue;
                }

                if (this.ParamType == typeof(OpenCvSharp.Size))
                {
                    var res = new OpenCvSharp.Size((int)this.currentValue, (int)this.currentValue);
                    return res;
                }

                if (this.ParamType == typeof(Scalar))
                {
                    return (Scalar)this.currentValue;
                }

                if (this.ParamType == typeof(OpenCvSharp.Point))
                {
                    var res = new OpenCvSharp.Point(this.currentValue, this.currentValue);
                    return res;
                }

                if (this.ParamType?.BaseType == typeof(Enum))
                {
                    return this.ParamList[this.CurrentIntValue];
                }

                return this.currentValue;
            }
            set
            {
                this.currentValue = (double)value;
                this.CurrentDoubleValue = (double)value;
                this.CurrentStringValue = this.CurrentValue.ToString();

                if (this.ParamType?.BaseType == typeof(Enum))
                {
                    this.CurrentIntValue = Convert.ToInt32(value);
                }
                else
                {
                    this.CurrentIntValue = 0;
                }

                this.NotifyPropertyChanged("CurrentValue");
            }
        }

        private double currentDoubleValue;
        public double CurrentDoubleValue
        {
            get
            {
                return (double)this.currentDoubleValue;
            }
            set
            {
                this.currentDoubleValue = value;
                this.NotifyPropertyChanged("CurrentDoubleValue");
            }
        }

        private string currentStringValue;
        public string CurrentStringValue
        {
            set
            {
                this.currentStringValue = "Current Value = " + value.ToString();
                this.NotifyPropertyChanged("CurrentStringValue");
            }
            get
            {
                return this.currentStringValue;
            }
        }

        private int currentIntValue;
        public int CurrentIntValue
        {
            get
            {
                return this.currentIntValue;
            }
            set
            {
                this.currentIntValue = value;
                this.NotifyPropertyChanged("CurrentIntValue");
            }
        }

        // Maximum value of the parameter
        private double maxValue;
        public double MaxValue
        {
            get { return this.maxValue; }
            set
            {
                this.maxValue = value;
                this.NotifyPropertyChanged("MaxValue");
            }
        }

        // Minimum value of the parameter
        private double minValue;
        public double MinValue
        {
            get { return this.minValue; }
            set
            {
                this.minValue = value;
                this.NotifyPropertyChanged("MinValue");
            }
        }

        private bool isSliderEnable;
        public bool IsSliderEnable
        {
            get { return this.isSliderEnable; }
            set
            {
                this.isSliderEnable = value;
                this.NotifyPropertyChanged("IsSliderEnable");
            }
        }

        private bool isComboBoxEnable;
        public bool IsComboBoxEnable
        {
            get { return this.isComboBoxEnable; }
            set
            {
                this.isComboBoxEnable = value;
                this.NotifyPropertyChanged("IsComboBoxEnable");
            }
        }

        private string tag;
        public string Tag
        {
            get { return this.tag; }
            set
            {
                this.tag = value;
                this.NotifyPropertyChanged("Tag");
            }
        }

        private List<object> comboList;
        public List<object> ComboList
        {
            get { return this.comboList; }
            set
            {
                this.comboList = value;
                this.NotifyPropertyChanged("ComboList");
            }
        }

        private Type paramType;
        public Type ParamType
        {
            get { return this.paramType; }
            set
            {
                this.paramType = value;
                this.NotifyPropertyChanged("ParamType");
            }
        }

        private List<object> paramList;
        public List<object> ParamList
        {
            get { return this.paramList; }
            set
            {
                this.paramList = value;
                this.NotifyPropertyChanged("ParamList");
            }
        }
        // Converter

        // enum val
        public List<string> Selections;
        public int selectIndex;
        public bool isInitialize;

        public AlgorithmProperty(int index, Type type, string name, string description = "The default property description.", double max = 255, double min = 0, double cur = 0)
        {
            this.ParameterName = name;
            this.Description = description;
            this.MaxValue = max;
            this.MinValue = min;
            this.CurrentValue = cur > max ? max : cur < min ? min : cur;
            this.ParamType = type;

            if (type.BaseType != typeof(Enum))
            {
                this.ParamList = null;
                this.IsComboBoxEnable = false;
                this.isSliderEnable = true;
            }
            else
            {
                var _enumval = Enum.GetValues(type).Cast<object>();
                this.ParamList = _enumval.ToList();
                this.IsComboBoxEnable = true;
                this.isSliderEnable = false;
            }

            this.selectIndex = index;
            this.SliderVisibility = Visibility.Collapsed;
            this.ComboBoxVisibility = Visibility.Collapsed;
            this.DetailsVisibility = Visibility.Collapsed;
            this.isInitialize = false;
            this.Tag = name;
        }
        public AlgorithmProperty(string name, List<string> selections, string description = "The default property description.")
        {
            this.parameterName = name;
            this.description = description;
            this.Selections = selections;
            this.selectIndex = 0;
        }

        public void updateSelectIndex(int idx)
        {
            this.selectIndex = idx;
        }

        public void resetCurrentValue()
        {
            this.currentValue = (this.maxValue + this.minValue) / 2;
        }
    }
}
