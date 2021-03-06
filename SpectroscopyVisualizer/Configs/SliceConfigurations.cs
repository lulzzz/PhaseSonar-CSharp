﻿using System;
using System.Windows.Controls;

namespace SpectroscopyVisualizer.Configs {
    [Serializable]
    public class SliceConfigurations {
        private static SliceConfigurations _singleton;

        private SliceConfigurations(int peakMinLength, bool crestAtCenter, double crestAmplitudeThreshold,
            RulerType rulerType, bool autoAdjust, bool findAbs, int fixedLength, bool reference) {
            PeakMinLength = peakMinLength;
            CrestAtCenter = crestAtCenter;
            CrestAmplitudeThreshold = crestAmplitudeThreshold;
            RulerType = rulerType;
            AutoAdjust = autoAdjust;
            FindAbsoluteValue = findAbs;
            FixedLength = fixedLength;
            Reference = reference;
        }

        public int PeakMinLength { get; set; }
        public bool CrestAtCenter { get; set; }
        public double CrestAmplitudeThreshold { get; set; }
        public RulerType RulerType { get; set; }
        public bool AutoAdjust { get; set; }
        public bool FindAbsoluteValue { get; set; }
        public int FixedLength { get; set; }
        public bool Reference { get; set; }

        public static void Initialize(int peakMinLength, bool crestAtCenter, double crestAmplitudeThreshold,
            RulerType rulerType, bool autoAdjust, bool findAbs, int fixedLength, bool reference) {
            _singleton = new SliceConfigurations(peakMinLength, crestAtCenter, crestAmplitudeThreshold, rulerType,
                autoAdjust, findAbs, fixedLength, reference);
        }

        public static void Register(SliceConfigurations sliceConfigurations) {
            if (_singleton == null) {
                _singleton = sliceConfigurations;
            } else {
                ConfigsHolder.CopyTo(sliceConfigurations, _singleton);
            }
        }

        public static SliceConfigurations Get() {
            return _singleton;
        }

    }
}