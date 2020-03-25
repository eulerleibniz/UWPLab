//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Controls.Primitives;

using OpenCvSharp;

namespace SDKTemplate
{
    /// <summary>
    /// Scenario that illustrates using OpenCV along with camera frames. 
    /// </summary>
    public sealed partial class Scenario1_ExampleOperations : Page
    {
        enum OperationType : int
        {
            Blur = 0,
            HoughLines,
            Contours,
            Canny,
            Histogram,
            MotionDetector
        }

        private const int ImageRows = 480;
        private const int ImageCols = 640;

        public AlgorithmProperty StoredProperty { get; set; }
        public AlgorithmProperty LastStoredProperty { get; set; }
        private Algorithm storeditem;

        private MainPage rootPage;

        private FrameRenderer previewRenderer = null;
        private FrameRenderer outputRenderer = null;

        private DispatcherTimer fpsTimer = null;
        private int frameCount = 0;

        OperationType currentOperation;
        private OcvOp operation = new OcvOp();

        // Rendering
        private MediaCapture mediaCapture = null;
        private MediaFrameReader reader = null;

        public Scenario1_ExampleOperations()
        {
            this.InitializeComponent();
            this.previewRenderer = new FrameRenderer(this.PreviewImage);
            this.outputRenderer = new FrameRenderer(this.OutputImage);

            this.fpsTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            this.fpsTimer.Tick += this.UpdateFPS;
        }

        /// <summary>
        /// Initializes the MediaCapture object with the given source group.
        /// </summary>
        /// <param name="sourceGroup">SourceGroup with which to initialize.</param>
        private async Task InitializeMediaCaptureAsync(MediaFrameSourceGroup sourceGroup)
        {
            if (this.mediaCapture != null)
            {
                return;
            }

            this.mediaCapture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings()
            {
                SourceGroup = sourceGroup,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };
            await this.mediaCapture.InitializeAsync(settings);
        }

        /// <summary>
        /// Unregisters FrameArrived event handlers, stops and disposes frame readers
        /// and disposes the MediaCapture object.
        /// </summary>
        private async Task CleanupMediaCaptureAsync()
        {
            if (this.mediaCapture != null)
            {
                await this.reader.StopAsync();
                this.reader.FrameArrived -= this.ColorFrameReader_FrameArrivedAsync;
                this.reader.Dispose();
                this.mediaCapture = null;
            }
        }

        private void UpdateFPS(object sender, object e)
        {
            var frameCount = Interlocked.Exchange(ref this.frameCount, 0);
            this.FPSMonitor.Text = "FPS: " + frameCount;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            App.dispatcher = this.Dispatcher;
            Cv2.InitContainer((object)App.container);
            this.rootPage = MainPage.Current;

            // setting up the combobox, and default operation
            this.OperationComboBox.ItemsSource = Enum.GetValues(typeof(OperationType));
            this.OperationComboBox.SelectedIndex = 0;
            this.currentOperation = OperationType.Blur;

            // Find the sources 
            var allGroups = await MediaFrameSourceGroup.FindAllAsync();
            var sourceGroups = allGroups.Select(g => new
            {
                Group = g,
                SourceInfo = g.SourceInfos.FirstOrDefault(i => i.SourceKind == MediaFrameSourceKind.Color)
            }).Where(g => g.SourceInfo != null).ToList();

            if (sourceGroups.Count == 0)
            {
                // No camera sources found
                return;
            }
            var selectedSource = sourceGroups.FirstOrDefault();

            // Initialize MediaCapture
            try
            {
                await this.InitializeMediaCaptureAsync(selectedSource.Group);
            }
            catch (Exception exception)
            {
                Debug.WriteLine("MediaCapture initialization error: " + exception.Message);
                await this.CleanupMediaCaptureAsync();
                return;
            }

            // Create the frame reader
            MediaFrameSource frameSource = this.mediaCapture.FrameSources[selectedSource.SourceInfo.Id];
            BitmapSize size = new BitmapSize() // Choose a lower resolution to make the image processing more performant
            {
                Height = ImageRows,
                Width = ImageCols
            };
            this.reader = await this.mediaCapture.CreateFrameReaderAsync(frameSource, MediaEncodingSubtypes.Bgra8, size);
            this.reader.FrameArrived += this.ColorFrameReader_FrameArrivedAsync;
            await this.reader.StartAsync();

            this.fpsTimer.Start();
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs args)
        {
            this.fpsTimer.Stop();
            await this.CleanupMediaCaptureAsync();
        }

        /// <summary>
        /// Handles a frame arrived event and renders the frame to the screen.
        /// </summary>
        private void ColorFrameReader_FrameArrivedAsync(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            var frame = sender.TryAcquireLatestFrame();

            if (frame != null)
            {
                SoftwareBitmap originalBitmap = null;
                var inputBitmap = frame.VideoMediaFrame?.SoftwareBitmap;

                if (inputBitmap != null)
                {
                    // The XAML Image control can only display images in BRGA8 format with premultiplied or no alpha
                    // The frame reader as configured in this sample gives BGRA8 with straight alpha, so need to convert it
                    originalBitmap = SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                    SoftwareBitmap outputBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, originalBitmap.PixelWidth, originalBitmap.PixelHeight, BitmapAlphaMode.Premultiplied);

                    // Operate on the image in the manner chosen by the user.
                    if (this.currentOperation == OperationType.Blur)
                    {
                        this.operation.Blur(originalBitmap, outputBitmap, this.storeditem);
                    }
                    else if (this.currentOperation == OperationType.HoughLines)
                    {
                        this.operation.HoughLines(originalBitmap, outputBitmap, this.storeditem);
                    }
                    else if (this.currentOperation == OperationType.Contours)
                    {
                        this.operation.Contours(originalBitmap, outputBitmap, this.storeditem);
                    }
                    else if (this.currentOperation == OperationType.Canny)
                    {
                        this.operation.Canny(originalBitmap, outputBitmap, this.storeditem);
                    }
                    else if (this.currentOperation == OperationType.MotionDetector)
                    {
                        this.operation.MotionDetector(originalBitmap, outputBitmap, this.storeditem);
                    }
                    else if (this.currentOperation == OperationType.Histogram)
                    {
                        // MP! Todo: Implement C# version in OcvOp.
                    }

                    // Display both the original bitmap and the processed bitmap.
                    this.previewRenderer.RenderFrame(originalBitmap);
                    this.outputRenderer.RenderFrame(outputBitmap);
                }

                Interlocked.Increment(ref this.frameCount);
            }
        }

        private async void OnOpComboBoxSelectionChanged(object sender, RoutedEventArgs e)
        {
            this.currentOperation = (OperationType)((sender as ComboBox).SelectedItem);

            if (OperationType.Contours != this.currentOperation)
                App.container.Visibility = Visibility.Collapsed;
            else
                App.container.Visibility = Visibility.Visible;

            if (OperationType.Blur == this.currentOperation)
            {
                this.CurrentOperationTextBlock.Text = "Current: Blur";
            }
            else if (OperationType.Contours == this.currentOperation)
            {
                this.CurrentOperationTextBlock.Text = "Current: Contours";
            }
            else if (OperationType.Histogram == this.currentOperation)
            {
                this.CurrentOperationTextBlock.Text = "Current: Histogram of RGB channels";
            }
            else if (OperationType.HoughLines == this.currentOperation)
            {
                this.CurrentOperationTextBlock.Text = "Current: Line detection";
            }
            else if (OperationType.Canny == this.currentOperation)
            {
                this.CurrentOperationTextBlock.Text = "Current: Canny";
            }
            else if (OperationType.MotionDetector == this.currentOperation)
            {
                this.CurrentOperationTextBlock.Text = "Current: Motion detection";
            }
            else
            {
                this.CurrentOperationTextBlock.Text = string.Empty;
            }

            this.rootPage.algorithms[this.OperationComboBox.SelectedIndex].ResetEnable();
            this.storeditem = this.rootPage.algorithms[this.OperationComboBox.SelectedIndex];
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                this.collection.ItemsSource = Algorithm.GetObjects(this.storeditem);
            });
        }

        private async void OnSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var slider = sender as Slider;

            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (this.storeditem != null && this.StoredProperty != null)
                {
                    if (slider.Tag.ToString() != this.StoredProperty.ParameterName) return;
                    if (this.StoredProperty.isInitialize)
                    {
                        if (slider.Value >= this.StoredProperty.MinValue && slider.Value <= this.StoredProperty.MaxValue)
                        {
                            this.StoredProperty.CurrentValue = slider.Value;
                            this.UpdateStoredAlgorithm(this.currentOperation, this.StoredProperty);
                        }
                    }
                    else
                    {
                        // initialize slider
                        this.collection.ItemsSource = Algorithm.GetObjects(this.storeditem);
                        this.StoredProperty.isInitialize = true;
                    }
                }
            });
        }

        private async void Collection_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Get the collection item corresponding to the clicked item.
            var container = this.collection.ContainerFromItem(e.ClickedItem) as ListViewItem;

            if (container != null)
            {
                // Stash the clicked item for use later. We'll need it when we connect back from the detailpage.
                this.StoredProperty = container.Content as AlgorithmProperty;
                this.storeditem.RevertEnable(this.StoredProperty.ParameterName);
                await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    this.collection.ItemsSource = Algorithm.GetObjects(this.storeditem);
                });
            }
        }

        private void UpdateStoredAlgorithm(OperationType operationType, AlgorithmProperty algorithmProperty)
        {
            if (OperationType.Blur == operationType)
            {
                if (algorithmProperty.ParameterName == "Ksize")
                {
                    this.storeditem.UpdateCurrentValue(algorithmProperty);
                    this.storeditem.UpdateProperty("Anchor", AlgorithmPropertyType.maxValue, algorithmProperty.CurrentDoubleValue);
                }
                else
                {
                    this.storeditem.UpdateCurrentValue(algorithmProperty);
                }
            }
            else if (OperationType.Contours == this.currentOperation)
            {
                this.storeditem.UpdateCurrentValue(algorithmProperty);
            }
            else if (OperationType.Histogram == this.currentOperation)
            {
                this.storeditem.UpdateCurrentValue(algorithmProperty);
            }
            else if (OperationType.HoughLines == this.currentOperation)
            {
                this.storeditem.UpdateCurrentValue(algorithmProperty);
            }
            else if (OperationType.Canny == this.currentOperation)
            {
                this.storeditem.UpdateCurrentValue(algorithmProperty);
            }
            else if (OperationType.MotionDetector == this.currentOperation)
            {
                this.storeditem.UpdateCurrentValue(algorithmProperty);
            }
        }

        /// <summary>
        /// Writes the given object instance to a binary file.
        /// <para>Object type (and all child types) must be decorated with the [Serializable] attribute.</para>
        /// <para>To prevent a variable from being serialized, decorate it with the [NonSerialized] attribute; cannot be applied to properties.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the XML file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the XML file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
        {
            using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        /// <summary>
        /// Reads an object instance from a binary file.
        /// </summary>
        /// <typeparam name="T">The type of object to read from the XML.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the binary file.</returns>
        public static T ReadFromBinaryFile<T>(string filePath)
        {
            using (Stream stream = File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }

        private async void ToggleButton_Click(object sender, RoutedEventArgs e)
        {

            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (this.collection.Visibility == Visibility.Collapsed)
                {
                    this.Setting.Glyph = "\uE751";
                    this.collection.Visibility = Visibility.Visible;
                }
                else
                {
                    this.Setting.Glyph = "\uE713";
                    this.collection.Visibility = Visibility.Collapsed;
                }
            });
        }

        private async void OnComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.storeditem != null && this.StoredProperty != null)
            {
                if (this.StoredProperty.isInitialize)
                {
                    var combo = sender as ComboBox;
                    var selectIdx = combo.SelectedIndex;
                    if (combo.Tag?.ToString() != this.StoredProperty.ParameterName) return;
                    this.StoredProperty.CurrentValue = (double)selectIdx;
                    this.UpdateStoredAlgorithm(this.currentOperation, this.StoredProperty);
                }
                else
                {
                    await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        // initialize slider
                        this.collection.ItemsSource = Algorithm.GetObjects(this.storeditem);
                        this.StoredProperty.isInitialize = true;
                    });
                }
            }
        }
    }
}
