using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TagReporter.Datasource;
using TagReporter.Domains;

namespace TagReporter.Services;

public class ZoneService
{
    private readonly TagReporterContext _context;

    public ZoneService(TagReporterContext context)
    {
        _context = context;
    }

    public async Task<List<Zone>> FindAllAsync()
    {
        var zones = await _context.Zones.ToListAsync();
        foreach (var z in zones) z.Tags = FindTagsByZone(z);
        return zones;
    }

    public List<Tag> FindTagsByZone(Zone zone)
    {
        var uuids = _context.ZoneTagUuids
            .Where(zoneTagUuid => zoneTagUuid.ZoneId == zone.Id)
            .Select(ztu => ztu.TagUuid).ToList();
        return _context.Tags.Where(t => uuids.Contains(t.Uuid)).ToList();
    }
    public List<Guid> FindTagUuidsByZone(Zone zone)
    {
        var uuids = _context.ZoneTagUuids
            .Where(zoneTagUuid => zoneTagUuid.ZoneId == zone.Id)
            .Select(ztu => ztu.TagUuid).ToList();
        return _context.Tags.Where(t => uuids.Contains(t.Uuid)).Select(t => t.Uuid).ToList();
    }

    public async Task CreateAsync(Zone zone)
    {
        if (string.IsNullOrEmpty(zone.Name)) throw new Exception("[Create] zone.Name is empty");
        _context.Add(zone);            
        await _context.SaveChangesAsync();
        await _context.AddRangeAsync(zone.TagUuids.Select(uuid => new ZoneTagUuid
        {
            ZoneId = zone.Id,
            TagUuid = uuid
        }));
        await _context.SaveChangesAsync();
    }

    public async Task Update(int? id, Zone zone)
    {
        if (id == null)
            throw new Exception("[Update] id is null");
        var updZone = await _context.Zones.FindAsync(id);
        if (updZone == null) throw new Exception($"[Update] Zone ID={id} not found!");
        updZone.Name = zone.Name;
        _context.Update(updZone);
        await _context.SaveChangesAsync();
        _context.ZoneTagUuids.RemoveRange(
            _context.ZoneTagUuids.Where(zoneTagUuid => zoneTagUuid.ZoneId == updZone.Id));
        await _context.SaveChangesAsync();
        await _context.AddRangeAsync(zone.TagUuids.Select(uuid => new ZoneTagUuid
        {
            ZoneId = updZone.Id,
            TagUuid = uuid
        }));
        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id)
    {
        var zone = await _context.Zones.FindAsync(id);
        if (zone == null) throw new Exception("[Delete] ");
        _context.ZoneTagUuids.RemoveRange(
            _context.ZoneTagUuids.Where(zoneTagUuid => zoneTagUuid.ZoneId == zone.Id));
        _context.Zones.Remove(zone);
        await _context.SaveChangesAsync();
    }

    public async Task<Zone?> FindOne(int id) => await _context.Zones.FindAsync(id);
}