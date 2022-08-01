using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using TagReporter.Datasource;
using TagReporter.Domains;

namespace TagReporter.Services;

/// <summary>
/// AccountService has access to the database context and
/// can access, modify WstAccount instances
/// </summary>
public class AccountService
{
    private readonly TagReporterContext _context;

    public AccountService(TagReporterContext context)
    {
        _context = context;
    }

    public async Task<List<WstAccount>> FindAll() => await _context.WstAccounts.ToListAsync();

    public async Task CreateAsync(WstAccount wstAccount)
    {
        if (string.IsNullOrEmpty(wstAccount.Email) || string.IsNullOrEmpty(wstAccount.Password))
            throw new Exception("[Create] Email/password is null or empty");
        _context.WstAccounts.Add(wstAccount);
        await _context.SaveChangesAsync();
    }

    public async Task Update(string email, WstAccount wstAccount)
    {
        if (string.IsNullOrEmpty(wstAccount.Email) || string.IsNullOrEmpty(wstAccount.Password))
            throw new Exception("[Update] Email/Password is null or empty");
        _context.Update(wstAccount);
        await _context.SaveChangesAsync();
    }

    public async Task<WstAccount?> FindOne(string email) => await _context.WstAccounts.FindAsync(email);


    public async Task Delete(string email)
    {
        var account = await _context.WstAccounts.FindAsync(email);
        if (account == null)
            throw new Exception("[Delete] Account not found");
        _context.WstAccounts.Remove(account);
        await _context.SaveChangesAsync();
    }
}