using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;

namespace AmoraApp.Services
{
    public class LocationResult
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Description { get; set; } = string.Empty; // texto pra exibir no perfil
        public string Source { get; set; } = string.Empty;      // "gps", "ip", "debug"
    }

    public class LocationService
    {
        public static LocationService Instance { get; } = new LocationService();

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        private LocationService()
        {
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<LocationResult?> GetCurrentLocationAsync()
        {
            try
            {
                // ===== Debug no Windows/Mac Catalyst: fixa São Paulo/SP =====
                if (DeviceInfo.Platform == DevicePlatform.WinUI ||
                    DeviceInfo.Platform == DevicePlatform.MacCatalyst)
                {
                    return new LocationResult
                    {
                        Latitude = -23.55052,
                        Longitude = -46.633308,
                        Description = "São Paulo/SP (debug)",
                        Source = "debug"
                    };
                }

                // ===== Tenta GPS primeiro (Android/iOS, etc.) =====
                Microsoft.Maui.Devices.Sensors.Location? gpsLocation = null;

                try
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                    if (status != PermissionStatus.Granted)
                    {
                        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    }

                    if (status == PermissionStatus.Granted)
                    {
                        var request = new GeolocationRequest(
                            GeolocationAccuracy.Medium,
                            TimeSpan.FromSeconds(10));

                        gpsLocation = await Geolocation.GetLocationAsync(request);
                    }
                }
                catch
                {
                    // Ignora erros de GPS e vai pro fallback via IP
                }

                if (gpsLocation != null)
                {
                    return new LocationResult
                    {
                        Latitude = gpsLocation.Latitude,
                        Longitude = gpsLocation.Longitude,
                        Description = $"{gpsLocation.Latitude:0.0000}, {gpsLocation.Longitude:0.0000} (GPS)",
                        Source = "gps"
                    };
                }

                // ===== Fallback via IP =====
                try
                {
                    var json = await _httpClient.GetStringAsync("https://ipapi.co/json/");
                    var ipInfo = JsonSerializer.Deserialize<IpApiResponse>(json, _jsonOptions);

                    if (ipInfo != null && ipInfo.Latitude != 0 && ipInfo.Longitude != 0)
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(ipInfo.City)) parts.Add(ipInfo.City);
                        if (!string.IsNullOrWhiteSpace(ipInfo.Region)) parts.Add(ipInfo.Region);
                        if (!string.IsNullOrWhiteSpace(ipInfo.CountryName)) parts.Add(ipInfo.CountryName);

                        var desc = parts.Count > 0
                            ? string.Join(", ", parts) + " (IP)"
                            : $"{ipInfo.Latitude:0.0000}, {ipInfo.Longitude:0.0000} (IP)";

                        return new LocationResult
                        {
                            Latitude = ipInfo.Latitude,
                            Longitude = ipInfo.Longitude,
                            Description = desc,
                            Source = "ip"
                        };
                    }
                }
                catch
                {
                    // Se IP também falhar, retorna null
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private class IpApiResponse
        {
            [JsonPropertyName("latitude")]
            public double Latitude { get; set; }

            [JsonPropertyName("longitude")]
            public double Longitude { get; set; }

            [JsonPropertyName("city")]
            public string City { get; set; } = string.Empty;

            [JsonPropertyName("region")]
            public string Region { get; set; } = string.Empty;

            [JsonPropertyName("country_name")]
            public string CountryName { get; set; } = string.Empty;
        }
    }
}
