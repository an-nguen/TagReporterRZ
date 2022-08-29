using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml;
using TagReporter.Domains;
using System.Collections.Generic;
using System;
using TagReporter.DTOs;
using System.Linq;
using System.IO;

namespace TagReporter.Utils;

/// <summary>
/// Вспомогательные функции создания отчётов xlsx
/// </summary>
public class ExcelUtils
{
    public static ExcelChart CreatePredefinedScatterLineChart(ExcelWorksheet worksheet, string chartName,
        string title, TagDataType dataType)
    {
        var chart =
            worksheet.Drawings.AddChart(chartName, eChartType.XYScatterSmoothNoMarkers);

        chart.Title.Text = title;
        chart.Title.Font.Size = 18;
        chart.SetPosition(1, 10, 0, 32);
        // Default column size is 64px, row size 20px
        chart.SetSize(896, 580);

        chart.YAxis.Title.Text = dataType switch
        {
            TagDataType.Temperature => "Температура (ºС)",
            TagDataType.Humidity => "Влажность (%)",
            _ => "Неизвестный тип данных"
        };
        chart.XAxis.Title.Text = "Время";
        chart.XAxis.Format = "dd.MM.yyyy HH:mm:ss";
        chart.XAxis.TextBody.Rotation = 270;

        chart.YAxis.Title.Rotation = 270;
        chart.YAxis.Format = "0.00";
        chart.YAxis.MajorTickMark = eAxisTickMark.Out;
        chart.YAxis.MinorTickMark = eAxisTickMark.None;

        chart.Legend.Position = eLegendPosition.Right;
        return chart;
    }

    // Excel chart that can be printed on A4 paper

    public static string GenerateXlsxReportFile(Zone zone, DateTimeOffset startDate, DateTimeOffset endDate,
        string dirPath)
    {
        var date = DateTime.Now.ToString("dd-MM-yyyy HH-mm");
        var filename = $@"{zone.Name}_{date}.xlsx";

        using var fs = File.Create($"{dirPath}/{filename}");
        using var package = new ExcelPackage(fs);
        /* 1. Create sheets */
        // Create sheets with chart 
        var tempChartSheet = package.Workbook.Worksheets.Add("График");
        tempChartSheet.HeaderFooter.FirstHeader.RightAlignedText = "Ф01-СОП-03.04\nВерсия 01";
        var humidityChartSheet = package.Workbook.Worksheets.Add("График влажности");
        humidityChartSheet.HeaderFooter.FirstHeader.RightAlignedText = "Ф01-СОП-03.04\nВерсия 01";
        // Create sheets with measurements
        var tempMSheet = package.Workbook.Worksheets.Add($"Данные измерении (Температуры)");
        var humidityMSheet = package.Workbook.Worksheets.Add($"Данные измерении (Влажности)");
        // Create sheet of list of tags info
        var tagSheet = package.Workbook.Worksheets.Add("Теги");
        /* 2. Create charts */
        // Create temperature chart
        var tempChart =
            CreatePredefinedScatterLineChart(tempChartSheet, "scatterLineChart0",
                $"Журнал мониторинга температуры\n{zone.Name}\n{startDate:dd.MM.yyyy HH:mm:ss} - {endDate:dd.MM.yyyy HH:mm:ss}",
                TagDataType.Temperature);
        // Create humidity chart
        var humidityChart =
            CreatePredefinedScatterLineChart(humidityChartSheet, "scatterLineChart0",
                $"Журнал мониторинга влажности\n{zone.Name}\n{startDate:dd.MM.yyyy HH:mm:ss} - {endDate:dd.MM.yyyy HH:mm:ss}",
                TagDataType.Humidity);
        // Specify min and max values of Time axis
        SetMinMaxTimeAxis(tempChart, startDate, endDate);
        SetMinMaxTimeAxis(humidityChart, startDate, endDate);
        // Specify min and max values of Temperature/Humidity axis
        var tagMeasurements = new List<MeasurementRecord>();
        zone.Tags.ForEach(t => { tagMeasurements.AddRange(t.Measurements); });
        SetMinMaxYAxis(tempChart, tagMeasurements, TagDataType.Temperature);
        SetMinMaxYAxis(humidityChart, tagMeasurements, TagDataType.Humidity);

        // Populate sheet of measurements
        PopulateMSheet(zone, TagDataType.Temperature, tempMSheet, tempChart);
        PopulateMSheet(zone, TagDataType.Humidity, humidityMSheet, humidityChart);

        PopulateTagSheet(tagSheet, zone.Tags);

        package.Save();

        zone.Tags.ForEach(t => t.Measurements.Clear());
        return Path.Combine(dirPath, filename);
    }


    private static void SetMinMaxTimeAxis(ExcelChart chart, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        chart.XAxis.MinValue = startDate.DateTime.ToOADate();
        chart.XAxis.MaxValue = endDate.DateTime.ToOADate();
    }

    private static void SetMinMaxYAxis(ExcelChart chart, List<MeasurementRecord> measurements, TagDataType dataType)
    {
        double minValue, maxValue;

        switch (dataType)
        {
            case TagDataType.Temperature:
                minValue = measurements.Count > 0 ? measurements.Min(m => m.TemperatureValue) - 3 : 0;
                maxValue = measurements.Count > 0 ? measurements.Max(m => m.TemperatureValue) + 3 : 40;
                break;
            case TagDataType.Humidity:
                minValue = measurements.Count > 0 ? measurements.Min(m => m.Cap) - 3 : 0;
                maxValue = measurements.Count > 0 ? measurements.Max(m => m.Cap) + 3 : 100;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dataType), $"Unknown data type ${dataType}");
        }

        chart.YAxis.MinValue = minValue;
        chart.YAxis.MaxValue = maxValue;
    }




    public static void PopulateMSheet(Zone zone, TagDataType dataType, ExcelWorksheet sheet, ExcelChart chart)
    {
        var dateTimePointer = 1; // A
        var valuePointer = 2; // B

        foreach (var tag in zone.Tags)
        {
            var rowNumber = 1;
            sheet.Cells[rowNumber, dateTimePointer].Value = "Дата Время";
            sheet.Cells[rowNumber, valuePointer].Value = tag.Name;

            foreach (var m in tag.Measurements)
            {
                rowNumber++;
                // Set date time
                sheet.Cells[rowNumber, dateTimePointer].Value = m.DateTime.DateTime;
                sheet.Cells[rowNumber, dateTimePointer].Style.Numberformat.Format =
                    "dd.MM.yyyy HH:mm:ss";
                // According to data type set value
                sheet.Cells[rowNumber, valuePointer].Value = dataType switch
                {
                    TagDataType.Temperature => Math.Round(m.TemperatureValue, 6),
                    TagDataType.Humidity => Math.Round(m.Cap, 6),
                    _ => throw new ArgumentOutOfRangeException(nameof(dataType), $"Unknown data type ${dataType}")
                };
            }

            var series = chart.Series.Add(
                sheet.Cells[2, valuePointer, rowNumber, valuePointer],
                sheet.Cells[2, dateTimePointer, rowNumber, dateTimePointer]
            );
            series.HeaderAddress = sheet.Cells[1, valuePointer];

            sheet.Cells[rowNumber, dateTimePointer].AutoFitColumns();
            sheet.Cells[rowNumber, valuePointer].AutoFitColumns();

            dateTimePointer += 2;
            valuePointer += 2;
        }
    }

    public static void PopulateTagSheet(ExcelWorksheet sheet, List<Tag> tags)
    {
        List<string> columns = new()
        {
            "Аккаунт",
            "Менеджер",
            "MAC",
            "Тег",
        };
        // Set columns
        var rowPtr = 1;
        var colPtr = 1;

        columns.ForEach(c =>
        {
            sheet.Cells[rowPtr, colPtr].Value = c;
            colPtr++;
        });
        rowPtr++;
        tags.ForEach(t =>
        {
            if (t.Account != null)
            {
                sheet.Cells[rowPtr, 1].Value = t.Account.Email;
                sheet.Cells[rowPtr, 1].AutoFitColumns();
            }

            sheet.Cells[rowPtr, 2].Value = t.TagManagerName;
            sheet.Cells[rowPtr, 2].AutoFitColumns();
            sheet.Cells[rowPtr, 3].Value = t.TagManagerMac;
            sheet.Cells[rowPtr, 3].AutoFitColumns();
            sheet.Cells[rowPtr, 4].Value = t.Name;
            sheet.Cells[rowPtr, 4].AutoFitColumns();

            rowPtr++;
        });
    }

}
