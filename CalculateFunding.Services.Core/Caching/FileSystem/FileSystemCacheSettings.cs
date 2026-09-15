using System;
using System.Runtime.CompilerServices;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.Utility;
using Microsoft.Extensions.Configuration;

namespace CalculateFunding.Services.Core.Caching.FileSystem
{
    public class FileSystemCacheSettings : IFileSystemCacheSettings
    {
        public static readonly string SectionName = nameof(FileSystemCacheSettings).ToLower();
        
        private static readonly string _defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static readonly string _defaultPathLinux = "/app/cache";

        private readonly ICacheProvider _cacheProvider;
        private readonly IConfiguration _configuration;

        public FileSystemCacheSettings(IConfiguration configuration, 
            ICacheProvider cacheProvider)
        {
            Guard.ArgumentNotNull(cacheProvider, nameof(cacheProvider));
            Guard.ArgumentNotNull(configuration, nameof(configuration));
            
            _configuration = configuration;
            _cacheProvider = cacheProvider;
        }

        // Use /app/cache when running in a Linux Docker container.
        // Use _defaultPath when running on Windows, e.g. C:\Users\<UserName>\AppData\Roaming.
        public string Path => GetConfigurationValue()
                             ?? (!string.IsNullOrWhiteSpace(_defaultPath)
                                 ? _defaultPath
                                 : _defaultPathLinux);

        public string Prefix => GetConfigurationValue() ?? GetOrCreateCachedPrefix();

        private string GetConfigurationValue([CallerMemberName] string key = null) => _configuration[$"{SectionName}:{key}"];

        private string GetOrCreateCachedPrefix()
        {
            string key = $"{SectionName}:{nameof(Prefix)}";
            
            string cachedPrefix = _cacheProvider.GetAsync<string>(key)
                .GetAwaiter()
                .GetResult();

            if (cachedPrefix.IsNullOrWhitespace())
            {
                cachedPrefix = Guid.NewGuid().ToString();
                
                _cacheProvider.SetAsync(key, cachedPrefix)
                    .GetAwaiter()
                    .GetResult();
            }

            return cachedPrefix;
        }
    }
}