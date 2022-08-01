using System;

namespace TagReporter.DTOs;

public readonly record struct MeasurementRecord(
    DateTimeOffset DateTime,
    double TemperatureValue,
    double Cap
);