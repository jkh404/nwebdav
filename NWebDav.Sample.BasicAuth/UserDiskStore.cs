using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NWebDav.Server.Stores;

namespace NWebDav.Sample.Kestrel;



public class WebDavConfig
{
    public string? BasePath { get; set; }
    public List<UserInfo> Users { get; set; }

    public class UserInfo
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}

internal sealed class UserDiskStore : DiskStoreBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    private readonly IOptionsMonitor<WebDavConfig> _webDavConfig;

    public UserDiskStore(IHttpContextAccessor httpContextAccessor, DiskStoreCollectionPropertyManager diskStoreCollectionPropertyManager, DiskStoreItemPropertyManager diskStoreItemPropertyManager, ILoggerFactory loggerFactory, IOptionsMonitor<WebDavConfig> optionsMonitor) : base(diskStoreCollectionPropertyManager, diskStoreItemPropertyManager, loggerFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _webDavConfig = optionsMonitor;
    }

    public override bool IsWritable => true;

    public override string BaseDirectory
    {
        get
        {
            //// Each user has a dedicated directory
            //var username = User?.Identity?.Name;
            //if (username == null) throw new AuthenticationException("not authenticated");
            //var path = Path.Combine(Path.GetTempPath(), username);
            //Directory.CreateDirectory(path);
            //return path;
            var username = User?.Identity?.Name;
            if(string.IsNullOrWhiteSpace(username))throw new AuthenticationException("not authenticated");
            var basePath = _webDavConfig.CurrentValue.BasePath;
            if (string.IsNullOrWhiteSpace(basePath)) basePath = Path.Combine(Directory.GetCurrentDirectory(), "WebDavData");
            var result = Path.Combine(basePath, username);
            Directory.CreateDirectory(result);
            return result;
        }
    }
    
    // Even though the store is a singleton, the HttpContext will still hold
    // the current request's principal. IHttpContextAccessor uses AsyncLocal
    // internally that flows the async operation.
    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;
}