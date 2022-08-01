using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Cronos;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using Radzen;
using SMBLibrary;
using SMBLibrary.Client;
using TagReporter.Domains;
using TagReporter.DTOs;
using TagReporter.Settings;
using TagReporter.Utils;

namespace TagReporter.Services;

public class ReportService
{
    private readonly AccountService _accountService;
    private readonly TagService _tagService;
    private readonly ZoneService _zoneService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly EmailService _emailService;
    private readonly ILogger _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationService _notificationService;
    private readonly SmbSettings _smbSettings;

    private string? Message { get; set; }
    
    public ReportService(TagService tagService, IWebHostEnvironment webHostEnvironment,
        EmailService emailService, ILogger<ReportService> logger, IHttpClientFactory httpClientFactory,
        AccountService accountService, ZoneService zoneService, NotificationService notificationService,
        IOptions<SmbSettings> smbSettings)
    {
        _tagService = tagService;
        _webHostEnvironment = webHostEnvironment;
        _emailService = emailService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _accountService = accountService;
        _zoneService = zoneService;
        _notificationService = notificationService;
        _smbSettings = smbSettings.Value;
    }

    public void EnqueueJob(List<Zone> zones, List<string> recipientEmailList,
        string cronExpr, int beginTimeInHour, int timeOffsetInHours, string savePath)
    {
        RecurringJob.AddOrUpdate($"SendReports_{Guid.NewGuid()}",
            () => SendReports(zones, recipientEmailList, cronExpr, beginTimeInHour, timeOffsetInHours, savePath),
            cronExpr,
            TimeZoneInfo.Local);
    }

    public void UpdateJob(string id, List<Zone> zones, List<string> recipientEmailList,
        string cronExpr, int beginTimeInHour, int timeOffsetInHours, string savePath)
    {
        RecurringJob.AddOrUpdate(id,
            () => SendReports(zones, recipientEmailList, cronExpr, beginTimeInHour, timeOffsetInHours, savePath),
            cronExpr,
            TimeZoneInfo.Local);
    }

    [AutomaticRetry(Attempts = 2)]
    public void SendReports(List<Zone> zones, List<string> recipientEmailList, string cronExpr, int beginTimeInHour,
        int timeOffsetInHours, string savePath)
    {
        var cronExpression = CronExpression.Parse(cronExpr);
        var nextDate = cronExpression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
        if (nextDate == null) return;

        var today = DateTime.UtcNow.ToLocalTime();
        var endDate = today.AddHours(-today.Hour + beginTimeInHour).AddMinutes(-today.Minute)
            .AddSeconds(-today.Second);
        var startDate = endDate.AddHours(-timeOffsetInHours);

        var bodyBuilder = new BodyBuilder
        {
            TextBody = $"{DateTime.Now:f}"
        };

        _tagService.RemoveAll();

        // 1. Load tags from cloud server
        var getAccountsTask = _accountService.FindAll();
        getAccountsTask.Wait();
        var accounts = getAccountsTask.Result;
        foreach (var acc in accounts)
        {
            var signInTask = acc.SignIn(CommonResources.BaseAddress);
            signInTask.Wait();
            _tagService.StoreTagsFromCloud(acc).Wait();
        }

        // 2. Load measurements from cloud server 
        foreach (var zone in zones)
            zone.TagUuids = _zoneService.FindTagUuidsByZone(zone);

        LoadMeasurements(zones, startDate, endDate).Wait();
        SaveReportFiles(zones, savePath, startDate, endDate);
        
        // 4. Form archive of reports
        var (filepath, filename) = GenerateArchiveOfReports(zones, startDate, endDate);
        // 5. Send email
        bodyBuilder.Attachments.Add(filepath);
        recipientEmailList.ForEach(email =>
        {
            _emailService.SendReport(MailboxAddress.Parse(email), bodyBuilder.ToMessageBody());
        });
    }

    private void SaveReportFiles(List<Zone> zones, string savePath, DateTime startDate, DateTime endDate)
    {
        ISMBFileStore? fileStore = null;
        // 3. Save reports to shared folder
        var client = new SMB2Client();
        try
        {
            var isConnected = client.Connect(IPAddress.Parse(_smbSettings.Host), SMBTransportType.DirectTCPTransport);
            client.Login("dominanta-logic", _smbSettings.Username, _smbSettings.Password);
            savePath = $@"{savePath}\{DateTimeOffset.Now:dd-MM-yyyy}";
            if (!isConnected) throw new Exception("failed to connect SMB share");
            fileStore = client.TreeConnect(_smbSettings.ShareName, out var status);
            if (status != NTStatus.STATUS_SUCCESS) throw new Exception($"failed to connect SMB share: {status}");
            status = SmbLibUtils.CreateDir(fileStore, savePath);
            if (status != NTStatus.STATUS_SUCCESS) throw new Exception($"failed to create folder: {status}");
            var tuples = GenerateReports(zones, startDate, endDate);
            foreach (var tuple in tuples)
            {
                using var stream = File.Open(tuple.filepath, FileMode.Open, FileAccess.Read);
                status = SmbLibUtils.CreateFile(client, fileStore, stream, $@"{savePath}\{tuple.filename}");
                if (status != NTStatus.STATUS_SUCCESS) throw new Exception($"failed to write to file: {status}");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }
        finally
        {
            fileStore?.Disconnect();
            client.Logoff();
            client.Disconnect();
        }
    }

    private List<(string filepath, string filename)> GenerateReports(List<Zone> zones, DateTimeOffset startDateTime,
        DateTimeOffset endDateTime)
    {
        var dirPath =
            $"{_webHostEnvironment.ContentRootPath}{Path.DirectorySeparatorChar}AppData{Path.DirectorySeparatorChar}{DateTime.Now:yy-MM-dd}";
        if (Directory.Exists(dirPath))
            Directory.Delete(dirPath, true);
        Directory.CreateDirectory(dirPath);

        return zones.Select(zone => GenerateReport(zone, startDateTime, endDateTime, dirPath))
            .Select(filepath => (filepath, Path.GetFileName(filepath))).ToList();
    }

    public (string, string) GenerateArchiveOfReports(List<Zone> zones, DateTimeOffset startDateTime,
        DateTimeOffset endDateTime)
    {
        var filename = $"{DateTime.Now:yy-MM-dd}_archive{Guid.NewGuid()}.zip";
        var dirPath =
            $"{_webHostEnvironment.ContentRootPath}{Path.DirectorySeparatorChar}AppData{Path.DirectorySeparatorChar}{DateTime.Now:yy-MM-dd}";
        if (Directory.Exists(dirPath))
            Directory.Delete(dirPath, true);

        Directory.CreateDirectory(dirPath);

        var zipFilepath = Path.Combine(dirPath, filename);
        using var archive = ZipFile.Open(zipFilepath, ZipArchiveMode.Create);
        foreach (var filepath in zones.Select(zone => GenerateReport(zone, startDateTime, endDateTime, dirPath)))
            archive.CreateEntryFromFile(filepath, Path.GetFileName(filepath));

        return (zipFilepath, filename);
    }

    /// <summary>
    /// Generate XLSX reports
    /// </summary>
    /// <param name="zone">Zone instance that contains Tags with measurements</param>
    /// <param name="startDateTime"></param>
    /// <param name="endDateTime"></param>
    /// <returns>
    ///     Tuple(filepath, filename)
    /// </returns>
    public (string, string) GenerateReport(Zone zone, DateTimeOffset startDateTime,
        DateTimeOffset endDateTime)
    {
        var dirPath =
            $"{_webHostEnvironment.ContentRootPath}{Path.DirectorySeparatorChar}AppData{Path.DirectorySeparatorChar}{DateTime.Now:yy-MM-dd}";
        if (Directory.Exists(dirPath))
            Directory.Delete(dirPath, true);

        Directory.CreateDirectory(dirPath);

        var filepath = GenerateReport(zone, startDateTime, endDateTime, dirPath);

        return (filepath, Path.GetFileName(filepath));
    }
    
    public (string, string) GenerateReport(IReadOnlyCollection<Measurement> measurements, 
        DateTimeOffset startDate, DateTimeOffset endDate)
    {
        var tags = measurements.Select(m => m.TagId)
            .Distinct()
            .Select(tagId => new Tag
        {
            Name = $"Tag {tagId}",
        }).ToList();
        foreach (var tag in tags)
        {
            tag.Measurements = measurements
                .Where(m => m.TagId == int.Parse(tag.Name!.Split(' ')[1]))
                .Select(m => new MeasurementRecord
                {
                    Cap = (double) (m.Humidity ?? 0),
                    TemperatureValue = (double)m.Temperature,
                    DateTime = m.DateTime
                })
                .ToList();
        }

        var zone = new Zone
        {
            Name = "Custom",
            Tags = tags
        };
        return GenerateReport(zone, 
            measurements.Select(m => m.DateTime).Min(), 
            measurements.Select(m => m.DateTime).Max());
    }

    
    // Load measurement from cloud server.
    // Parameters: 
    // - zones - List of zones (TagUuids should be populated)
    public async Task LoadMeasurements(List<Zone> zones, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        _logger.Log(LogLevel.Information, "Загружаем измерения ({:G} - {:G})", startDate, endDate);

        foreach (var zone in zones)
        {
            var tags = await _tagService.FindAll();
            zone.Tags = tags.Where(t => zone.TagUuids.Contains(t.Uuid)).ToList();
            if (zone.Tags.Count == 0)
                Message += $"[{DateTime.Now:G}] Нет тегов ({zone.Name}) \n";

            await DownloadMeasurements(zone, startDate, endDate);
            Message += $"[{DateTime.Now:G}] Данные загружены ({zone.Name}) \n";
        }
    }

    private string GenerateReport(Zone zone, DateTimeOffset startDate, DateTimeOffset endDate, string dirPath)
    {
        var filepath = GenerateXlsxReportFile(zone, startDate, endDate, dirPath);
        Message += $"[{DateTime.Now:G}] Файл сформирован ({zone.Name}) \n";

        return filepath;
    }


    private async Task DownloadMeasurements(Zone zone, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        var fromDateStr = startDate.ToString("M/d/yyyy HH:mm");
        var toDateStr = endDate.ToString("M/d/yyyy HH:mm");

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = CommonResources.BaseAddress;

        foreach (var tag in zone.Tags)
        {
            var request = new TemperatureRawDataRequest
            {
                FromDate = fromDateStr,
                ToDate = toDateStr,
                Uuid = tag.Uuid.ToString()
            };

            var jsonRequestBody = JsonConvert.SerializeObject(request);
            var content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/ethLogShared.asmx/GetTemperatureRawDataByUUID", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError("[DownloadMeasurements] responseBody = {}", responseBody);
                _notificationService.Notify(NotificationSeverity.Error, "Ошибка", responseBody, 5000);
                continue;
            }

            var jsonResponse =
                JsonConvert.DeserializeObject<DefaultWstResponse<TemperatureRawDataResponse>>(responseBody);
            tag.Measurements = new List<MeasurementRecord>();
            if (jsonResponse?.D == null) return;
            foreach (var r in jsonResponse.D)
            {
                if (r.Time == null) continue;
                var dateTime = DateTimeOffset.Parse(r.Time);
                tag.Measurements.Add(new MeasurementRecord(dateTime, Math.Round(r.TempDegC, 6), r.Cap));
            }

            tag.Measurements = tag.Measurements.Where(t => t.DateTime > startDate && t.DateTime < endDate).ToList();

            if (tag.Measurements.Count == 0)
                Message += $"[{DateTime.Now:G}] Нет измерении ({zone.Name}) \n";
        }
    }

    // Excel chart that can be printed on A4 paper
    private static ExcelChart CreatePredefinedScatterLineChart(ExcelWorksheet worksheet, string chartName,
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


    private static string GenerateXlsxReportFile(Zone zone, DateTimeOffset startDate, DateTimeOffset endDate,
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

    private static void PopulateMSheet(Zone zone, TagDataType dataType, ExcelWorksheet sheet, ExcelChart chart)
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

    private static void PopulateTagSheet(ExcelWorksheet sheet, List<Tag> tags)
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

    public void RemoveJob(RecurringJobDto recurringJobDto)
    {
        RecurringJob.RemoveIfExists(recurringJobDto.Id);
    }

    public void TriggerJob(RecurringJobDto recurringJobDto)
    {
        RecurringJob.Trigger(recurringJobDto.Id);
    }
}