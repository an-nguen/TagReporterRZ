// using System;
// using System.Collections.Generic;
// using System.Net.NetworkInformation;
// using System.Threading.Tasks;
// using Microsoft.EntityFrameworkCore;
// using TagReporter.Datasource;
// using TagReporter.Domains;
//
// namespace TagReporter.Services;
//
// public class WiredTagService
// {
//     private readonly TagReporterContext _context;
//     
//     public WiredTagService(TagReporterContext context)
//     {
//         _context = context;
//     }
//
//     public async Task<WiredTag?> FindAsync(Guid id) => await _context.WiredTags.FindAsync(id); 
//     
//     public async Task<List<WiredTag>> FindAllAsync() => await _context.WiredTags.ToListAsync();
//
//     public async Task CreateAsync(WiredTag tag)
//     {
//         if (string.IsNullOrEmpty(tag.Name) && string.IsNullOrEmpty(tag.MacAddress) && PhysicalAddress.TryParse(tag.MacAddress, out _))
//             throw new Exception("tag has no name or MAC address");
//
//         await _context.WiredTags.AddAsync(tag);
//         await _context.SaveChangesAsync();
//     }
//
//     public async Task UpdateAsync(Guid id, WiredTag tag)
//     {
//         if (string.IsNullOrEmpty(tag.Name) && string.IsNullOrEmpty(tag.MacAddress) && PhysicalAddress.TryParse(tag.MacAddress, out _))
//             throw new Exception("tag has no name or MAC address");
//         var dbWiredTag = await _context.WiredTags.FindAsync(id);
//         if (dbWiredTag == null) throw new Exception($"Failed to find wired tag with id = {id}");
//         dbWiredTag.Name = tag.Name;
//         _context.Update(dbWiredTag);
//         await _context.SaveChangesAsync();
//     }
//
//     public async Task RemoveAsync(Guid id)
//     {
//         var found = await _context.WiredTags.FindAsync(id);
//         if (found == null) throw new Exception($"Failed to find wired tag with id = {id}");
//         _context.WiredTags.Remove(found);
//         await _context.SaveChangesAsync();
//
//     }
// }