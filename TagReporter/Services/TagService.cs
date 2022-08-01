using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TagReporter.Datasource;
using TagReporter.Domains;
using TagReporter.DTOs;

namespace TagReporter.Services;

/// <summary>
/// The service have access to modify, read records
/// from Tags table by <b>_context</b> (an instance of the TagReporterContext class)
/// </summary>
public class TagService
{
    private readonly TagReporterContext _context;

    public TagService(TagReporterContext context)
    {
        _context = context;
    }

    public async Task<List<Tag>> FindAll() => await _context.Tags.ToListAsync();

    public async Task Create(Tag tag)
    {
        if (string.IsNullOrEmpty(tag.Name))
            throw new Exception("[Create] tag.Name is null or empty");
        _context.Add(tag);
        await _context.SaveChangesAsync();
    }

    public async Task Update(Guid guid, Tag tag)
    {
        var found = await _context.Tags.Where((t) => t.Uuid == guid).FirstOrDefaultAsync();
        if (found != null) throw new Exception($"[Update] Tag with uuid - {guid} exist");
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();
    }

    public async Task<Tag?> FindOne(Guid id) => await _context.Tags.FindAsync(id);

    public void RemoveAll()
    {
        _context.Tags.RemoveRange(_context.Tags);
    }

    public async Task StoreTagsFromCloud(WstAccount wstAccount)
    {
        var cookieContainer = new CookieContainer();
        using var handler = new HttpClientHandler { CookieContainer = cookieContainer };
        using var client = new HttpClient(handler) { BaseAddress = CommonResources.BaseAddress };
        cookieContainer.Add(CommonResources.BaseAddress, new Cookie("WTAG", wstAccount.SessionId));
        var content = new StringContent("{}", Encoding.UTF8, MediaTypeNames.Application.Json);
        var result = await client.PostAsync("/ethClient.asmx/GetTagList2", content);
        if (result.StatusCode != HttpStatusCode.OK)
            throw new Exception($"Error {result.StatusCode}: {result.RequestMessage}");
            
        var responseBody = await result.Content.ReadAsStringAsync();

        var jsonResponse = JsonConvert.DeserializeObject<DefaultWstResponse<TagResponse>>(responseBody);
        if (jsonResponse?.D == null || jsonResponse.D.Count == 0)
            return;

        foreach (var tagResponse in jsonResponse.D)
        {
            var tag = new Tag
            {
                Uuid = new Guid(tagResponse.Uuid ?? string.Empty),
                Name = tagResponse.Name,
                TagManagerName = tagResponse.ManagerName,
                TagManagerMac = tagResponse.Mac,
                Account = wstAccount
            };

            var tagFromDb = await _context.Tags.FindAsync(tag.Uuid);
            if (tagFromDb != null)
                continue;

            await Create(tag);
        }
    }
}