﻿using System;
using System.Collections.Generic;
using System.Numerics;
using JetBrains.Annotations;
using PhaseSonar.Analyzers.WithoutReference;
using PhaseSonar.Correctors;
using PhaseSonar.CorrectorV2s;
using PhaseSonar.CorrectorV2s.PulsePreprocessors;
using PhaseSonar.CrestFinders;
using PhaseSonar.Maths;
using PhaseSonar.PhaseExtractors;
using PhaseSonar.Slicers;
using PhaseSonar.Slicers.RefSlicers;
using PhaseSonar.Utils;

namespace PhaseSonar.Analyzers.WithReference {
    public class Splitter : ISplitter {
        [NotNull] private readonly ICorrectorV2 _corrector;

        [NotNull] private readonly ICrestFinder _finder;

        [NotNull] private readonly IPulsePreprocessor _preprocessor;

        [NotNull] private readonly Rotator _rotator = new Rotator();

        [NotNull] private readonly IRefSlicer _slicer;

        /// <summary>
        ///     Create an accumulator
        /// </summary>
        /// <param name="finder">A finder</param>
        /// <param name="slicer">A slicer</param>
        /// <param name="preprocessor"></param>
        /// <param name="corrector">A corrector</param>
        public Splitter([NotNull] ICrestFinder finder, IRefSlicer slicer, IPulsePreprocessor preprocessor,
            ICorrectorV2 corrector) {
            _preprocessor = preprocessor;
            _corrector = corrector;
            _finder = finder;
            _slicer = slicer;
        }

        /// <summary>
        ///     Process the pulse sequence with reference signals and accumulate results of all pulses of gas and reference respectively
        /// </summary>
        /// <param name="pulseSequence">A pulse sequence, containing reference pulses</param>
        /// <returns>The result</returns>
        [NotNull]
        public SplitResult Process([NotNull] double[] pulseSequence) {
            var crestIndices = _finder.Find(pulseSequence);
            if (crestIndices.Count <= 1) {
                return SplitResult.FromException(ProcessException.NoPeakFound);
            }
            Duo<List<SliceInfo>> sliceInfos;
            try {
                sliceInfos = _slicer.Slice(pulseSequence, crestIndices);
            } catch (Exception) {
                return SplitResult.FromException(ProcessException.NoSliceValid);
            }
            var errorCnt = 0;
            var spectra = new Spectrum[2];
            for (var i = 0; i < 2; i++) {
                var list = sliceInfos[i];
                Complex[] accumulatedSpectrum = null;
                var cnt = 0;

                foreach (var sliceInfo in list) {
                    var pulse = _preprocessor.RetrievePulse(pulseSequence, sliceInfo.StartIndex,
                        sliceInfo.CrestOffset,
                        sliceInfo.Length);
                    _rotator.TrySymmetrize(pulse, sliceInfo.CrestOffset); // todo do it at preproposs
                    Complex[] correctedSpectrum;
                    try {
                        correctedSpectrum = _corrector.Correct(pulse);
                    } catch (CorrectFailException) {
                        errorCnt++;
                        continue;
                    }
                    if (accumulatedSpectrum == null) {
                        accumulatedSpectrum = correctedSpectrum.Clone() as Complex[];
                    } else {
                        accumulatedSpectrum.Increase(correctedSpectrum);
                    }
                    cnt++;
                }
                if (accumulatedSpectrum == null) {
                    return SplitResult.FromException(ProcessException.NoFlatPhaseIntervalFound, errorCnt);
                }
                spectra[i] = new Spectrum(accumulatedSpectrum, cnt);
            }

            GasRefTuple tuple;
            if (Average(spectra[1]) > Average(spectra[0])) {
                tuple = GasRefTuple.SourceAndRef(spectra[1], spectra[0]);
            } else {
                tuple = GasRefTuple.SourceAndRef(spectra[0], spectra[1]);
            }
            if (errorCnt != 0) {
                return new SplitResult(tuple, ProcessException.NoFlatPhaseIntervalFound, errorCnt);
            }
            return SplitResult.WithoutException(tuple);
        }

        private static double Average([NotNull] ISpectrum array) {
            double sum = 0;
            for (var i = 0; i < array.Length()/2; i++) {
                sum += array.Intensity(i);
            }
            return sum/array.PulseCount/array.PulseCount;
        }
    }
}