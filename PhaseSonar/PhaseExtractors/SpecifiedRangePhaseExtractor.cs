using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.Statistics;
using PhaseSonar.Maths;
using PhaseSonar.Utils;

namespace PhaseSonar.PhaseExtractors {
    public class SpecifiedRangePhaseExtractor : IPhaseExtractor {
        public enum DivisionResult {
            NoLeapPtsFound,
            AllIntervalTooShort,
            BestIntervalFound
        }

        private readonly int _end;
        private readonly double[] _linespace;
        private readonly double _maxPhaseStd;
        private readonly int _minFlatPhasePtsNumCnt;
        private readonly int _rangeLength;
        private readonly double[] _rangePhaseContainer;
        private readonly Complex[] _rangeSpecContainer;

        private readonly int _start;

        private Complex[] _fullComplexContainer;
        private double[] _halfDoubleContainer;

        /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
        public SpecifiedRangePhaseExtractor(int start, int end, int minFlatPhasePtsNumCnt, double maxPhaseStd) {
            _start = start;
            _end = end;
            _minFlatPhasePtsNumCnt = minFlatPhasePtsNumCnt;
            _maxPhaseStd = maxPhaseStd;
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
            if (correspondSpectrum == null) {
                if (_fullComplexContainer == null) {
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
            //Toolbox.WriteData(@"D:\zbf\temp2\full_spectrum.txt", spectrum.Select(complex => complex.MagnitudeSquared()).ToArray());

            RawSpectrumReady?.Invoke(_rangeSpecContainer);
            //Toolbox.WriteData(@"D:\zbf\temp2\range_spectrum.txt", _rangeSpecContainer.Select(complex => complex.MagnitudeSquared()).ToArray());

            _rangeSpecContainer.Phase(_rangePhaseContainer);
            //Toolbox.WriteData(@"D:\zbf\temp2\range_phase.txt", _rangePhaseContainer);

            Functions.UnwrapInPlace(_rangePhaseContainer);
            //Toolbox.WriteData(@"D:\zbf\temp2\range_phase_unwrap.txt", _rangePhaseContainer);

            RawPhaseReady?.Invoke(_rangePhaseContainer);
            Tuple<double, double> tuple;
            Tuple<double, int, int> bestZone;
            var result = TryGetSmoothestInterval(_rangePhaseContainer, out bestZone);
            switch (result) {
                case DivisionResult.BestIntervalFound:
                    ThrowIfStdTooLarge(bestZone.Item1, _maxPhaseStd);
                    var start = bestZone.Item2;
                    var end = bestZone.Item3;
                    var lineSpace = Functions.LineSpace(start + _start, end + _start);
                    var doubles =
                        _rangePhaseContainer.Where((d, i) => i >= start && i <= end).ToArray();
                    tuple = Fit.Line(lineSpace, doubles);
                    break;
                case DivisionResult.NoLeapPtsFound:
                    var standardDeviation = _rangePhaseContainer.AsEnumerable().StandardDeviation();
                    ThrowIfStdTooLarge(standardDeviation, _maxPhaseStd);
                    tuple = Fit.Line(_linespace, _rangePhaseContainer);
                    break;
                case DivisionResult.AllIntervalTooShort:
                default:
                    throw new PhaseFitException();
            }
//            tuple = Fit.Line(_linespace, _rangePhaseContainer);
            var slope = tuple.Item2;
//             if (Math.Abs(slope)>0.05) {
//                 throw new PhaseFitException();
//             }
            var intersect = tuple.Item1;

            for (var i = 0; i < _halfDoubleContainer.Length; i++) {
                _halfDoubleContainer[i] = intersect + slope*i;
            }
            //Toolbox.WriteData(@"D:\zbf\temp2\half_fit.txt", _halfDoubleContainer);

            return _halfDoubleContainer;
        }


        public event SpectrumReadyEventHandler RawSpectrumReady;
        public event PhaseReadyEventHandler RawPhaseReady;

        private static void ThrowIfStdTooLarge(double std, double threshold) {
            if (std > threshold) {
                throw new PhaseFitException();
            }
        }

        public DivisionResult TryGetSmoothestInterval(double[] phase, out Tuple<double, int, int> bestZone) {
            var last = phase[0];

            var leapPts = new List<int>();
            for (var i = 1; i < phase.Length; i++) {
                var curr = phase[i];
                var delta = curr - last;
                if (Math.Abs(delta) > Math.PI*0.7) {
                    leapPts.Add(i);
                }
                last = curr;
            }
            if (leapPts.IsEmpty()) {
                bestZone = null;
                return DivisionResult.NoLeapPtsFound;
            }

            var intervals = new List<Tuple<int, int>> {new Tuple<int, int>(0, leapPts.First() - 1)};
            for (var i = 0; i < leapPts.Count - 1; i++) {
                var start = leapPts[i];
                var end = leapPts[i + 1] - 1;
                intervals.Add(new Tuple<int, int>(start, end));
            }
            intervals.Add(new Tuple<int, int>(leapPts.Last(), phase.Length - 1));
            intervals.RemoveAll(tuple => tuple.Item2 - tuple.Item1 <= _minFlatPhasePtsNumCnt);
            if (intervals.IsEmpty()) {
                bestZone = null;
                return DivisionResult.AllIntervalTooShort;
            }
            var leastStd = new Tuple<double, int, int>(double.MaxValue, -1, -1);
            intervals.ForEach(tuple => {
                var enumerable = _rangePhaseContainer.Where((d, j) => j >= tuple.Item1 && j <= tuple.Item2);
                var standardDeviation = enumerable.StandardDeviation();
                if (standardDeviation <= leastStd.Item1) {
                    leastStd = new Tuple<double, int, int>(standardDeviation, tuple.Item1, tuple.Item2);
                }
            });
            bestZone = leastStd;
            return DivisionResult.BestIntervalFound;
        }
    }
}