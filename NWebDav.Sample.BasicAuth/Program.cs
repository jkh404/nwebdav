using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NWebDav.Sample.Kestrel;
using NWebDav.Server;
using NWebDav.Server.Authentication;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// 配置Kestrel服务器
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxConcurrentConnections = 1000;
    options.Limits.MaxConcurrentUpgradedConnections = 1000;
    options.Limits.MinRequestBodyDataRate = null; // 禁用最小请求体数据速率限制
    options.Limits.MinResponseDataRate = null; // 禁用最小响应数据速率限制

    // 设置最大请求体大小
    options.Limits.MaxRequestBodySize = int.MaxValue;
    // 设置请求处理超时时间
    options.Limits.RequestHeadersTimeout = TimeSpan.FromHours(12);
    // 设置Keep-Alive超时时间
    options.Limits.KeepAliveTimeout = TimeSpan.FromHours(12);
});

builder.Configuration.AddJsonFile("appsettings.webdav.json", optional: false, reloadOnChange: true);

builder.Services.Configure<WebDavConfig>(builder.Configuration.GetSection("WebDav"));
// Add NWebDAV services and set the options 
builder.Services
    .AddNWebDav(opts => opts.RequireAuthentication = true)
    .AddDiskStore<UserDiskStore>();

// Data protection is used to protect cached cookies. If you're
// not using cached cookies, then data protection is not required
// by NWebDAV.
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.GetTempPath()));

builder.Services
    .AddAuthentication(opts => opts.DefaultScheme = BasicAuthenticationDefaults.AuthenticationScheme)
    .AddBasicAuthentication(opts =>
    {
        opts.AllowInsecureProtocol = true;  // This will enable NWebDAV to allow authentication via HTTP, but your client may not allow it
        opts.CacheCookieName = "NWebDAV";   // Cache the authorization result in a cookie
        opts.CacheCookieExpiration = TimeSpan.FromHours(1); // Cached credentials in the cookie are valid for an hour
        opts.Events.OnValidateCredentials = context =>
        {
            // In a real-world application, this is where you would contact
            // you identity provider and validate the credentials and determine
            // the claims.
            var webDavConfig=context.HttpContext.RequestServices.GetService<IOptionsMonitor<WebDavConfig>>();
            if (webDavConfig?.CurrentValue.Users.Any(t=>t.UserName== context.Username && t.Password==context.Password) ??false)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, context.Username, ClaimValueTypes.String, context.Options.ClaimsIssuer),
                    new Claim(ClaimTypes.Name, context.Username, ClaimValueTypes.String, context.Options.ClaimsIssuer)
                };

                context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                context.Success();
            }
            else
            {
                context.Fail("invalid credentials");
            }

            return Task.CompletedTask;
        };
    });

var app = builder.Build();
app.UseAuthentication();
app.UseNWebDav();

// It this fails, then make sure you have created the certificate. Note that
// the certificate should also be imported in the certificate store of the
// local machine to trust it.
app.Run();