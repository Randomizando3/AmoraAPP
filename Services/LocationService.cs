using System;
using System.Threading.Tasks;
using Microsoft.Maui.Devices.Sensors;

namespace AmoraApp.Services
{
    public class LocationResult
    {
        public double Latitude { get; }
        public double Longitude { get; }
        public bool IsFallback { get; }

        public LocationResult(double latitude, double longitude, bool isFallback)
        {
            Latitude = latitude;
            Longitude = longitude;
            IsFallback = isFallback;
        }
    }

    public interface ILocationService
    {
        Task<LocationResult> GetCurrentLocationAsync();
    }

    public class LocationService : ILocationService
    {
        // São Paulo/SP
        private const double FallbackLat = -23.55052;
        private const double FallbackLng = -46.633308;

        public async Task<LocationResult> GetCurrentLocationAsync()
        {
#if ANDROID || IOS
            try
            {
                if (!Geolocation.Default.IsSupported)
                    return new LocationResult(FallbackLat, FallbackLng, true);

                var request = new GeolocationRequest(
                    GeolocationAccuracy.Medium,
                    TimeSpan.FromSeconds(10));

                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location == null)
                    return new LocationResult(FallbackLat, FallbackLng, true);

                return new LocationResult(location.Latitude, location.Longitude, false);
            }
            catch (FeatureNotSupportedException)
            {
                return new LocationResult(FallbackLat, FallbackLng, true);
            }
            catch (FeatureNotEnabledException)
            {
                return new LocationResult(FallbackLat, FallbackLng, true);
            }
            catch (PermissionException)
            {
                return new LocationResult(FallbackLat, FallbackLng, true);
            }
            catch
            {
                return new LocationResult(FallbackLat, FallbackLng, true);
            }
#else
            // WINDOWS / MAC / OUTROS → Nunca chama Geolocation, sempre fallback
            await Task.CompletedTask;
            return new LocationResult(FallbackLat, FallbackLng, true);
#endif
        }
    }
}
