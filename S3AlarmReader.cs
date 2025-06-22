using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace VirtualHealthAPI
{
    public class S3AlarmReader
    {
        private readonly IAmazonS3 _s3Client;
        private const string BucketName = "health-alert-logs";

        public S3AlarmReader(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }

        public async Task<List<AlarmNotification>> GetAlarmNotification(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
                patientId = "01978609-4506-72a9-a00e-8083bbf66207"; // fallback for local testing

            var notifications = new List<AlarmNotification>();

            for (int i = 0; i < 2; i++)
            {
                //var dateToken = DateTime.UtcNow.AddDays(-i).ToString("yyyy/MM/dd");
                var date = DateTime.UtcNow.AddDays(-i);
                var prefix = $"{patientId}/{date.Year}/{date.Month:D2}/{date.Day:D2}/";

                Console.WriteLine($"🔍 Checking prefix: {prefix}");

                var listRequest = new ListObjectsV2Request
                {
                    BucketName = BucketName,
                    Prefix = prefix
                };

                var listResponse = await _s3Client.ListObjectsV2Async(listRequest);
                if (listResponse.S3Objects == null || !listResponse.S3Objects.Any())
                    Console.WriteLine($"No objects found for patient {patientId} on {dateToken}");
                    return new List<AlarmNotification>();
                }   

                var jsonFiles = listResponse.S3Objects
                    .Where(o => o.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var getRequest = new GetObjectRequest
                        {
                            BucketName = BucketName,
                            Key = file.Key
                        };

                        using var response = await _s3Client.GetObjectAsync(getRequest);
                        using var reader = new StreamReader(response.ResponseStream);
                        var json = await reader.ReadToEndAsync();
                        // Fix invalid timestamp format using regex BEFORE deserializing
                        json = Regex.Replace(json, @"""timestamp"":\s*""(\d{4}-\d{2}-\d{2}T\d{2})-(\d{2})-(\d{2})Z""", m =>
                        {
                            return $@"""timestamp"":""{m.Groups[1].Value}:{m.Groups[2].Value}:{m.Groups[3].Value}Z""";
                        });

                        // Deserialize using wrapper
                        var wrapper = JsonSerializer.Deserialize<AlarmResponseWrapper>($"{{ \"Message\": [{json}] }}", new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (wrapper?.Message != null)
                        {
                            notifications.AddRange(wrapper.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error reading {file.Key}: {ex.Message}");
                    }
                }
            }

            return notifications;
        }
    }
}
