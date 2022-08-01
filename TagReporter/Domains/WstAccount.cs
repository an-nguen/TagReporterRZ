using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TagReporter.Domains;

/// <summary>
/// Class that contains email and password strings
/// This class is necessary for authentication
/// in wirelesstag.net so that later get
/// list of wireless tags
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class WstAccount
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("email")]
    public string Email { get; set; }
        
    [JsonProperty("password")]
    public string? Password { get; set; }
    [NotMapped]
    public string? SessionId { get; set; }
        
    public WstAccount(string email, string password)
    {
        Email = email;
        Password = password;
    }

    /// <summary>
    /// Get session id from http cookie after request to wirelesstag.net
    /// </summary>
    /// <param name="baseAddress">url address of wirelesstag.net</param>
    /// <exception cref="Exception"></exception>
    public async Task SignIn(Uri baseAddress)
    {
        using var client = new HttpClient {BaseAddress = baseAddress};
        var jsonString = JsonConvert.SerializeObject(this);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        var result = await client.PostAsync("/ethAccount.asmx/SignIn", content);
        var setCookieHeader = result.Headers
            .FirstOrDefault(kv => kv.Key == "Set-Cookie");
        if (setCookieHeader.Equals(default(KeyValuePair<string, IEnumerable<string>>)))
            throw new Exception($"Error: setCookieHeader - {setCookieHeader}");

        var cookies = setCookieHeader.Value;
        var cookie = cookies.FirstOrDefault(c => c.StartsWith("WTAG="));
        if (cookie == null) throw new Exception($"Error: cookie[WTAG=] - {cookie}");

        var sessionId = cookie.Split(";")[0];
        if (string.IsNullOrEmpty(sessionId)) throw new Exception($"Error: sessionId is empty");

        SessionId = sessionId.Substring("WTAG=".Length, sessionId.Length - "WTAG=".Length);
    }
}