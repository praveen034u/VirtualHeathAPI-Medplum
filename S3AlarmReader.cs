using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;
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
                var dateToken = DateTime.UtcNow.ToString("yyyy/MM/dd");
                var prefix = $"{patientId}/{dateToken}/";

                var listRequest = new ListObjectsV2Request
                {
                    BucketName = BucketName,
                    Prefix = prefix
                };

                var listResponse = await _s3Client.ListObjectsV2Async(listRequest);
                if (listResponse.S3Objects == null || !listResponse.S3Objects.Any())
                {
                    Console.WriteLine($"No objects found for patient {patientId} on {dateToken}");
                    return new List<AlarmNotification>();
                }   
             
                var jsonFiles = listResponse.S3Objects
                    .Where(o => o.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var recentFiles = jsonFiles
                    .Select(o => new
                    {
                        Object = o,
                        //Timestamp = ExtractTimestampFromFilename(o.Key)
                        Timestamp = o.Key
                    })
                    .Where(x => x.Timestamp != null)
                    .OrderByDescending(x => x.Timestamp)
                    .Take(100) //TBD read it from configuration
                    .ToList();

                var notifications = new List<AlarmNotification>();

                foreach (var file in recentFiles)
                {
                    var getRequest = new GetObjectRequest
                    {
                        BucketName = BucketName,
                        Key = file.Object.Key
                    };

                    using var response = await _s3Client.GetObjectAsync(getRequest);
                    using var reader = new StreamReader(response.ResponseStream);
                    string json = await reader.ReadToEndAsync();

                    try
                    {
                        var notification = JsonSerializer.Deserialize<AlarmNotification>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (notification != null)
                        {
                            notifications.Add(notification);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error deserializing {file.Object.Key}: {ex.Message}");
                    }
                }

                return notifications;
            }

            private static DateTime? ExtractTimestampFromFilename(string key)
            {
                try
                {
                    var filename = Path.GetFileNameWithoutExtension(key);
                    var match = Regex.Match(filename, @"(\d{8}T\d{6}Z)");
                if (match.Success &&
                    DateTime.TryParseExact(match.Value, "yyyy-MM-dd'T'HH-mm-ss'Z'", null,  //2025-06-21T13-10-17Z
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime parsedDate))
                {
                    return parsedDate;
                }
                }
                catch
                {
                    // Ignore parsing errors
                }

                return null;
            }
        }
    }
