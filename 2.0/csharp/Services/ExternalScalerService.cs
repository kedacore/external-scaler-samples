using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Externalscaler;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace ExternalScalerSample
{
    public class ExternalScalerService : ExternalScaler.ExternalScalerBase
    {
        private static readonly HttpClient _client = new HttpClient();

        private static readonly ConcurrentDictionary<string, ConcurrentBag<IServerStreamWriter<IsActiveResponse>>> _streams =
            new ConcurrentDictionary<string, ConcurrentBag<IServerStreamWriter<IsActiveResponse>>>();

        async Task<int> GetEarthQuakeCount(string longitude, string latitude, double magThreshold)
        {
            var startTime = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            var endTime = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var radiusKm = 500;
            var query = $"format=geojson&starttime={startTime}&endtime={endTime}&longitude={longitude}&latitude={latitude}&maxradiuskm={radiusKm}";

            var resp = await _client.GetAsync($"https://earthquake.usgs.gov/fdsnws/event/1/query?{query}");
            resp.EnsureSuccessStatusCode();
            var payload = JsonConvert.DeserializeObject<USGSResponse>(await resp.Content.ReadAsStringAsync());
            return payload.features.Count(f => f.properties.mag > magThreshold);
        }

        public override async Task<IsActiveResponse> IsActive(ScaledObjectRef request, ServerCallContext context)
        {
            if (!request.ScalerMetadata.ContainsKey("latitude") ||
                !request.ScalerMetadata.ContainsKey("longitude"))
            {
                throw new ArgumentException("longitude and latitude must be specified");
            }

            var longitude = request.ScalerMetadata["longitude"];
            var latitude = request.ScalerMetadata["latitude"];
            var earthquakeCount = await GetEarthQuakeCount(longitude, latitude, 1.0);
            return new IsActiveResponse
            {
                Result = earthquakeCount > 2
            };
        }

        public override async Task StreamIsActive(ScaledObjectRef request, IServerStreamWriter<IsActiveResponse> responseStream, ServerCallContext context)
        {
            if (!request.ScalerMetadata.ContainsKey("latitude") ||
                !request.ScalerMetadata.ContainsKey("longitude"))
            {
                throw new ArgumentException("longitude and latitude must be specified");
            }

            var longitude = request.ScalerMetadata["longitude"];
            var latitude = request.ScalerMetadata["latitude"];
            var key = $"{longitude}|{latitude}";

            while (!context.CancellationToken.IsCancellationRequested)
            {
                var earthquakeCount = await GetEarthQuakeCount(longitude, latitude, 1.0);
                if (earthquakeCount > 2)
                {
                    await responseStream.WriteAsync(new IsActiveResponse
                    {
                        Result = true
                    });
                }
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        public override Task<GetMetricSpecResponse> GetMetricSpec(ScaledObjectRef request, ServerCallContext context)
        {
            var resp = new GetMetricSpecResponse();
            resp.MetricSpecs.Add(new MetricSpec
            {
                MetricName = "earthquakeThreshold",
                TargetSize = 10
            });
            return Task.FromResult(resp);
        }

        public override async Task<GetMetricsResponse> GetMetrics(GetMetricsRequest request, ServerCallContext context)
        {
            if (!request.ScaledObjectRef.ScalerMetadata.ContainsKey("latitude") ||
                !request.ScaledObjectRef.ScalerMetadata.ContainsKey("longitude"))
            {
                throw new ArgumentException("longitude and latitude must be specified");
            }

            var longitude = request.ScaledObjectRef.ScalerMetadata["longitude"];
            var latitude = request.ScaledObjectRef.ScalerMetadata["latitude"];

            var earthquakeCount = await GetEarthQuakeCount(longitude, latitude, 1.0);

            var resp = new GetMetricsResponse();
            resp.MetricValues.Add(new MetricValue
            {
                MetricName = "earthquakeThreshold",
                MetricValue_ = earthquakeCount
            });

            return resp;
        }
    }
}