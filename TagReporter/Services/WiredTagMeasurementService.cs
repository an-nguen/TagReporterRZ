using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TagReporter.Datasource;
using TagReporter.Domains;

namespace TagReporter.Services;


/// <summary>
/// This service have access to database by _context
/// responsible for measurements of wired tags
/// </summary>
public class WiredTagMeasurementService
{
    private readonly ILogger<WiredTagMeasurementService> _logger;
    private readonly TagReporterContext _context;

    private readonly string[] _formats =
    {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:sszzz",
        "dd.MM.yyyy H:mm"
    };

    private readonly char[] _separator =
    {
        '\t',
        ';',
        ','
    };

    private int? _separatorIdx;

    public WiredTagMeasurementService(TagReporterContext context, ILogger<WiredTagMeasurementService> logger)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Parse measurements files with CSV format
    /// </summary>
    /// <param name="path">Path to CSV file</param>
    /// <returns>A list of measurements</returns>
    public async Task<List<Measurement>> ParseMeasurement(string path)
    {
        var measurements = new List<Measurement>();
        using var streamReader = new StreamReader(path, Encoding.UTF8);
        while (streamReader.Peek() >= 0)
        {
            var line = await streamReader.ReadLineAsync();
            if (line == null) continue;
            var m = ParseLine(line);
            if (m == null) continue;
            measurements.Add(m);
        }

        _separatorIdx = null;
        _logger.Log(LogLevel.Information, "Measurement loaded {}", measurements.Count);
        return measurements;
    }

    public async Task<List<int>> GetTagIdsAsync()
        => await _context.Measurements
            .Select(m => m.TagId)
            .Distinct()
            .ToListAsync();

    public async Task<List<Measurement>> GetMeasurementsAsync(int tagId)
        => await _context.Measurements
            .Where(m => m.TagId == tagId)
            .ToListAsync();

    /// <summary>
    /// Fetch list of measurements from the Measurements table by an instance of the TagReporterDbContext class
    /// </summary>
    /// <param name="tagId">Tag ID</param>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <returns></returns>
    public async Task<List<Measurement>> GetMeasurementsAsync(int tagId, DateTimeOffset startDate,
        DateTimeOffset endDate) => await _context.Measurements
        .Where(m => m.TagId == tagId && m.DateTime > startDate && m.DateTime < endDate)
        .ToListAsync();

    private Measurement? ParseLine(string line)
    {
        string[]? values = null;
        if (_separatorIdx == null)
        {
            for (var i = 0; i < _separator.Length; i++)
            {
                values = line.Split(_separator[i]);
                if (values.Length < 2) continue;
                _separatorIdx = i;
                break;
            }

            if (_separatorIdx == null)
                return null;
        }
        else
        {
            values = line.Split(_separator[(int)_separatorIdx]);
            if (values.Length < 2) return null;
        }

        if (values == null) return null;
        if (!DateTimeOffset.TryParseExact(values[0].Trim('"'), _formats, null, DateTimeStyles.None, out var dateTime))
            return null;
        if (!decimal.TryParse(values[1].Trim('"').Replace(",", "."), NumberStyles.Any,
                CultureInfo.InvariantCulture.NumberFormat,
                out var temperature))
            return null;
        if (!decimal.TryParse(values[2].Trim('"').Replace(",", "."), NumberStyles.Any,
                CultureInfo.InvariantCulture.NumberFormat,
                out var humidity))
            return null;

        return new Measurement
        {
            DateTime = dateTime,
            Temperature = temperature,
            Humidity = humidity
        };
    }

    public async Task<(int, bool)> UploadMeasurements(List<Measurement> measurements)
    {
        var dbMeasurements = await _context.Measurements.ToListAsync();
        var newMeasurements = measurements
            // Remove repeated data 
            .Where(m =>
                !dbMeasurements.Any(item => item.TagId == m.TagId
                                            && item.PositionId == m.PositionId
                                            && item.DateTime.Equals(m.DateTime)))
            .ToList();
        await _context.Measurements.AddRangeAsync(newMeasurements);
        await _context.SaveChangesAsync();
        return (newMeasurements.Count, newMeasurements.Count == measurements.Count);
    }

    public async Task<DateTimeOffset> GetMaxDateTime() => await _context.Measurements.MaxAsync(m => m.DateTime);
    public async Task<DateTimeOffset> GetMinDateTime() => await _context.Measurements.MinAsync(m => m.DateTime);
}