﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using JetBrains.Annotations;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using NationalInstruments.Restricted;
using PhaseSonar.Analyzers;
using PhaseSonar.Utils;
using SpectroscopyVisualizer.Configs;
using SpectroscopyVisualizer.Consumers;
using SpectroscopyVisualizer.Factories;
using SpectroscopyVisualizer.Presenters;
using SpectroscopyVisualizer.Producers;

namespace SpectroscopyVisualizer {
    /// <summary>
    ///     Interaction logic for MainWindow.xaml, also the entrance of the whole program.
    /// </summary>
    public partial class MainWindow : Window {
//        private readonly CanvasView _canvasView;

        private bool _ultraFastMode;

        public MainWindow() {
            // init system components
            InitializeComponent();

            if (File.Exists(@"default.svcfg")) {
                ConfigsHolder.Load(@"default.svcfg");
            } else {
                // init configurations
                SamplingConfigurations.Initialize(
                    deviceName: "Dev3",
                    channel: 0,
                    samplingRateInMHz: 100,
                    recordLengthInM: 1,
                    range: 10);

                GeneralConfigurations.Initialize(
                    repetitionRate: 400,
                    threadNum: 4,
                    dispPoints: 1000,
                    directory: @"C:\buffer\captured\",
                    viewPhase: false,
                    saveType: SaveType.Magnitude,
                    queueSize: 48,
                    saveSample:false,
                    saveSpec:false,
                    saveAcc:false,
                    operationMode:OperationMode.Manual,
                    targetCnt:100);

                SliceConfigurations.Initialize(
                    crestAmplitudeThreshold: 1,
                    pointsBeforeCrest: 1000,
                    crestAtCenter: true,
                    rulerType: RulerType.MinLength,
                    findAbs: true,
                    autoAdjust: false,
                    fixedLength: 232171,
                    reference: false
                    );

                CorrectorConfigurations.Initialize(
                    zeroFillFactor: 1,
                    centerSpanLength: 512,
                    correctorType: CorrectorType.Mertz,
                    apodizerType: ApodizerType.Hann,
                    phaseType: PhaseType.FullRange,
                    autoFlip: false,
                    realPhase: false,
                    rangeStart: 18000,
                    rangeEnd: 20000);
            }

            CbPhaseType.SelectionChanged += (sender, args) => {
                HideAllPhaseOptions();

                var selected = (PhaseType) args.AddedItems[0];
                HandleAdditionalPhaseOptions(selected);
            };

            CbSliceLength.SelectionChanged += (sender, args) => {
                var selected = (RulerType) args.AddedItems[0];
                TbFixedLength.Visibility = selected == RulerType.FixLength ? Visibility.Visible : Visibility.Hidden;
            };

            CbCorrector.SelectionChanged += (sender, args) => {
                var selected = (CorrectorType) args.AddedItems[0];
                switch (selected) {
                    case CorrectorType.Fake:
                        Hide(CbPhaseType);
                        Hide(LbPhaseType);
                        HideAllPhaseOptions();
                        break;
                    case CorrectorType.Mertz:
                        Show(CbPhaseType);
                        Show(LbPhaseType);
                        HandleAdditionalPhaseOptions((PhaseType?) CbPhaseType.SelectedItem ?? PhaseType.FullRange);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            };
            CbOperationMode.SelectionChanged += (sender, args) => {
                var selected = (OperationMode) args.AddedItems[0];
                switch (selected) {
                    case OperationMode.Manual:
                        Hide(TbTargetCnt);
                        break;
                    case OperationMode.Single:
                    case OperationMode.Loop:
                        Show(TbTargetCnt);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            };
            RoutedEventHandler ckPhaseOnChecked = (sender, args) => {
                var correct = !CkPhase.IsChecked.GetValueOrDefault(false);
                if (correct) {
                    Show(LbCorrector);
                    Show(CbCorrector);
                    Show(CbApodizationType);
                    Show(LbApodize);
                    Show(CkAutoFlip);
                    Show(CkSpecReal);
                    Show(CbPhaseType);
                    Show(LbPhaseType);
                    HandleAdditionalPhaseOptions((PhaseType?) CbPhaseType.SelectedItem ?? PhaseType.FullRange);
                } else {
                    Hide(CbCorrector);
                    Hide(LbCorrector);
                    Hide(CbApodizationType);
                    Hide(LbApodize);
                    Hide(CkAutoFlip);
                    Hide(CkSpecReal);
                    Hide(CbPhaseType);
                    Hide(LbPhaseType);
                    HideAllPhaseOptions();
                }
            };
            CkPhase.Checked += ckPhaseOnChecked;
            CkPhase.Unchecked += ckPhaseOnChecked;


            //            CorrectorConfigs.Register(Toolbox.DeserializeData<CorrectorConfigs>(@"D:\\configuration.bin"));
            // bind configs to controls
            SamplingConfigurations.Get().Bind(TbDeviceName, TbChannel, TbSamplingRate, TbRecordLength, TbRange);
            GeneralConfigurations.Get().Bind(TbRepRate, TbThreadNum, TbDispPoints, TbSavePath, CkPhase, CbSaveType, TbQueueSize, CkCaptureSample, CkCaptureSpec, CkCaptureAcc, CbOperationMode, TbTargetCnt);
            SliceConfigurations.Get().Bind(TbPtsBeforeCrest, TbCrestMinAmp, CbSliceLength, CkAutoAdjust, CkFindAbs, TbFixedLength, CkRef);
            CorrectorConfigurations.Get().Bind(TbZeroFillFactor, TbCenterSpanLength, CbCorrector, CbApodizationType, CbPhaseType, TbRangeStart, TbRangeEnd, CkAutoFlip, CkSpecReal);
            // init custom components
//            _canvasView = new CanvasView(ScopeCanvas);
//            HorizontalAxisView = new HorizontalAxisView(HorAxisCanvas);
//            VerticalAxisView = new VerticalAxisView(VerAxisCanvas);


            SwitchButton = new ToggleButtonV2(ToggleButton, false, "STOP", "START");
            SwitchButton.TurnOn += TurnOn;
            SwitchButton.TurnOff += ClearFromRunningState;
            SizeChanged += (sender, args) => { Adapter?.OnWindowZoomed(); };
            // todo text disapeared

            CkCaptureSpec.Checked += (sender, args) => { CkCaptureAcc.IsChecked = true; };
        }


        private ToggleButtonV2 SwitchButton { get; }

        [CanBeNull]
        public DisplayAdapter Adapter { get; set; }

        [NotNull]
        public IScheduler Scheduler { get; private set; } = new EmptyScheduler();

        private void HandleAdditionalPhaseOptions(PhaseType selected) {
            switch (selected) {
                case PhaseType.FullRange:
                    break;
                case PhaseType.CenterInterpolation:
                case PhaseType.OldCenterInterpolation:
                    Show(TbCenterSpanLength);
                    Show(LbCentralSpan);
                    break;
                case PhaseType.SpecifiedRange:
                case PhaseType.SpecifiedFreqRange:
                    Show(LbRangeStart);
                    Show(LbRangeEnd);
                    Show(TbRangeStart);
                    Show(TbRangeEnd);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        private void HideAllPhaseOptions() {
            Hide(TbCenterSpanLength);
            Hide(LbCentralSpan);
            Hide(TbRangeStart);
            Hide(LbRangeStart);
            Hide(TbRangeEnd);
            Hide(LbRangeEnd);
        }

        private static void Hide(Control control) {
            control.Visibility = Visibility.Hidden;
        }

        private static void Show(Control control) {
            control.Visibility = Visibility.Visible;
        }


        private void ToggleButton_OnClick(object sender, RoutedEventArgs routedEventArgs) {
            SwitchButton.State = !SwitchButton.State;
        }

        private void TurnOn() {
            GC.Collect();
            if (_ultraFastMode) {
                var textBlock = new TextBlock {
                    Text = "Happy 2016!", Foreground = new SolidColorBrush(Colors.Wheat), FontSize = 30
                };
                var canvasView = new CanvasView(ScopeCanvas);
                Canvas.SetTop(textBlock, canvasView.ScopeHeight/2);
                Canvas.SetLeft(textBlock, canvasView.ScopeWidth/3);
                canvasView.Canvas.Children.Add(textBlock);
                SwitchButton.State = false;
                return;
            }

            var factory = FactoryHolder.Get();
            IProducerV2<SampleRecord> producer;
            if (!factory.TryNewSampleProducer(out producer)) {
                SwitchButton.State = false;
                MessageBox.Show("Sampler can't be initialized");
                return;
            }
            producer.ProductionFailed += () => {
                Dispatcher.InvokeAsync(() => {
                    SwitchButton.State = false;
                    MessageBox.Show("Unable to sample data. Maybe out of Memory.");
                });
            };
            Adapter = NewAdapter();
            int? targetCnt;
            var configs = GeneralConfigurations.Get();
            if (configs.OperationMode == OperationMode.Manual) {
                targetCnt = null;
            } else {
                targetCnt = configs.TargetCnt;
            }
            var consumer = factory.NewConsumer(producer, Adapter, targetCnt);
            try {
                Adapter.StartFreqInMHz = Convert.ToDouble(TbStartFreq.Text); // todo move to constructor
                Adapter.EndFreqInMHz = Convert.ToDouble(TbEndFreq.Text);
            } catch (Exception) {
            }
            Scheduler = new Scheduler(producer, consumer);
            consumer.SourceInvalid += ConsumerOnSourceInvalid;
            consumer.ElementConsumedSuccessfully += () => {
                ConsumerOnConsumeEvent(consumer, Scheduler.Watch);
            };
            switch (configs.OperationMode) {
                case OperationMode.Manual:
                    PbLoading.IsIndeterminate = true;

                    break;
                case OperationMode.Single:
                    consumer.TargetAmountReached += OnConsumerStopped;
                    PbLoading.IsIndeterminate = false;
                    PbLoading.Maximum = targetCnt.Value;
                    break;
                case OperationMode.Loop:
                    PbLoading.IsIndeterminate = false;
                    PbLoading.Maximum = targetCnt.Value;
                    consumer.TargetAmountReached += () => {
                        OnConsumerStopped();

                        Dispatcher.Invoke(() => {
                            SwitchButton.State = true;
                        });
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            TbStartFreq.DataContext = Adapter;
            TbEndFreq.DataContext = Adapter;
            Scheduler.Start();
        }

        [NotNull]
        private DisplayAdapter NewAdapter() {
            return FactoryHolder.Get().NewAdapter(new CanvasView(ScopeCanvas), new HorizontalAxisView(HorAxisCanvas), new VerticalAxisView(VerAxisCanvas), TbXCoordinate, TbDistance);
        }

        private void ConsumerOnConsumeEvent(IConsumerV2 consumer, StopWatch watch) {
            var sizeInM = SamplingConfigurations.Get().RecordLength/1e6;
            Application.Current.Dispatcher.InvokeAsync(() => {
                var consumedCnt = consumer.ConsumedCnt;
                var elapsedSeconds = watch.ElapsedSeconds();
                var speed = consumedCnt*sizeInM/elapsedSeconds;
                TbConsumerSpeed.Text = speed.ToString("F3");
                TbTotalData.Text = (consumedCnt*sizeInM).ToString();
                if (PbLoading.Maximum != 12345) {
                    PbLoading.Value += 1;
                }
            });
        }


        private void ConsumerOnSourceInvalid() {
            Dispatcher.Invoke(() => { SwitchButton.State = false; });
            MessageBox.Show("It seems that the source is invalid.");
        }

        private void ClearFromRunningState() {
            Scheduler.Stop();
            PbLoading.IsIndeterminate = false;
            if (PbLoading.Value < PbLoading.Maximum) {
                PbLoading.Value += 1;
            }
            PbLoading.Value = 0;
            PbLoading.Maximum = 12345;
        }


        private void BnPath_Click(object sender, RoutedEventArgs e) {
            var dialog = new CommonOpenFileDialog {IsFolderPicker = true};
            var result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok) {
                foreach (var fileName in dialog.FileNames) {
                    TbSavePath.Text = fileName;
                }
            }
            GeneralConfigurations.Get().Directory = TbSavePath.Text;
        }


        private void DecodeFiles_OnClick(object sender, RoutedEventArgs e) {
            var files = SelectFiles();
            PbLoading.Maximum = files.Length;
            PbLoading.Value = 0;
            if (!files.IsEmpty()) {
                Task.Run(() => {
                    files.ForEach(path => {
                        var deserializeData = Toolbox.DeserializeData<double[]>(path);
                        Toolbox.WriteData(path.Replace("Binary", "Decoded"), deserializeData);
                        PbLoading.Dispatcher.InvokeAsync(() => { PbLoading.Value += 1; });
                    });
                    MessageBox.Show("decoding finished");
                });
            }
        }

        private static string[] SelectFiles() {
            // Create OpenFileDialog 
            var dlg = new OpenFileDialog {
                DefaultExt = ".txt", Filter = "Text documents (.txt)|*.txt", Multiselect = true
            };

            // Set filter for file extension and default file extension 


            // Display OpenFileDialog by calling ShowDialog method 
            var result = dlg.ShowDialog();
            if (result == true) {
                return dlg.FileNames;
            }
            return new string[0];
        }

        private static string SelectFile() {
            // Create OpenFileDialog 
            var dlg = new OpenFileDialog {
                DefaultExt = ".txt", Filter = "Text documents (.txt)|*.txt", Multiselect = false
            };

            // Set filter for file extension and default file extension 


            // Display OpenFileDialog by calling ShowDialog method 
            var result = dlg.ShowDialog();
            if (result == true) {
                return dlg.FileNames[0];
            }
            return null;
        }

        private void LoadCompressedFiles_OnClick(object sender, RoutedEventArgs e) {
            LoadFiles(true);
        }

        private bool IsProgramRunning() {
            if (SwitchButton.State) {
                MessageBox.Show("Command rejected! Plz terminate current processing first.");
            }
            return SwitchButton.State;
        }

        private void LoadFiles(bool compressed) {
            if (IsProgramRunning()) {
                return;
            }
            var fileNames = SelectFiles();
            if (fileNames.IsEmpty()) return;
            GeneralConfigurations.Get().Directory = Path.GetDirectoryName(fileNames[0]) + @"\";
            TbSavePath.Text = GeneralConfigurations.Get().Directory;
            var factory = FactoryHolder.Get();
            var producer = factory.NewProducer(fileNames, compressed);
            Adapter = NewAdapter();
            var consumer = factory.NewConsumer(producer, Adapter, fileNames.Length);
            consumer.SourceInvalid += ConsumerOnSourceInvalid;
            consumer.ElementConsumedSuccessfully += () => { ConsumerOnConsumeEvent(consumer, Scheduler.Watch); };
            consumer.ProducerEmpty += OnConsumerStopped;
            consumer.TargetAmountReached += OnConsumerStopped;
            PbLoading.Maximum = fileNames.Length;
            PbLoading.Value = 0;
            Scheduler = new Scheduler(producer, consumer);
            Scheduler.Start();
            SetButtonRunning();
        }

        private void OnConsumerStopped() {
            Dispatcher.Invoke(() => { SwitchButton.State = false; });
        }

        private void About_OnClick(object sender, RoutedEventArgs e) {
            MessageBox.Show("A 2016 ST Workshop Production. All Rights Reserved.");
        }

        private void StartSample_OnClick(object sender, RoutedEventArgs e) {
            if (IsProgramRunning()) {
                return;
            }
            var numberDialog = new NumberDialog();
            if (!numberDialog.ShowDialog().GetValueOrDefault(false)) {
                return;
            }
            var total = numberDialog.Number;
            if (total <= 0) {
                MessageBox.Show("plz input the number of records to be sampled.");
                return;
            }

            var factory = FactoryHolder.Get();

            IProducerV2<SampleRecord> producer;
            if (!factory.TryNewSampleProducer(out producer, total)) {
                SwitchButton.State = false;
                MessageBox.Show("Sampler can't be initialized");
                return;
            }
//            producer.HitTarget += () => { Dispatcher.InvokeAsync(() => { SwitchButton.State = false; }); };
            PbLoading.Maximum = total;
            Adapter = NewAdapter();
            var threadNum = GeneralConfigurations.Get().ThreadNum;
            var workers = new List<SpecialSampleWriter>(threadNum);
            for (var i = 0; i < threadNum; i++) {
                workers.Add(new SpecialSampleWriter(GeneralConfigurations.Get().Directory, "[Binary]"));
            }
            var consumer = new DataSerializer(producer.BlockingQueue, workers, total);
            consumer.SourceInvalid += ConsumerOnSourceInvalid;
            consumer.ElementConsumedSuccessfully += () => { ConsumerOnConsumeEvent(consumer, Scheduler.Watch); };
            consumer.TargetAmountReached += () => { Dispatcher.InvokeAsync(() => { SwitchButton.State = false; }); };
            CkCaptureSample.IsChecked = true;
            Scheduler = new Scheduler(producer, consumer);
            Scheduler.Start();
            SetButtonRunning();
        }

        private void SetButtonRunning() {
            SwitchButton.TurnOn -= TurnOn;
            SwitchButton.State = true;
            SwitchButton.TurnOn += TurnOn;
        }

        private void LoadDataFiles_OnClick(object sender, RoutedEventArgs e) {
            LoadFiles(false);
        }

        private void DebugCmd_OnClick(object sender, RoutedEventArgs e) {
            if (IsProgramRunning()) {
                return;
            }
            var fileNames = SelectFiles();
            if (fileNames.IsEmpty()) return;

            GeneralConfigurations.Get().Directory = Path.GetDirectoryName(fileNames[0]) + @"\";
            TbSavePath.Text = GeneralConfigurations.Get().Directory;
            var factory = FactoryHolder.Get();
            var producer = factory.NewProducer(fileNames, true);
            Adapter = NewAdapter();

            var checkers = new List<PulseChecker>();
            for (var i = 0; i < 4; i++) {
                checkers.Add(new PulseChecker(factory.NewCrestFinder(), factory.NewSlicer(), factory.NewPulsePreprocessor(), factory.NewCorrector()));
            }
            var consumer = new PulseByPulseChecker(producer.BlockingQueue, checkers, fileNames.Length);
            consumer.ElementConsumedSuccessfully += () => { PbLoading.Dispatcher.InvokeAsync(() => { PbLoading.Value += 1; }); };
            PbLoading.Maximum = fileNames.Length;
            PbLoading.Value = 0;
            Scheduler = new Scheduler(producer, consumer);
            Scheduler.Start();
            SetButtonRunning();
        }

        private void LoadConfig_OnClick(object sender, RoutedEventArgs e) {
            ConfigsHolder.Load();
        }

        private void SaveConfig_OnClick(object sender, RoutedEventArgs e) {
            new ConfigsHolder().Dump();
        }

        private void SetConfigAsDef_OnClick(object sender, RoutedEventArgs e) {
            new ConfigsHolder().Dump("default.svcfg");
            MessageBox.Show("Default configuration set successfully");
        }

        private void Donate_OnClick(object sender, RoutedEventArgs e) {
            MessageBox.Show("If you think this app is valuable, plz pay $10 USD to the author. \n\nYour support is very important! Thanks!");
        }

        private void ReportBug_OnClick(object sender, RoutedEventArgs e) {
            var issue = @"https://github.com/SphiaTower/PhaseSonar-CSharp/issues";
            Process.Start(issue);
        }

        private void ContactAuthor_OnClick(object sender, RoutedEventArgs e) {
            var address = @"mailto:traspip@126.com";
            Process.Start(address);
        }

        private void UltraFast_OnChecked(object sender, RoutedEventArgs e) {
            _ultraFastMode = CkUltraFast.IsChecked;
        }

        private void CkUltraFast_OnUnchecked(object sender, RoutedEventArgs e) {
            _ultraFastMode = CkUltraFast.IsChecked;
        }

        private void GenerateWavelengthAxis_OnClick(object sender, RoutedEventArgs e) {
            var file = SelectFile();
            if (file != null) {
                Process.Start(@"C:\Anaconda3\python.exe", @"C:\Users\admin\PycharmProjects\PhaseSonar2\Tools\Mapper.py " + file);
            }
        }

        private void FlattenCurves_OnClick(object sender, RoutedEventArgs e) {
            var file = SelectFile();
            if (file == null) {
                return;
            }
            if (File.Exists(file.Replace(".txt", "[WavelengthAxis].txt"))) {
                Process.Start(@"C:\Anaconda3\python.exe", @"C:\Users\admin\PycharmProjects\PhaseSonar2\Tools\Flatter.py " + file);
            } else {
                Process.Start(@"C:\Anaconda3\python.exe", @"C:\Users\admin\PycharmProjects\PhaseSonar2\Tools\Mapper.py " + file);
                MessageBox.Show("Generating wavelength axis, please click 'OK' AFTER completion.");
                Process.Start(@"C:\Anaconda3\python.exe", @"C:\Users\admin\PycharmProjects\PhaseSonar2\Tools\Flatter.py " + file);
            }
        }

        private void Exit_OnClick(object sender, RoutedEventArgs e) {
            Close();
        }

        private void BnOpenPath_OnClick(object sender, RoutedEventArgs e) {
            Process.Start(GeneralConfigurations.Get().Directory);
        }
    }
}