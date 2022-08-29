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
using Radzen;
using SMBLibrary;
using SMBLibrary.Client;
using TagReporter.Domains;
using TagReporter.DTOs;
using TagReporter.Settings;
using TagReporter.Utils;

namespace TagReporter.Services;

/// <summary>
/// Сервис создания и отправки отчётов
/// </summary>
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
        // Set time to 00:00
        var endDate = today.AddHours(-today.Hour + beginTimeInHour).AddMinutes(-today.Minute)
            .AddSeconds(-today.Second);
        var startDate = endDate.AddHours(-timeOffsetInHours);

        // Clean up all tags from database
        _tagService.RemoveAll();

        // 1. Load tags from server wirelesstag.net
        var getAccountsTask = _accountService.FindAll();
        getAccountsTask.Wait();
        var accounts = getAccountsTask.Result;
        foreach (var acc in accounts)
        {
            var signInTask = acc.SignIn(CommonResources.BaseAddress);
            signInTask.Wait();
            _tagService.StoreTagsFromCloud(acc).Wait();
        }

        // 2. Load measurements from server wirelesstag.net
        foreach (var zone in zones)
            zone.TagUuids = _zoneService.FindTagUuidsByZone(zone);
        LoadMeasurements(zones, startDate, endDate).Wait();

        // 3. Save report files on a file server
        SaveReportFiles(zones, savePath, startDate, endDate);

        // 4. Form archive of reports
        var (filepath, _) = GenerateArchiveOfReports(zones, startDate, endDate);
        // 5. Send email with archive of reports
        SendEmail(recipientEmailList, filepath);
    }

    private void SendEmail(List<string> recipientEmailList, string filepath)
    {
        var bodyBuilder = new BodyBuilder
        {
            TextBody = $"{DateTime.Now:f}"
        };
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
    /// <summary>
    /// Создаёт отчёты и помещает в архив 
    /// </summary>
    /// <param name="zones"></param>
    /// <param name="startDateTime"></param>
    /// <param name="endDateTime"></param>
    /// <returns> кортеж Tuple(путь к архиву вместе с именем, имя архива)</returns>
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
    /// Создаёт XLSX отчёт
    /// </summary>
    /// <param name="zone">экземпляр класса Zone который содержит список объектов Tags с заполненым списком Measurements</param>
    /// <param name="startDateTime">Дата начала</param>
    /// <param name="endDateTime">Дата конца</param>
    /// <returns>
    ///     кортеж Tuple(путь к файлу с его именем, имя файла)
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
        var filepath = ExcelUtils.GenerateXlsxReportFile(zone, startDate, endDate, dirPath);
        Message += $"[{DateTime.Now:G}] Файл сформирован ({zone.Name}) \n";

        return filepath;
    }

    /// <summary>
    /// Загружает данные измерении тегов с wirelesstag.com. Теги должны быть зашарены для того чтобы можно загрузить данные.
    /// Этот метод заполняет список тегов Tags с данными измерениями Measurements в экземпляре Zone
    /// </summary>
    /// <param name="zone"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns>Ничего. Только заполняет экземпляр класса Zone определённый в параметре.</returns>

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

    

    public void RemoveJob(RecurringJobDto recurringJobDto)
    {
        RecurringJob.RemoveIfExists(recurringJobDto.Id);
    }

    public void TriggerJob(RecurringJobDto recurringJobDto)
    {
        RecurringJob.Trigger(recurringJobDto.Id);
    }
}