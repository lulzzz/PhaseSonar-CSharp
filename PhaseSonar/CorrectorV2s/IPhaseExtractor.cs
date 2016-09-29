using System;
using System.Numerics;
using System.Windows.Forms;
using JetBrains.Annotations;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using PhaseSonar.Maths;
using PhaseSonar.Utils;

namespace PhaseSonar.CorrectorV2s {
    public interface IPhaseExtractor {
        [NotNull]
        double[] GetPhase([NotNull] double[] symmetryPulse,[CanBeNull] Complex[] correspondSpectrum);

        event SpectrumReadyEventHandler RawSpectrumReady;

        event PhaseReadyEventHandler RawPhaseReady;
    }

    public delegate void SpectrumReadyEventHandler(Complex[] spectrum);
    public delegate void PhaseReadyEventHandler(double[] phase);

    public class FourierOnlyPhaseExtractor : IPhaseExtractor {
        private readonly Rotator _rotator = new Rotator();
        private Complex[] _complexContainer;
        private double[] _phaseArray;


        public double[] GetPhase(double[] symmetryPulse, [CanBeNull] Complex[] correspondSpectrum) {
            if (_phaseArray == null) {
                _phaseArray = new double[symmetryPulse.Length/2];
            }

            Complex[] complexSpectrum;
            if (correspondSpectrum==null) {
                if (_complexContainer==null) {
                    _complexContainer= new Complex[symmetryPulse.Length];
                }
                Functions.ToComplexRotate(symmetryPulse, _complexContainer);
                Fourier.Forward(_complexContainer, FourierOptions.Matlab);
                complexSpectrum = _complexContainer;
            } else {
                complexSpectrum = correspondSpectrum;
            }

            RawSpectrumReady?.Invoke(complexSpectrum);

            //            symmetryPulse.ToComplex(_complexContainer);
            //            _rotator.Rotate(_complexContainer);

            // rotate & to complex

            for (var i = 0; i < _phaseArray.Length; i++) {
                _phaseArray[i] = complexSpectrum[i].Phase;
            }
            RawPhaseReady?.Invoke(_phaseArray);
            return _phaseArray;
        }

        public event SpectrumReadyEventHandler RawSpectrumReady;
        public event PhaseReadyEventHandler RawPhaseReady;
    }

    public class SpecifiedFreqRangePhaseExtractor : IPhaseExtractor {
        private readonly double _startFreqInM;
        private readonly double _endFreqInM;
        private readonly double _samplingRateInM;
        private SpecifiedRangePhaseExtractor _extractor;

        public SpecifiedFreqRangePhaseExtractor(double startFreqInM, double endFreqInM, double samplingRateInM) {
            _startFreqInM = startFreqInM;
            _endFreqInM = endFreqInM;
            _samplingRateInM = samplingRateInM;
        }

        [NotNull]
        private SpecifiedRangePhaseExtractor Init(int wholeFreqLength) {
            double factor = wholeFreqLength/_samplingRateInM;
            int startIndex = (int) (_startFreqInM*factor);
            int endIndex = (int) (_endFreqInM*factor);
            return new SpecifiedRangePhaseExtractor(startIndex,endIndex);
        }

        public double[] GetPhase(double[] symmetryPulse, Complex[] correspondSpectrum) {
            if (_extractor==null) {
                _extractor = Init(symmetryPulse.Length);
            }
            return _extractor.GetPhase(symmetryPulse,correspondSpectrum);
        }

        public event SpectrumReadyEventHandler RawSpectrumReady {
            add { _extractor.RawSpectrumReady += value; }
            remove { _extractor.RawSpectrumReady -= value; }
        }

        public event PhaseReadyEventHandler RawPhaseReady {
            add { _extractor.RawPhaseReady += value; }
            remove { _extractor.RawPhaseReady -= value; }
        }
    }
    public class SpecifiedRangePhaseExtractor : IPhaseExtractor {
        private readonly int _end;
        private readonly double[] _linespace;
        private readonly int _rangeLength;
        private readonly double[] _rangePhaseContainer;
        private readonly Complex[] _rangeSpecContainer;

        private readonly int _start;

        private Complex[] _fullComplexContainer;
        private double[] _halfDoubleContainer;

        /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
        public SpecifiedRangePhaseExtractor(int start, int end) {
            _start = start;
            _end = end;
            _rangeLength = end - start + 1;
            _linespace = Functions.LineSpace(start, end, _rangeLength);
            _rangeSpecContainer = new Complex[_rangeLength];
            _rangePhaseContainer = new double[_rangeLength];
        }

        public double[] GetPhase(double[] symmetryPulse, Complex[] correspondSpectrum) {
            if (_halfDoubleContainer == null || _fullComplexContainer == null) {
                _halfDoubleContainer = new double[symmetryPulse.Length/2];
                _fullComplexContainer = new Complex[symmetryPulse.Length];
            }

            Complex[] spectrum;
            if (correspondSpectrum==null) {
                if (_fullComplexContainer==null) {
                    _fullComplexContainer = new Complex[symmetryPulse.Length];
                }
                Functions.ToComplexRotate(symmetryPulse, _fullComplexContainer);
                _fullComplexContainer.FFT();
                spectrum = _fullComplexContainer;
            } else {
                spectrum = correspondSpectrum;
            }
            // todo including problem
            Array.Copy(spectrum, _start, _rangeSpecContainer, 0, _rangeLength);

            RawSpectrumReady?.Invoke(_rangeSpecContainer);

            _rangeSpecContainer.Phase(_rangePhaseContainer);
            Functions.UnwrapInPlace(_rangePhaseContainer);

            RawPhaseReady?.Invoke(_rangePhaseContainer);

            var fitFunc = Fit.LineFunc(_linespace, _rangePhaseContainer);
            for (var i = 0; i < _halfDoubleContainer.Length; i++) {
                _halfDoubleContainer[i] = fitFunc(i);
            }
            return _halfDoubleContainer;
        }

        public event SpectrumReadyEventHandler RawSpectrumReady;
        public event PhaseReadyEventHandler RawPhaseReady;
    }

    public class CentralInterpolationPhaseExtractor : IPhaseExtractor {
        [NotNull] private readonly IApodizer _apodizer;

        [NotNull] private readonly Complex[] _centerComplexContainer;

        private readonly int _centerHalfWidth;

        [NotNull] private readonly double[] _centerRealContainer;
        private readonly Func<Complex, double> _complexToPhaseFunc;

        [NotNull] private readonly Rotator _rotator = new Rotator();

        [CanBeNull] private Interpolator _interpolator;

        [CanBeNull] private double[] _phaseArray;

        /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
        public CentralInterpolationPhaseExtractor(IApodizer apodizer, int centerHalfWidth,
            Func<Complex, double> complexToPhaseFunc) {
            _apodizer = apodizer;
            _centerHalfWidth = centerHalfWidth;
            _complexToPhaseFunc = complexToPhaseFunc;
            var centerLength = centerHalfWidth*2;
            _centerRealContainer = new double[centerLength];
            _centerComplexContainer = new Complex[centerLength];
        }

        [NotNull]
        public double[] GetPhase(double[] symmetryPulse,Complex[] correspondSpectrum) {
            if (_interpolator == null || _phaseArray == null) {
                _interpolator = new Interpolator(_centerHalfWidth*2, symmetryPulse.Length);
                _phaseArray = new double[symmetryPulse.Length/2];
            }
            // extract central portion
            var centerBurst = symmetryPulse.Length/2;
            var centralPulse = _centerRealContainer;
            Array.Copy(symmetryPulse, centerBurst - _centerHalfWidth, centralPulse, 0, _centerHalfWidth*2);
            // apodize & rotate
            _apodizer.Apodize(centralPulse);
            _rotator.Rotate(centralPulse);
            // fft
            _centerRealContainer.ToComplex(_centerComplexContainer);
            Fourier.Forward(_centerComplexContainer, FourierOptions.Matlab);
//            Toolbox.WriteData(@"D:\zbf\temp\central_fft.txt", _centerComplexContainer);
            RawSpectrumReady?.Invoke(_centerComplexContainer);
            // get phase from spectrum
            var complexSpectrum = _centerComplexContainer;
            for (var i = 0; i < _centerRealContainer.Length; i++) {
                _centerRealContainer[i] = _complexToPhaseFunc(complexSpectrum[i]);
            }
            RawPhaseReady?.Invoke(_centerRealContainer);
//            Toolbox.WriteData(@"D:\zbf\temp\central_phase.txt", _centerRealContainer);
            // interpolate into full length
            _interpolator.Interpolate(_centerRealContainer, _phaseArray);
            return _phaseArray;
        }

        public event SpectrumReadyEventHandler RawSpectrumReady;
        public event PhaseReadyEventHandler RawPhaseReady;
    }

    public class ClassicWrongPhaseExtractor : IPhaseExtractor {
        private readonly CentralInterpolationPhaseExtractor _phaseExtractor;

        public ClassicWrongPhaseExtractor(IApodizer apodizer, int centerHalfWidth) {
            _phaseExtractor = new CentralInterpolationPhaseExtractor(apodizer, centerHalfWidth, Functions.Phase);
        }

        public double[] GetPhase(double[] symmetryPulse, Complex[] correspondSpectrum) {
            return _phaseExtractor.GetPhase(symmetryPulse,correspondSpectrum);
        }

        public event SpectrumReadyEventHandler RawSpectrumReady {
            add { _phaseExtractor.RawSpectrumReady += value; }
            remove { _phaseExtractor.RawSpectrumReady -= value; }
        }

        public event PhaseReadyEventHandler RawPhaseReady {
            add { _phaseExtractor.RawPhaseReady += value; }
            remove { _phaseExtractor.RawPhaseReady -= value; }
        }
    }

    public class CorrectCenterPhaseExtractor : IPhaseExtractor {
        private readonly CentralInterpolationPhaseExtractor _phaseExtractor;

        public CorrectCenterPhaseExtractor(IApodizer apodizer, int centerHalfWidth) {
            _phaseExtractor = new CentralInterpolationPhaseExtractor(apodizer, centerHalfWidth, complex => complex.Phase);
        }

        public double[] GetPhase(double[] symmetryPulse, Complex[] correspondSpectrum) {
            return _phaseExtractor.GetPhase(symmetryPulse, correspondSpectrum);
        }

        public event SpectrumReadyEventHandler RawSpectrumReady {
            add { _phaseExtractor.RawSpectrumReady += value; }
            remove { _phaseExtractor.RawSpectrumReady -= value; }
        }

        public event PhaseReadyEventHandler RawPhaseReady {
            add { _phaseExtractor.RawPhaseReady += value; }
            remove { _phaseExtractor.RawPhaseReady -= value; }
        }

    }
}