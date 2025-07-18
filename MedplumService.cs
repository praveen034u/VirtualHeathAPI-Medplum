using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.Extensions.Configuration;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using System.Net.Sockets;
using System.Security.AccessControl;

namespace VirtualHealthAPI
{
    public class MedplumService
    {
        private readonly InfluxDBClient _influxClient;
        private readonly string _bucket;
        private readonly string _org;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly string _apiBaseUrl;
        private readonly HttpClient _httpClient;
        private readonly string _catSocialHistory = "social-history";
        private readonly string _catLifeStyle = "lifestyle";
        private readonly string _catVitalSign = "vital-signs";
        private readonly string _influxUrl;
        private readonly char[] _token;
        private readonly S3AlarmReader _s3alarmReader;

        public MedplumService(IHttpClientFactory httpClientFactory, IConfiguration config, InfluxDBClient influxClient, S3AlarmReader s3alarmReader)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _apiBaseUrl = $"{_config["Medplum:FhirUrl"]}";
            _httpClient = httpClientFactory.CreateClient();
            //_influxUrl = config["Influx:Url"];
            //_token = config["Influx:Token"].ToCharArray();
            _org = config["Influx:Org"]!;
            _bucket = config["Influx:Bucket"]!;

            //_influxClient = InfluxDBClientFactory.Create(_influxUrl, new string(_token));
            _influxClient = influxClient;
            _s3alarmReader = s3alarmReader;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var client = _httpClientFactory.CreateClient();
            var tokenUrl = _config["Medplum:TokenUrl"] ?? "https://api.medplum.com/oauth2/token";
            var clientId = _config["Medplum:ClientId"];
            var clientSecret = _config["Medplum:ClientSecret"];
            Console.WriteLine("Token URL: " + tokenUrl);
            Console.WriteLine("Client ID: " + clientId);
            Console.WriteLine("Client Secret: " + (string.IsNullOrEmpty(clientSecret) ? "MISSING" : "SET"));
            HttpResponseMessage response = null;
            try
            {
                var formData = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "system/*.*"
                };
                Console.WriteLine($"Requesting token from {tokenUrl} with client ID {clientId} and clientSecret {clientSecret}");
                response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(formData));
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Medplum token error {response.StatusCode}: {responseBody}");
                    throw new ApplicationException("Medplum token request failed.");
                }

                var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody);
                return tokenResponse["access_token"].ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in GetAccessTokenAsync: " + ex.Message);
                throw;
            }
        }

        public async Task<string> IngestWearableObservationsDataStoreAsync(WearableVitalsInput input)
        {

            // write the logic to store the data from device to influx db
            // var point = PointData
            //.Measurement("vitals")
            //.Tag("device", "wearable-1")
            //.Field("heartRate", heartRate)
            //.Field("spo2", spo2)
            //.Timestamp(DateTime.UtcNow, WritePrecision.Ns);

            // using (var writeApi = _influxClient.GetWriteApiAsync())
            // {
            //     await writeApi.WritePointAsync(point, _bucket, _org);
            // }
            var writeApi = _influxClient.GetWriteApiAsync();

            var point = PointData
                .Measurement("vitals")
                .Tag("deviceId", input.DeviceId)
                .Tag("patientId", input.PatientId)
                //.Field("deviceId", input.DeviceId)
                .Field("heartRate", (int)(input.HeartRate ?? 0))
                .Field("systolicBp", (int)(input.Systolic ?? 0))
                .Field("diastolicBp", (int)(input.Diastolic ?? 0))
                .Field("spo2", (int)(input.Spo2 ?? 0))
                .Field("temperature", input.Temperature ?? 0)
                .Field("steps", (int)(input.Steps ?? 0))
                .Field("respiratoryRate", (int)(input.RespiratoryRate ?? 0))
                .Field("bloodGlucose", (int)(input.BloodGlucose ?? 0))
                // .Field("bloodPressure", $"{input.Systolic}/{input.Diastolic}")
                .Field("caloriesBurned", (int)(input.CaloriesBurned ?? 0))
                .Field("heartRateVariability", (int)(input.HeartRateVariability ?? 0))
                .Field("vo2Max", (int)(input.Vo2Max ?? 0))
                .Field("skinTemperature", input.SkinTemperature ?? 0)
                .Field("sleepDuration", input.SleepDuration ?? 0)
                .Field("sleepRestlessnessIndex", (int)(input.SleepRestlessnessIndex ?? 0))
                .Field("stepsGoalCompletion", (int)(input.StepsGoalCompletion ?? 0))
                .Field("oxygenDesaturationEvents", input.OxygenDesaturationEvents ?? 0)
                .Field("collectedDateTime", input.CollectedDateTime?.ToString("o") ?? DateTime.UtcNow.ToString("o"))
                .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

            await writeApi.WritePointAsync(point, _bucket, _org);



            return $"Saved Wearable data.";
        }

        public async Task<string> IngestWearableObservationsEHRSystemAsync(WearableVitalsInput input)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Use CollectedDateTime if provided, else fallback to current UTC
            var timestamp = input.CollectedDateTime?.ToString("o") ?? DateTime.UtcNow.ToString("o");

            var observations = new List<object>();

            // Helper method to create simple Observations
            void AddSimpleObservation(string loincCode, string display, double value, string unit, string unitCode)
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    device = new
                    {
                        reference = $"Device/{input.DeviceId}-wearable"
                    },
                    category = new[] {
                new {
                    coding = new[] {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "vital-signs" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[] {
                    new { system = "http://loinc.org", code = loincCode, display = display }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = value,
                        unit = unit,
                        system = "http://unitsofmeasure.org",
                        code = unitCode
                    }
                });
            }

            if (input.HeartRate.HasValue)
                AddSimpleObservation("8867-4", "Heart rate", input.HeartRate.Value, "beats/min", "/min");

            if (input.Systolic.HasValue && input.Diastolic.HasValue)
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    device = new
                    {
                        reference = $"Device/{input.DeviceId}-wearable"
                    },
                    category = new[] {
                new {
                    coding = new[] {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "vital-signs" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[] {
                    new { system = "http://loinc.org", code = "85354-9", display = "Blood pressure panel" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    component = new[] {
                new {
                    code = new {
                        coding = new[] { new { system = "http://loinc.org", code = "8480-6", display = "Systolic blood pressure" } }
                    },
                    valueQuantity = new {
                        value = input.Systolic.Value,
                        unit = "mmHg",
                        system = "http://unitsofmeasure.org",
                        code = "mm[Hg]"
                    }
                },
                new {
                    code = new {
                        coding = new[] { new { system = "http://loinc.org", code = "8462-4", display = "Diastolic blood pressure" } }
                    },
                    valueQuantity = new {
                        value = input.Diastolic.Value,
                        unit = "mmHg",
                        system = "http://unitsofmeasure.org",
                        code = "mm[Hg]"
                    }
                }
            }
                });
            }

            if (input.Spo2.HasValue)
                AddSimpleObservation("59408-5", "Oxygen saturation in Arterial blood", input.Spo2.Value, "%", "%");

            if (input.Temperature.HasValue)
                AddSimpleObservation("8310-5", "Body temperature", input.Temperature.Value, "°F", "°F");

            if (input.Steps.HasValue)
                AddSimpleObservation("41950-7", "Number of steps", input.Steps.Value, "steps", "steps");

            if (input.RespiratoryRate.HasValue)
                AddSimpleObservation("9279-1", "Respiratory rate", input.RespiratoryRate.Value, "breaths/min", "/min");

            if (input.BloodGlucose.HasValue)
                AddSimpleObservation("2339-0", "Glucose [Mass/volume] in Blood", input.BloodGlucose.Value, "mg/dL", "mg/dL");

            if (input.CaloriesBurned.HasValue)
                AddSimpleObservation("41981-2", "Calories burned", input.CaloriesBurned.Value, "kcal", "kcal");

            if (input.HeartRateVariability.HasValue)
                AddSimpleObservation("80394-6", "Heart rate variability", input.HeartRateVariability.Value, "milliseconds", "ms");

            if (input.Vo2Max.HasValue)
                AddSimpleObservation("41918-4", "VO2 Max", input.Vo2Max.Value, "ml/kg/min", "mL/(kg.min)");


            if (input.SleepDuration.HasValue)
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    device = new
                    {
                        reference = $"Device/{input.DeviceId}-wearable"
                    },
                    category = new[] {
                new {
                    coding = new[] {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "activity" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[] {
                    new { system = "http://loinc.org", code = "93832-4", display = "Sleep duration" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = input.SleepDuration.Value,
                        unit = "hours",
                        system = "http://unitsofmeasure.org",
                        code = "h"
                    }
                });

            if (input.SleepRestlessnessIndex.HasValue)
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    device = new
                    {
                        reference = $"Device/{input.DeviceId}-wearable"
                    },
                    category = new[] {
                new {
                    coding = new[] {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "activity" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[] {
                    new { system = "http://example.org/custom", code = "sleep-restlessness", display = "Sleep restlessness index" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = input.SleepRestlessnessIndex.Value,
                        unit = "%",
                        system = "http://unitsofmeasure.org",
                        code = "%"
                    }
                });


            if (input.StepsGoalCompletion.HasValue)
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    device = new
                    {
                        reference = $"Device/{input.DeviceId}-wearable"
                    },
                    category = new[] {
    new {
        coding = new[] {
            new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "activity" }
        }
    }
},
                    code = new
                    {
                        coding = new[] {
                    new { system = "http://example.org/custom", code = "steps-goal-completion", display = "Steps Goal Completion %" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = input.StepsGoalCompletion.Value,
                        unit = "%",
                        system = "http://unitsofmeasure.org",
                        code = "%"
                    }
                });

            if (input.OxygenDesaturationEvents.HasValue)
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    device = new
                    {
                        reference = $"Device/{input.DeviceId}-wearable"
                    },
                    category = new[] {
    new {
        coding = new[] {
            new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "vital-signs" }
        }
    }
},
                    code = new
                    {
                        coding = new[] {
                    new { system = "http://example.org/custom", code = "oxygen-desaturation-events", display = "Oxygen Desaturation Events" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueInteger = input.OxygenDesaturationEvents.Value
                });

            // Now post each observation
            foreach (var obs in observations)
            {
                var json = JsonSerializer.Serialize(obs);
                var res = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Observation",
                    new StringContent(json, Encoding.UTF8, "application/fhir+json"));
                res.EnsureSuccessStatusCode();
            }

            return $"Ingested {observations.Count} wearable vitals for Patient/{input.PatientId}.";
        }

        public async Task<PatientProfileInput> GetPatientFullProfileByPatientIdAsync(string patientId)
        {
            var token = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var searchJson = await FhirGetAsync("Patient", $"_id={Uri.EscapeDataString(patientId)}");
            return await SetPatientprfileData(searchJson);
        }

        public async Task<PatientProfileInput> GetPatientFullProfileByEmailAsync(string email)
        {
            var token = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var searchJson = await FhirGetAsync("Patient", $"telecom=email%7C{Uri.EscapeDataString(email)}");
            return await SetPatientprfileData(searchJson);
        }

        private async Task<PatientProfileInput> SetPatientprfileData(JsonElement searchJson)
        {
            if (!searchJson.TryGetProperty("entry", out var entries) || entries.GetArrayLength() == 0)
                return null;

            var patientJson = entries[0].GetProperty("resource");
            var patientId = patientJson.GetProperty("id").GetString() ?? throw new Exception("Patient ID missing.");

            var patientProfile = new PatientProfileInput
            {
                PatientId = patientId,
                PatientName = $"{patientJson.GetProperty("name")[0].GetProperty("given")[0].GetString()} {patientJson.GetProperty("name")[0].GetProperty("family").GetString()}",
                FirstName = $"{patientJson.GetProperty("name")[0].GetProperty("given")[0].GetString()}",
                LastName = $"{patientJson.GetProperty("name")[0].GetProperty("family").GetString()}",
                BirthDate = patientJson.GetProperty("birthDate").GetString() ?? "",
                Gender = patientJson.GetProperty("gender").GetString() ?? ""
            };

            // 2. Fetch Patient Email and Phone
            if (patientJson.TryGetProperty("telecom", out var telecoms))
            {
                foreach (var telecom in telecoms.EnumerateArray())
                {
                    var system = telecom.GetProperty("system").GetString();
                    var value = telecom.GetProperty("value").GetString();
                    if (system == "phone")
                        patientProfile.PhoneNumber = value ?? "";
                    else if (system == "email")
                        patientProfile.Email = value ?? "";
                }
            }

            // 2.1 Fetch Patient Address
            if (patientJson.TryGetProperty("address", out var addresses))
            {
                foreach (var address in addresses.EnumerateArray())
                {
                    var use = address.GetProperty("use").GetString();
                    if (use == "home")
                    {
                        patientProfile.PatientAddress.AddressLine1 = address.GetProperty("line")[0].GetString() ?? string.Empty;
                        patientProfile.PatientAddress.City = address.GetProperty("city").GetString() ?? string.Empty;
                        patientProfile.PatientAddress.State = address.GetProperty("state").GetString() ?? string.Empty;
                        patientProfile.PatientAddress.ZipCode = address.GetProperty("postalCode").GetString() ?? string.Empty;
                        patientProfile.PatientAddress.Country = address.GetProperty("country").GetString() ?? string.Empty;
                    }
                }
            }

            // 3. Fetch Emergency Contact
            if (patientJson.TryGetProperty("contact", out var contacts))
            {
                var emergency = contacts[0]; // Assuming first contact is emergency
                if (emergency.TryGetProperty("name", out var contactName))
                {
                    patientProfile.EmergencyContactFirstName = contactName.GetProperty("given")[0].GetString() ?? "";
                    patientProfile.EmergencyContactLastName = contactName.GetProperty("family").GetString() ?? "";
                }
                if (emergency.TryGetProperty("telecom", out var contactTelecoms))
                {
                    foreach (var contactTelecom in contactTelecoms.EnumerateArray())
                    {
                        if (contactTelecom.GetProperty("system").GetString() == "phone")
                        {
                            patientProfile.EmergencyContactPhone = contactTelecom.GetProperty("value").GetString() ?? "";
                            break;
                        }
                    }
                }
            }

            // 4. Fetch Practitioner (PCP)
            if (patientJson.TryGetProperty("generalPractitioner", out var gpList))
            {
                var practitionerRef = gpList[0].GetProperty("reference").GetString(); // e.g., Practitioner/123
                if (practitionerRef != null)
                {
                    var practitionerId = practitionerRef.Split('/')[1];
                    //var practitionerRes = await _httpClient.GetAsync($"{_config["Medplum:FhirUrl"]}/Practitioner/{practitionerId}");
                    //practitionerRes.EnsureSuccessStatusCode();
                    //var practitionerJson = JsonDocument.Parse(await practitionerRes.Content.ReadAsStringAsync()).RootElement;

                    var practitionerJson = await FhirGetAsync($"Practitioner/{practitionerId}", string.Empty);
                    patientProfile.Pcp = new PractitionerInput
                    {
                        PractitionerId = practitionerId,
                        PractitionerName = $"{practitionerJson.GetProperty("name")[0].GetProperty("given")[0].GetString()} {practitionerJson.GetProperty("name")[0].GetProperty("family").GetString()}",
                        FirstName = $"{practitionerJson.GetProperty("name")[0].GetProperty("given")[0].GetString()}",
                        LastName = $"{practitionerJson.GetProperty("name")[0].GetProperty("family").GetString()}",
                        Gender = practitionerJson.GetProperty("gender").GetString() ?? "",
                        Email = practitionerJson.TryGetProperty("telecom", out var pcpTelecom) && pcpTelecom.GetArrayLength() > 0
                                ? pcpTelecom[0].GetProperty("value").GetString() ?? ""
                                : ""
                    };
                }
            }

            var observations = await GetPatientObservationsAsync(patientId);
            if (observations.Count > 0)
                ParseObservation(patientProfile, observations);

            patientProfile.PastConditions = await GetPatientConditionsAsync(patientId);

            patientProfile.Consent = await GetPatientConsentAsync(patientId);

            return patientProfile;
        }


        public async Task<string> CreatePatientWithPcpAndVitalsAsync(PatientProfileInput input)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            // 1. Create Practitioner (PCP)
            var pcpPayload = new
            {
                resourceType = "Practitioner",
                name = new[] {
            new {
                family = input.Pcp.LastName,
                given = new[] { input.Pcp.FirstName }
            }
        },
                telecom = new[] {
            new {
                system = "email",
                value = input.Pcp.Email,
                use = "work"
            }
        },
                gender = input.Pcp.Gender
            };

            var pcpJson = JsonSerializer.Serialize(pcpPayload);
            var pcpRes = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Practitioner",
                new StringContent(pcpJson, Encoding.UTF8, "application/fhir+json"));
            pcpRes.EnsureSuccessStatusCode();
            var pcpId = JsonDocument.Parse(await pcpRes.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();

            // 2. Create Patient with PCP link
            var patientPayload = new
            {
                resourceType = "Patient",
                name = new[] {
            new {
                given = new[] { input.FirstName },
                family = input.LastName
            }
        },
                gender = input.Gender,
                birthDate = input.BirthDate,

                telecom = new[]
    {
        new
        {
            system = "phone",
            value = input.PhoneNumber,
            use = "mobile"
        },
        new
        {
            system = "email",
            value = input.Email,   // <-- assuming input.Email is available
            use = "home"
        }
    },
                address = new[]
    {
        new
        {
            use = "home",
            line = new[] { input.PatientAddress.AddressLine1  },
            city = input.PatientAddress.City,
            state = input.PatientAddress.State,
            postalCode = input.PatientAddress.ZipCode,
            country = input.PatientAddress.Country
        }
    },
                contact = new[]
    {
        new
        {
            relationship = new[]
            {
                new
                {
                    coding = new[]
                    {
                        new
                        {
                            system = "http://terminology.hl7.org/CodeSystem/v2-0131",
                            code = "E",
                            display = "Emergency"
                        }
                    }
                }
            },
            name = new
            {
                given = new[] { input.EmergencyContactFirstName },
                family = input.EmergencyContactLastName
            },
            telecom = new[]
            {
                new
                {
                    system = "phone",
                    value = input.EmergencyContactPhone,
                    use = "mobile"
                }
            }
        }
    },

                generalPractitioner = new[] {
            new {
                reference = $"Practitioner/{pcpId}"
            }
        }
            };

            var patientJson = JsonSerializer.Serialize(patientPayload);
            var patientRes = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Patient",
                new StringContent(patientJson, Encoding.UTF8, "application/fhir+json"));
            patientRes.EnsureSuccessStatusCode();
            var patientId = JsonDocument.Parse(await patientRes.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();


            // 3. Social History Observations (Smoking, Alcohol, Occupation)
            var observations = new List<object>();
            if (!string.IsNullOrEmpty(input.SmokingStatus))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "social-history", display = "Social History" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://loinc.org", code = "72166-2", display = "Tobacco smoking status" }
                }
                    },
                    subject = new { reference = $"Patient/{patientId}" },
                    effectiveDateTime = DateTime.UtcNow.ToString("o"),
                    valueString = input.SmokingStatus
                });
            }

            if (!string.IsNullOrEmpty(input.AlcoholUse))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "social-history", display = "Social History" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://loinc.org", code = "74013-4", display = "Alcohol use" }
                }
                    },
                    subject = new { reference = $"Patient/{patientId}" },
                    effectiveDateTime = DateTime.UtcNow.ToString("o"),
                    valueString = input.AlcoholUse
                });
            }

            // 4. Create Lifestyle Observations
            // 4. Lifestyle Observations (Exercise, Diet)
            if (!string.IsNullOrEmpty(input.ExerciseFrequency))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "lifestyle", display = "Lifestyle" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://example.org/custom", code = "exercise-frequency", display = "Exercise Frequency" }
                }
                    },
                    subject = new { reference = $"Patient/{patientId}" },
                    effectiveDateTime = DateTime.UtcNow.ToString("o"),
                    valueString = input.ExerciseFrequency
                });
            }

            if (!string.IsNullOrEmpty(input.DietHabits))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "lifestyle", display = "Lifestyle" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://example.org/custom", code = "diet-habits", display = "Diet Habits" }
                }
                    },
                    subject = new { reference = $"Patient/{patientId}" },
                    effectiveDateTime = DateTime.UtcNow.ToString("o"),
                    valueString = input.DietHabits
                });
            }

            // Post all observations
            foreach (var obs in observations)
            {
                var json = JsonSerializer.Serialize(obs);
                var res = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Observation",
                    new StringContent(json, Encoding.UTF8, "application/fhir+json"));
                res.EnsureSuccessStatusCode();
            }

            // 4. Fetch Conditions if exists
            foreach (var condition in input.PastConditions)
            {
                var conditionPayload = new
                {
                    resourceType = "Condition",
                    clinicalStatus = new
                    {
                        coding = new[] {
                    new {
                        system = "http://terminology.hl7.org/CodeSystem/condition-clinical",
                        code = "active"
                    }
                }
                    },
                    verificationStatus = new
                    {
                        coding = new[] {
                    new {
                        system = "http://terminology.hl7.org/CodeSystem/condition-ver-status",
                        code = "confirmed"
                    }
                }
                    },
                    code = new
                    {
                        coding = new[] {
                    new {
                        system = "http://snomed.info/sct",
                        code = condition.Code,
                        display = condition.Display
                    }
                }
                    },
                    subject = new
                    {
                        reference = $"Patient/{patientId}"
                    }
                };

                var conditionJson = JsonSerializer.Serialize(conditionPayload);
                var conditionRes = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Condition",
                    new StringContent(conditionJson, Encoding.UTF8, "application/fhir+json"));
                conditionRes.EnsureSuccessStatusCode();
            }

            return $"Patient {patientId} created with PCP.";
        }

        //public async Task<string> CreatePatientProfileAsync(PatientProfileInput input)
        //{
        //    var token = await GetAccessTokenAsync();
        //    var client = _httpClientFactory.CreateClient();
        //    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        //    client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

        //    // 1. Create Practitioner (PCP)
        //    var pcpPayload = new
        //    {
        //        resourceType = "Practitioner",
        //        name = new[]
        //        {
        //            new {
        //                family = input.Pcp.LastName,
        //                given = new[] { input.Pcp.FirstName }
        //            }
        //        },
        //        telecom = new[]
        //        {
        //            new {
        //                system = "email",
        //                value = input.Pcp.Email,
        //                use = "work"
        //            }
        //        },
        //        gender = input.Pcp.Gender
        //    };

        //    var pcpJson = JsonSerializer.Serialize(pcpPayload);
        //    var pcpRes = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Practitioner",
        //        new StringContent(pcpJson, Encoding.UTF8, "application/fhir+json"));
        //    pcpRes.EnsureSuccessStatusCode();
        //    var pcpId = JsonDocument.Parse(await pcpRes.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();

        //    // 2. Create Patient with PCP link
        //    var patientPayload = new
        //    {
        //        resourceType = "Patient",
        //        name = new[]
        //                {
        //                    new {
        //                        given = new[] { input.FirstName },
        //                        family = input.LastName
        //                    }
        //                },
        //        gender = input.Gender,
        //        birthDate = input.BirthDate,
        //        telecom = new[]
        //                    {
        //                        new {
        //                            system = "phone",
        //                            value = input.PhoneNumber,
        //                            use = "mobile"
        //                        },
        //                        new {
        //                            system = "email",
        //                            value = input.Email,   // <-- assuming input.Email is available
        //                            use = "home"
        //                        }
        //                    },
        //        address = new[]
        //                    {
        //                        new {
        //                            use = "home",
        //                            line = new[] { input.PatientAddress.AddressLine1  },
        //                            city = input.PatientAddress.City,
        //                            state = input.PatientAddress.State,
        //                            postalCode = input.PatientAddress.ZipCode,
        //                            country = input.PatientAddress.Country
        //                        }
        //                    },
        //        contact = new[]
        //                    {
        //                        new {
        //                            relationship = new[]
        //                            {
        //                                new {
        //                                    coding = new[]
        //                                    {
        //                                        new {
        //                                            system = "http://terminology.hl7.org/CodeSystem/v2-0131",
        //                                            code = "E",
        //                                            display = "Emergency"
        //                                        }
        //                                    }
        //                                }
        //                            },
        //                            name = new {
        //                                given = new[] { input.EmergencyContactFirstName },
        //                                family = input.EmergencyContactLastName
        //                            },
        //                            telecom = new[]
        //                            {
        //                                new {
        //                                    system = "phone",
        //                                    value = input.EmergencyContactPhone,
        //                                    use = "mobile"
        //                                }
        //                            }
        //                        }
        //                    },

        //        generalPractitioner = new[]
        //                                {
        //                                    new {
        //                                        reference = $"Practitioner/{pcpId}"
        //                                    }
        //                                }
        //    };

        //    var patientJson = JsonSerializer.Serialize(patientPayload);
        //    var patientRes = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Patient",
        //        new StringContent(patientJson, Encoding.UTF8, "application/fhir+json"));
        //    patientRes.EnsureSuccessStatusCode();
        //    var patientId = JsonDocument.Parse(await patientRes.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();

        //    //Vitals
        //    await UpsertVitals(client, patientId, input.VitalSigns);

        //    await UpsertSocialHistory(client, patientId, input.SocialHistories);

        //    await UpsertLifeStyle(client, patientId, input.LifestyleHistories);

        //    // 4. Insert Conditions if exists
        //    await UpsertPastConditions(client, patientId, input.PastConditions);

        //    return $"Patient {patientId} created with PCP.";
        //}

        public async Task<string> UpsertPatientProfileAsync(PatientProfileInput input)
        {
            var token = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var patientId = input.PatientId ?? string.Empty;
            var pcpId = input.Pcp.PractitionerId ?? string.Empty;

            // 1. Create Practitioner (PCP)
            if (string.IsNullOrEmpty(pcpId))
                pcpId = await CreatePractitioner(input.Pcp);
            else
                await UpdatePractitioner(input.Pcp);

            // 2. Create Patient with PCP link
            if (string.IsNullOrEmpty(patientId))
                patientId = await CreatePatient(input, pcpId);
            else
                await UpdatePatient(input, pcpId);

            var previousObs = await GetPatientObservationsAsync(patientId);
            var previousProfile = new PatientProfileInput();
            if (previousObs.Count > 0)
                ParseObservation(previousProfile, previousObs);

            //Vitals
            await SyncVitalSignsAsync(patientId, input.VitalSigns, previousProfile.VitalSigns);

            await SyncSocialHistoryAsync(patientId, input.SocialHistories, previousProfile.SocialHistories);

            await SyncLifeStyleAsync(patientId, input.LifestyleHistories, previousProfile.LifestyleHistories);

            // 4. Insert Conditions if exists
            await SyncConditionsAsync(patientId, input.PastConditions);

            await SyncConsentAsync(patientId, input.Consent);

            return patientId;
        }

        private async Task<string> CreatePractitioner(PractitionerInput practitioner)
        {
            var pcpPayload = new
            {
                resourceType = "Practitioner",
                name = new[]
                {
                    new {
                        family = practitioner.LastName,
                        given = new[] { practitioner.FirstName }
                    }
                },
                telecom = new[]
                {
                    new {
                        system = "email",
                        value = practitioner.Email,
                        use = "work"
                    }
                },
                gender = practitioner.Gender
            };

            return await FhirPostAsync("Practitioner", pcpPayload);
        }

        private async Task<string> UpdatePractitioner(PractitionerInput practitioner)
        {
            var pcpPayload = new
            {
                resourceType = "Practitioner",
                id = practitioner.PractitionerId,
                name = new[]
                {
                    new {
                        family = practitioner.LastName,
                        given = new[] { practitioner.FirstName }
                    }
                },
                telecom = new[]
                {
                    new {
                        system = "email",
                        value = practitioner.Email,
                        use = "work"
                    }
                },
                gender = practitioner.Gender
            };

            return await FhirPutAsync("Practitioner", pcpPayload, practitioner?.PractitionerId);
        }

        private async Task<string> CreatePatient(PatientProfileInput patient, string pcpId)
        {
            var patientPayload = new
            {
                resourceType = "Patient",
                name = new[]
                        {
                            new {
                                use = "official",
                                given = new[] { patient.FirstName },
                                family = patient.LastName
                            }
                        },
                gender = patient.Gender,
                birthDate = DateTime.Parse(patient.BirthDate).ToString("yyyy-MM-dd"),
                telecom = new[]
                            {
                                new {
                                    system = "phone",
                                    value = patient.PhoneNumber,
                                    use = "mobile"
                                },
                                new {
                                    system = "email",
                                    value = patient.Email,   // <-- assuming input.Email is available
                                    use = "home"
                                }
                            },
                address = new[]
                            {
                                new {
                                    use = "home",
                                    line = new[] { patient.PatientAddress.AddressLine1  },
                                    city = patient.PatientAddress.City,
                                    state = patient.PatientAddress.State,
                                    postalCode = patient.PatientAddress.ZipCode,
                                    country = patient.PatientAddress.Country
                                }
                            },
                contact = new[]
                            {
                                new {
                                    relationship = new[]
                                    {
                                        new {
                                            coding = new[]
                                            {
                                                new {
                                                    system = "http://terminology.hl7.org/CodeSystem/v2-0131",
                                                    code = "E",
                                                    display = "Emergency"
                                                }
                                            }
                                        }
                                    },
                                    name = new {
                                        given = new[] { patient.EmergencyContactFirstName },
                                        family = patient.EmergencyContactLastName
                                    },
                                    telecom = new[]
                                    {
                                        new {
                                            system = "phone",
                                            value = patient.EmergencyContactPhone,
                                            use = "mobile"
                                        }
                                    }
                                }
                            },

                generalPractitioner = new[]
                                        {
                                            new {
                                                reference = $"Practitioner/{pcpId}"
                                            }
                                        }
            };

            return await FhirPostAsync("Patient", patientPayload);
        }

        private async Task<string> UpdatePatient(PatientProfileInput patient, string pcpId)
        {
            var patientPayload = new
            {
                resourceType = "Patient",
                id = patient.PatientId,
                name = new[]
                        {
                            new {
                                use = "official",
                                given = new[] { patient.FirstName },
                                family = patient.LastName
                            }
                        },
                gender = patient.Gender,
                birthDate = DateTime.Parse(patient.BirthDate).ToString("yyyy-MM-dd"),
                telecom = new[]
                            {
                                new {
                                    system = "phone",
                                    value = patient.PhoneNumber,
                                    use = "mobile"
                                },
                                new {
                                    system = "email",
                                    value = patient.Email,   // <-- assuming input.Email is available
                                    use = "home"
                                }
                            },
                address = new[]
                            {
                                new {
                                    use = "home",
                                    line = new[] { patient.PatientAddress.AddressLine1  },
                                    city = patient.PatientAddress.City,
                                    state = patient.PatientAddress.State,
                                    postalCode = patient.PatientAddress.ZipCode,
                                    country = patient.PatientAddress.Country
                                }
                            },
                contact = new[]
                            {
                                new {
                                    relationship = new[]
                                    {
                                        new {
                                            coding = new[]
                                            {
                                                new {
                                                    system = "http://terminology.hl7.org/CodeSystem/v2-0131",
                                                    code = "E",
                                                    display = "Emergency"
                                                }
                                            }
                                        }
                                    },
                                    name = new {
                                        given = new[] { patient.EmergencyContactFirstName },
                                        family = patient.EmergencyContactLastName
                                    },
                                    telecom = new[]
                                    {
                                        new {
                                            system = "phone",
                                            value = patient.EmergencyContactPhone,
                                            use = "mobile"
                                        }
                                    }
                                }
                            },

                generalPractitioner = new[]
                                        {
                                            new {
                                                reference = $"Practitioner/{pcpId}"
                                            }
                                        }
            };

            return await FhirPutAsync("Patient", patientPayload, patient.PatientId);
        }

        private async Task SyncConditionsAsync(string patientId, List<ConditionInput> current)
        {
            if (string.IsNullOrEmpty(patientId))
                throw new ArgumentException("Patient ID is required for update.");

            var previous = await GetPatientConditionsAsync(patientId);
            // INSERT new conditions
            var toInsert = current
                .Where(c => string.IsNullOrEmpty(c.Id))
                .ToList();

            foreach (var condition in toInsert)
            {
                condition.Id = await CreateCondition(condition, patientId);
            }

            // DELETE unselected
            var toDelete = previous
                .Where(existing => current.All(c => c.Id != existing.Id))
                .ToList();

            foreach (var condition in toDelete)
            {
                if (!string.IsNullOrEmpty(condition.Id))
                {
                    await DeleteCondition(condition.Id);
                }
            }
        }

        private async Task<string> CreateCondition(ConditionInput condition, string patientId)
        {
            var conditionPayLoad = new
            {
                resourceType = "Condition",
                clinicalStatus = new
                {
                    coding = new[]
                        {
                            new {
                                system = "http://terminology.hl7.org/CodeSystem/condition-clinical",
                                code = "active"
                            }
                        }
                },
                verificationStatus = new
                {
                    coding = new[]
                        {
                            new {
                                system = "http://terminology.hl7.org/CodeSystem/condition-ver-status",
                                code = "confirmed"
                            }
                        }
                },
                subject = new { reference = $"Patient/{patientId}" },
                code = new
                {
                    coding = new[]
                        {
                            new {
                                system = "http://snomed.info/sct",
                                code = condition.Code,
                                display = condition.Display
                            }
                        }
                }
            };

            return await FhirPostAsync("Condition", conditionPayLoad);
        }

        private async Task DeleteCondition(string conditionId)
        {
            await FhirDeleteAsync("Condition", conditionId);
        }

        private async Task SyncVitalSignsAsync(string patientId, List<VitalSignsInput> current, List<VitalSignsInput> previous)
        {
            if (string.IsNullOrEmpty(patientId))
                throw new ArgumentException("Patient ID is required for update.");

            // UPDATE modified (if needed)
            foreach (var vital in current.Where(c => !string.IsNullOrEmpty(c.Id)))
            {
                var original = previous.FirstOrDefault(x => x.Id == vital.Id);
                if (original != null && (original.Value != vital.Value))
                {
                    await UpdateVital(vital, patientId);
                }
            }

            // INSERT new conditions
            var toInsert = current
                .Where(c => string.IsNullOrEmpty(c.Id))
                .ToList();

            foreach (var vital in toInsert)
            {
                vital.Id = await CreateVital(vital, patientId);
            }

            // DELETE unselected
            var toDelete = previous
                .Where(existing => current.All(c => c.Id != existing.Id))
                .ToList();

            foreach (var vital in toDelete)
            {
                if (!string.IsNullOrEmpty(vital.Id))
                {
                    await DeleteCondition(vital.Id);
                }
            }
        }

        private async Task<string> CreateVital(VitalSignsInput vital, string patientId)
        {
            var vitalPayLoad = new
            {
                resourceType = "Observation",
                status = "final",
                category = new[]
                    {
                        new {
                            coding = new[]
                            {
                                new {
                                    system = "http://terminology.hl7.org/CodeSystem/observation-category",
                                    code = "vital-signs",
                                    display = "Vital Signs"
                                }
                            }
                        }
                    },
                code = new
                {
                    coding = new[]
                        {
                            new {
                                system = "http://loinc.org",
                                code = vital.Code,
                                display = vital.Display
                            }
                        }
                },
                subject = new { reference = $"Patient/{patientId}" },
                effectiveDateTime = DateTime.UtcNow.ToString("o"),
                valueQuantity = new
                {
                    value = vital.Value ?? 0,
                    unit = vital.Unit,
                    system = "http://unitsofmeasure.org"
                }
            };

            return await FhirPostAsync("Observation", vitalPayLoad);
        }

        private async Task<string> UpdateVital(VitalSignsInput vital, string patientId)
        {
            var vitalPayLoad = new
            {
                resourceType = "Observation",
                id = vital.Id,
                status = "final",
                category = new[]
                    {
                        new {
                            coding = new[]
                            {
                                new {
                                    system = "http://terminology.hl7.org/CodeSystem/observation-category",
                                    code = "vital-signs",
                                    display = "Vital Signs"
                                }
                            }
                        }
                    },
                code = new
                {
                    coding = new[]
                        {
                            new {
                                system = "http://loinc.org",
                                code = vital.Code,
                                display = vital.Display
                            }
                        }
                },
                subject = new { reference = $"Patient/{patientId}" },
                effectiveDateTime = DateTime.UtcNow.ToString("o"),
                valueQuantity = new
                {
                    value = vital.Value ?? 0,
                    unit = vital.Unit,
                    system = "http://unitsofmeasure.org"
                }
            };

            return await FhirPutAsync("Observation", vitalPayLoad, vital.Id);
        }

        private async Task SyncSocialHistoryAsync(string patientId, List<SocialHistoryInput> current, List<SocialHistoryInput> previous)
        {
            if (string.IsNullOrEmpty(patientId))
                throw new ArgumentException("Patient ID is required for update.");

            // UPDATE modified (if needed)
            foreach (var social in current.Where(c => !string.IsNullOrEmpty(c.Id)))
            {
                var original = previous.FirstOrDefault(x => x.Id == social.Id);
                if (original != null && (original.StatusCode != social.StatusCode || original.StatusDisplay != social.StatusDisplay || original.StatusValue != social.StatusValue))
                {
                    await UpdateSocialHistory(social, patientId);
                }
            }

            // INSERT new conditions
            var toInsert = current
                .Where(c => string.IsNullOrEmpty(c.Id))
                .ToList();

            foreach (var social in toInsert)
            {
                social.Id = await CreateSocialHistory(social, patientId);
            }

            // DELETE unselected
            var toDelete = previous
                .Where(existing => current.All(c => c.Id != existing.Id))
                .ToList();

            foreach (var social in toDelete)
            {
                if (!string.IsNullOrEmpty(social.Id))
                {
                    await DeleteObservation(social.Id);
                }
            }
        }

        private async Task<string> CreateSocialHistory(SocialHistoryInput social, string patientId)
        {
            var payLoad = new Dictionary<string, object>
            {
                ["resourceType"] = "Observation",
                ["status"] = "final",
                ["category"] = new[]
                    {
                        new {
                            coding = new[]
                            {
                                new {
                                    system = "http://terminology.hl7.org/CodeSystem/observation-category",
                                    code = "social-history",
                                    display = "Social History"
                                }
                            }
                        }
                    },
                ["code"] = new
                {
                    coding = new[]
                        {
                            new {
                                system = "http://loinc.org",
                                code = social.BehaviorCode,
                                display = social.BehaviorName
                            }
                        }
                },
                ["subject"] = new { reference = $"Patient/{patientId}" },
                ["effectiveDateTime"] = DateTime.UtcNow.ToString("o"),
            };

            if (Convert.ToInt32(social.StatusValue) > 0)
            {
                payLoad["valueInteger"] = social.StatusValue;
            }
            else if (string.IsNullOrEmpty(social.StatusCode) && !string.IsNullOrEmpty(social.StatusDisplay))
            {
                payLoad["valueString"] = social.StatusDisplay;
            }
            else if (!string.IsNullOrEmpty(social.StatusCode) && !string.IsNullOrEmpty(social.StatusDisplay))
            {
                payLoad["valueCodeableConcept"] = new
                {
                    coding = new[]
                    {
                            new {
                                system = "http://snomed.info/sct",
                                code = social.StatusCode,
                                display = social.StatusDisplay
                            }
                        }
                };
            }

            return await FhirPostAsync("Observation", payLoad);
        }

        private async Task<string> UpdateSocialHistory(SocialHistoryInput social, string patientId)
        {
            var payLoad = new Dictionary<string, object>
            {
                ["resourceType"] = "Observation",
                ["id"] = social.Id,
                ["status"] = "final",
                ["category"] = new[]
                    {
                        new {
                            coding = new[]
                            {
                                new {
                                    system = "http://terminology.hl7.org/CodeSystem/observation-category",
                                    code = "social-history",
                                    display = "Social History"
                                }
                            }
                        }
                    },
                ["code"] = new
                {
                    coding = new[]
                        {
                            new {
                                system = "http://loinc.org",
                                code = social.BehaviorCode,
                                display = social.BehaviorName
                            }
                        }
                },
                ["subject"] = new { reference = $"Patient/{patientId}" },
                ["effectiveDateTime"] = DateTime.UtcNow.ToString("o"),
            };

            if (Convert.ToInt32(social.StatusValue) > 0)
            {
                payLoad["valueInteger"] = social.StatusValue;
            }
            else if (string.IsNullOrEmpty(social.StatusCode) && !string.IsNullOrEmpty(social.StatusDisplay))
            {
                payLoad["valueString"] = social.StatusDisplay;
            }
            else if (!string.IsNullOrEmpty(social.StatusCode) && !string.IsNullOrEmpty(social.StatusDisplay))
            {
                payLoad["valueCodeableConcept"] = new
                {
                    coding = new[]
                    {
                            new {
                                system = "http://snomed.info/sct",
                                code = social.StatusCode,
                                display = social.StatusDisplay
                            }
                        }
                };
            }

            return await FhirPutAsync("Observation", payLoad, social.Id);
        }

        private async Task SyncLifeStyleAsync(string patientId, List<LifestyleInput> current, List<LifestyleInput> previous)
        {
            if (string.IsNullOrEmpty(patientId))
                throw new ArgumentException("Patient ID is required for update.");

            // UPDATE modified (if needed)
            foreach (var life in current.Where(c => !string.IsNullOrEmpty(c.Id)))
            {
                var original = previous.FirstOrDefault(x => x.Id == life.Id);
                if (original != null && (original.StatusCode != life.StatusCode || original.StatusDisplay != life.StatusDisplay || original.StatusValue != life.StatusValue))
                {
                    await UpdateLifestyle(life, patientId);
                }
            }

            // INSERT new conditions
            var toInsert = current
                .Where(c => string.IsNullOrEmpty(c.Id))
                .ToList();

            foreach (var life in toInsert)
            {
                life.Id = await CreateLifeStyle(life, patientId);
            }

            // DELETE unselected
            var toDelete = previous
                .Where(existing => current.All(c => c.Id != existing.Id))
                .ToList();

            foreach (var life in toDelete)
            {
                if (!string.IsNullOrEmpty(life.Id))
                {
                    await DeleteObservation(life.Id);
                }
            }
        }

        private async Task<string> CreateLifeStyle(LifestyleInput lifeStyle, string patientId)
        {
            var payLoad = new Dictionary<string, object>
            {
                ["resourceType"] = "Observation",
                ["status"] = "final",
                ["category"] = new[]
                    {
                        new {
                            coding = new[]
                            {
                                new {
                                    system = "http://terminology.hl7.org/CodeSystem/observation-category",
                                    code = "lifestyle",
                                    display = "Lifestyle"
                                }
                            }
                        }
                    },
                ["code"] = new
                {
                    coding = new[]
                        {
                            new {
                                system = "http://loinc.org",
                                code = lifeStyle.LifestyleCode,
                                display = lifeStyle.LifestyleName
                            }
                        }
                },
                ["subject"] = new { reference = $"Patient/{patientId}" },
                ["effectiveDateTime"] = DateTime.UtcNow.ToString("o"),
            };

            if (Convert.ToInt32(lifeStyle.StatusValue) > 0)
            {
                payLoad["valueInteger"] = lifeStyle.StatusValue;
            }
            else if (string.IsNullOrEmpty(lifeStyle.StatusCode) && !string.IsNullOrEmpty(lifeStyle.StatusDisplay))
            {
                payLoad["valueString"] = lifeStyle.StatusDisplay;
            }
            else if (!string.IsNullOrEmpty(lifeStyle.StatusCode) && !string.IsNullOrEmpty(lifeStyle.StatusDisplay))
            {
                payLoad["valueCodeableConcept"] = new
                {
                    coding = new[]
                    {
                            new {
                                system = "http://snomed.info/sct",
                                code = lifeStyle.StatusCode,
                                display = lifeStyle.StatusDisplay
                            }
                        }
                };
            }

            return await FhirPostAsync("Observation", payLoad);
        }

        private async Task<string> UpdateLifestyle(LifestyleInput lifeStyle, string patientId)
        {
            var payLoad = new Dictionary<string, object>
            {
                ["resourceType"] = "Observation",
                ["id"] = lifeStyle.Id,
                ["status"] = "final",
                ["category"] = new[]
                    {
                        new {
                            coding = new[]
                            {
                                new {
                                    system = "http://terminology.hl7.org/CodeSystem/observation-category",
                                    code = "lifestyle",
                                    display = "Lifestyle"
                                }
                            }
                        }
                    },
                ["code"] = new
                {
                    coding = new[]
                        {
                            new {
                                system = "http://loinc.org",
                                code = lifeStyle.LifestyleCode,
                                display = lifeStyle.LifestyleName
                            }
                        }
                },
                ["subject"] = new { reference = $"Patient/{patientId}" },
                ["effectiveDateTime"] = DateTime.UtcNow.ToString("o"),
            };

            if (Convert.ToInt32(lifeStyle.StatusValue) > 0)
            {
                payLoad["valueInteger"] = lifeStyle.StatusValue;
            }
            else if (string.IsNullOrEmpty(lifeStyle.StatusCode) && !string.IsNullOrEmpty(lifeStyle.StatusDisplay))
            {
                payLoad["valueString"] = lifeStyle.StatusDisplay;
            }
            else if (!string.IsNullOrEmpty(lifeStyle.StatusCode) && !string.IsNullOrEmpty(lifeStyle.StatusDisplay))
            {
                payLoad["valueCodeableConcept"] = new
                {
                    coding = new[]
                    {
                            new {
                                system = "http://snomed.info/sct",
                                code = lifeStyle.StatusCode,
                                display = lifeStyle.StatusDisplay
                            }
                        }
                };
            }

            return await FhirPutAsync("Observation", payLoad, lifeStyle.Id);
        }

        private async Task DeleteObservation(string observationId)
        {
            await FhirDeleteAsync("Observation", observationId);
        }

        public async Task<Dictionary<string, string>> GetPredictionUsingAIAsync(string patientId)
        {

            var result = await MapObservationSummaryToHealthMetricsInputAsync(patientId);
            var jsonResult = JsonSerializer.Serialize(result);
            // can you assign the observation summary result to the HealthMetricsInput .
            //call python API local url 
            var client = _httpClientFactory.CreateClient();
            //transform the output of medplum observation into the required input payload to call teh predeiction api. 
            var content = new StringContent(jsonResult, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://predictive-api-368018650904.us-central1.run.app/predict_combined", content);
            //transform the prediction output data into response dictionary to send back to UI
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Error calling prediction API");
            }

            var predictionContent = await response.Content.ReadAsStringAsync();

            var predictionResults = JsonSerializer.Deserialize<Dictionary<string, double>>(predictionContent);

            if (predictionResults == null)
            {
                throw new Exception("Error deserializing prediction results");
            }
            var resultWithRiskStatus = new Dictionary<string, string>();

            foreach (var kvp in predictionResults)
            {
                string riskLevel = kvp.Value switch
                {
                    <= 0.25 => "Low Risk (Easy)",
                    <= 0.50 => "Moderate Risk",
                    <= 0.75 => "High Risk",
                    _ => "Very High Risk (Danger)"
                };

                resultWithRiskStatus[kvp.Key] = $"{kvp.Value:F2} - {riskLevel}";
            }

            return resultWithRiskStatus;
        }


        private async Task<HealthMetricsInput> MapObservationSummaryToHealthMetricsInputAsync(string patientId)
        {
            var observationSummaries = await GetPatientObservationsAsync(patientId);

            var healthMetricsInput = new HealthMetricsInput();

            foreach (var observation in observationSummaries)
            {
                switch (observation.CodeValue)
                {
                    case "93832-4": // Sleep Duration
                        healthMetricsInput.SleepDuration = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "80394-6": // Heart Rate Variability
                        healthMetricsInput.HeartRateVariability = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "8867-4": // Heart Rate
                        healthMetricsInput.Hrv = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "8480-6": // Blood Pressure
                        healthMetricsInput.SystolicBp = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "8462-4": // Blood Pressure
                        healthMetricsInput.DiastolicBp = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "59408-5": // SpO2
                        healthMetricsInput.Spo2 = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "8310-5": // Temperature
                        healthMetricsInput.BodyTemp = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "41981-2": // CaloriesBurned
                        healthMetricsInput.CaloriesBurned = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "41950-7": // Steps
                        healthMetricsInput.Steps = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "9279-1": // RespiratoryRate
                        healthMetricsInput.RespiratoryRate = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    //lab results
                    case "2339-0": // Blood Glucose
                        healthMetricsInput.FastingGlucose = Convert.ToInt32(observation.Value.Split(" ")[0]);
                        break;
                    case "2093-3": // cholesterol
                        healthMetricsInput.Cholesterol = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "4548-4": // hba1c
                        healthMetricsInput.Hba1c = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "2085-9": // hdl
                        healthMetricsInput.Hdl = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "2089-1": // ldl
                        healthMetricsInput.Ldl = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "2571-8": // triglycerides
                        healthMetricsInput.Triglycerides = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "1988-5": // vitamin_d
                        healthMetricsInput.VitaminD = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "2132-9": // vitamin_b12
                        healthMetricsInput.VitaminB12 = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "2498-4": // iron
                        healthMetricsInput.Iron = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "718-7": // hemoglobin
                        healthMetricsInput.Hemoglobin = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "789-8": // rbc_count
                        healthMetricsInput.RbcCount = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "6690-2": // wbc_count
                        healthMetricsInput.WbcCount = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "777-3": // platelet_count
                        healthMetricsInput.PlateletCount = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "3016-3": // tsh
                        healthMetricsInput.Tsh = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "3053-6": // t3
                        healthMetricsInput.T3 = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "3024-7": // t4
                        healthMetricsInput.T4 = Convert.ToDouble(observation.Value.Split(" ")[0]);
                        break;
                    case "FamilyHistory": // FamilyHistory
                        healthMetricsInput.FamilyHistory = Convert.ToInt32(1);
                        break;
                    case "Smoking": // Smoking
                        healthMetricsInput.Smoking = Convert.ToInt32(1);
                        break;
                    case "Alchohal": // Smoking
                        healthMetricsInput.Alchohal = Convert.ToInt32(1);
                        break;
                }
            }

            return healthMetricsInput;
        }

        public async Task<List<ObservationSummary>> GetPatientObservationsAsync(string patientId, ObservationFilterType filterType = ObservationFilterType.All)
        {
            var token = await GetAccessTokenAsync();
            //var client = _httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var rawObservations = new List<ObservationSummary>();
            var rootElement = await FhirGetAsync("Observation", $"subject=Patient/{patientId}&_sort=-date&_count=100");

            if (!rootElement.TryGetProperty("entry", out var entries))
                return rawObservations; // No observations found

            foreach (var entry in entries.EnumerateArray())
            {
                var resource = entry.GetProperty("resource");
                var observable = ParseObservation(resource, string.Empty, patientId);

                // 🔥 Apply filters
                bool include = filterType switch
                {
                    ObservationFilterType.All => true,
                    ObservationFilterType.WearableVitals => observable.Categories.Contains(_catVitalSign),
                    ObservationFilterType.SocialHistory => observable.Categories.Contains(_catSocialHistory),
                    ObservationFilterType.Activity => observable.Categories.Contains("activity"),
                    ObservationFilterType.Survey => observable.Categories.Contains("survey"),
                    ObservationFilterType.Lifestyle => observable.Categories.Contains(_catLifeStyle),
                    ObservationFilterType.Exam => observable.Categories.Contains("exam"),
                    _ => true
                };

                if (include)
                {
                    rawObservations.Add(observable);
                }
            }

            // ✅ Group by CodeValue and take latest by EffectiveDateTime
            var latestObservations = rawObservations
               .Where(o => !string.IsNullOrEmpty(o.CodeValue) && DateTime.TryParse(o.EffectiveDateTime, out _))
               .GroupBy(o => o.CodeValue)
               .Select(g =>
               g.OrderByDescending(o => DateTime.Parse(o.EffectiveDateTime))
               .First())
               .ToList();

            return latestObservations;
        }

        private async Task<List<ConditionInput>> GetPatientConditionsAsync(string patientId)
        {
            //var response = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Condition?patient=Patient/{patientId}");
            //response.EnsureSuccessStatusCode();

            //var content = await response.Content.ReadAsStringAsync();

            //var jsonDoc = JsonDocument.Parse(content);

            var rawConditions = new List<ConditionInput>();
            var rootElement = await FhirGetAsync("Condition", $"patient=Patient/{patientId}");

            if (!rootElement.TryGetProperty("entry", out var entries))
                return rawConditions; // No observations found

            foreach (var entry in entries.EnumerateArray())
            {
                var resource = entry.GetProperty("resource");
                var id = resource.GetProperty("id").GetString();
                var codingArray = resource.GetProperty("code").GetProperty("coding");

                var firstCoding = codingArray[0];
                var codeDisplay = firstCoding.GetProperty("display").GetString();
                var codeSystem = firstCoding.GetProperty("system").GetString();
                var codeValue = firstCoding.GetProperty("code").GetString();

                var effectiveDateTimeStr = resource.TryGetProperty("effectiveDateTime", out var effTime) ? effTime.GetString() : null;
                DateTime.TryParse(effectiveDateTimeStr, out var effectiveDateTime);


                rawConditions.Add(new ConditionInput
                {
                    Id = id ?? string.Empty,
                    Code = codeValue ?? string.Empty,
                    Display = codeDisplay ?? "Unknown"
                });
            }

            return rawConditions;
        }

        private async Task<List<ConsentInput>> GetPatientConsentAsync(string patientId)
        {
            var rawConsent = new List<ConsentInput>();
            var rootElement = await FhirGetAsync("Consent", $"patient=Patient/{patientId}");

            if (!rootElement.TryGetProperty("entry", out var entries))
                return rawConsent; // No Consent found

            foreach (var entry in entries.EnumerateArray())
            {
                var resource = entry.GetProperty("resource");
                var id = resource.GetProperty("id").GetString();
                var scopeArray = resource.GetProperty("scope").GetProperty("coding");

                var firstCoding = scopeArray[0];
                var codeDisplay = firstCoding.GetProperty("display").GetString();
                var codeSystem = firstCoding.GetProperty("system").GetString();
                var codeValue = firstCoding.GetProperty("code").GetString();

                var policyArray = resource.GetProperty("policyRule").GetProperty("coding");
                var policycodeValue = policyArray[0].GetProperty("code").GetString();

                var effectiveDateTimeStr = resource.TryGetProperty("dateTime", out var effTime) ? effTime.GetString() : null;
                DateTime.TryParse(effectiveDateTimeStr, out var effectiveDateTime);

                rawConsent.Add(new ConsentInput
                {
                    Id = id ?? string.Empty,
                    Code = codeValue ?? string.Empty,
                    Display = codeDisplay ?? "Unknown",
                    IsSelected = policycodeValue?.ToLower() == "opt-in" ? true : false,
                });
            }

            return rawConsent;
        }

        public async Task<List<VitalTrendResult>> GetVitalsTrendAsync(string patientId)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7).ToString("o");

            var response = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Observation?subject=Patient/{patientId}&date=ge{sevenDaysAgo}&_count=200");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(content).RootElement;

            var vitals = new List<VitalTrendResult>();

            if (root.TryGetProperty("entry", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var resource = entry.GetProperty("resource");
                    var effectiveDate = resource.GetProperty("effectiveDateTime").GetString();
                    var timestamp = DateTime.Parse(effectiveDate!);

                    var codeElement = resource.GetProperty("code").GetProperty("coding")[0];
                    var code = codeElement.GetProperty("code").GetString();
                    var display = codeElement.GetProperty("display").GetString();

                    if (code == "8867-4" && resource.TryGetProperty("valueQuantity", out var heartRate)) // Heart Rate
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "HeartRate",
                            Value = (double)heartRate.GetProperty("value").GetDecimal()
                        });
                    }
                    else if (code == "85354-9" && resource.TryGetProperty("component", out var bpComponents)) // Blood Pressure
                    {
                        foreach (var comp in bpComponents.EnumerateArray())
                        {
                            var subCode = comp.GetProperty("code").GetProperty("coding")[0].GetProperty("code").GetString();
                            var subValue = comp.GetProperty("valueQuantity").GetProperty("value").GetDecimal();

                            if (subCode == "8480-6") // Systolic
                            {
                                vitals.Add(new VitalTrendResult
                                {
                                    Timestamp = timestamp,
                                    Type = "SystolicBP",
                                    Value = (double)subValue
                                });
                            }
                            else if (subCode == "8462-4") // Diastolic
                            {
                                vitals.Add(new VitalTrendResult
                                {
                                    Timestamp = timestamp,
                                    Type = "DiastolicBP",
                                    Value = (double)subValue
                                });
                            }
                        }
                    }
                    else if (code == "59408-5" && resource.TryGetProperty("valueQuantity", out var spo2)) // SpO2
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "SpO2",
                            Value = (double)spo2.GetProperty("value").GetDecimal()
                        });
                    }
                    else if (code == "8310-5" && resource.TryGetProperty("valueQuantity", out var temperature)) // Temperature
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "Temperature",
                            Value = (double)temperature.GetProperty("value").GetDecimal()
                        });
                    }
                    else if (code == "59267-0" && resource.TryGetProperty("valueQuantity", out var caloriesBurned)) // CaloriesBurned
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "CaloriesBurned",
                            Value = (double)caloriesBurned.GetProperty("value").GetDecimal()
                        });
                    }
                    else if (code == "41950-7" && resource.TryGetProperty("valueQuantity", out var steps)) // Steps
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "Steps",
                            Value = (double)steps.GetProperty("value").GetDecimal()
                        });
                    }
                    else if (code == "9279-1" && resource.TryGetProperty("valueQuantity", out var respiratoryRate)) // Respiratory Rate
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "RespiratoryRate",
                            Value = (double)respiratoryRate.GetProperty("value").GetDecimal()
                        });
                    }
                    else if (code == "2339-0" && resource.TryGetProperty("valueQuantity", out var glucose)) // Blood Glucose
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "BloodGlucose",
                            Value = (double)glucose.GetProperty("value").GetDecimal()
                        });
                    }
                    else if (code == "80394-6" && resource.TryGetProperty("valueQuantity", out var hrv)) // Heart Rate Variability
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "HeartRateVariability",
                            Value = (double)hrv.GetProperty("value").GetDecimal()
                        });
                    }
                    else if (code == "41918-4" && resource.TryGetProperty("valueQuantity", out var vo2max)) // VO2 Max
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "Vo2Max",
                            Value = (double)vo2max.GetProperty("value").GetDecimal()
                        });
                    }
                    else if (code == "93832-4" && resource.TryGetProperty("valueQuantity", out var sleepDuration)) // Sleep Duration
                    {
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "SleepDuration",
                            Value = (double)sleepDuration.GetProperty("value").GetDecimal()
                        });
                    }
                    // You can add more fields here if you have more wearable vitals ingested
                }
            }

            return vitals;
        }

        private void ParseObservation(PatientProfileInput patientProfile, List<ObservationSummary> observations)
        {
            //Category = "social-history"
            patientProfile.SocialHistories = observations
                .Where(o => o.Categories == _catSocialHistory)
                .Select(s => new SocialHistoryInput
                {
                    Id = s.Id,
                    BehaviorCode = s.CodeValue,
                    BehaviorName = s.CodeDisplay,
                    StatusDisplay = Convert.ToInt32(s.decValue) > 0 ? Convert.ToString(s.decValue) : s.Value,
                    StatusCode = s.StatusCode,
                    StatusValue = int.TryParse(s.Value, out int parsedValue) ? parsedValue : 0
                })
                .ToList();

            //Category = "lifestyle"
            patientProfile.LifestyleHistories = observations
                .Where(o => o.Categories == _catLifeStyle)
                .Select(s => new LifestyleInput
                {
                    Id = s.Id,
                    LifestyleCode = s.CodeValue,
                    LifestyleName = s.CodeDisplay,
                    StatusDisplay = Convert.ToInt32(s.decValue) > 0 ? Convert.ToString(s.decValue) : s.Value,
                    StatusCode = s.StatusCode,
                    StatusValue = int.TryParse(s.Value, out int parsedValue) ? parsedValue : 0
                })
                .ToList();

            //Category = "vital-signs"
            patientProfile.VitalSigns = observations
                .Where(o => o.Categories == _catVitalSign)
                .Select(s => new VitalSignsInput
                {
                    Id = s.Id,
                    Code = s.CodeValue,
                    Display = s.CodeDisplay,
                    Value = s.decValue,
                    Unit = s.unit ?? string.Empty
                })
                .ToList();
        }

        public async Task<string> SaveLabResultsAsync(LabResultsInput input)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var timestamp = input.CollectedDateTime?.ToString("o") ?? DateTime.UtcNow.ToString("o");

            var observations = new List<object>();

            // Helper to add lab Observation
            void AddLabObservation(string loincCode, string display, double value, string unit)
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "laboratory", display = "Laboratory" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://loinc.org", code = loincCode, display = display }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = value,
                        unit = unit,
                        system = "http://unitsofmeasure.org",
                        code = unit
                    }
                });
            }

            // HbA1c
            if (input.Hba1c.HasValue)
                AddLabObservation("4548-4", "Hemoglobin A1c/Hemoglobin.total in Blood", input.Hba1c.Value, "%");

            // Cholesterol Total
            if (input.TotalCholesterol.HasValue)
                AddLabObservation("2093-3", "Cholesterol [Mass/volume] in Serum or Plasma", input.TotalCholesterol.Value, "mg/dL");

            // HDL
            if (input.Hdl.HasValue)
                AddLabObservation("2085-9", "HDL Cholesterol [Mass/volume] in Serum or Plasma", input.Hdl.Value, "mg/dL");

            // LDL
            if (input.Ldl.HasValue)
                AddLabObservation("18262-6", "LDL Cholesterol [Mass/volume] in Serum or Plasma by calculation", input.Ldl.Value, "mg/dL");

            // Triglycerides
            if (input.Triglycerides.HasValue)
                AddLabObservation("2571-8", "Triglyceride [Mass/volume] in Serum or Plasma", input.Triglycerides.Value, "mg/dL");

            // Hemoglobin
            if (input.Hemoglobin.HasValue)
                AddLabObservation("718-7", "Hemoglobin [Mass/volume] in Blood", input.Hemoglobin.Value, "g/dL");

            // WBC
            if (input.Wbc.HasValue)
                AddLabObservation("6690-2", "Leukocytes [#/volume] in Blood by Automated count", input.Wbc.Value, "10^3/uL");

            // RBC
            if (input.Rbc.HasValue)
                AddLabObservation("789-8", "RBC [#/volume] in Blood", input.Rbc.Value, "10^3/ul");

            if (input.VitaminD.HasValue)
                AddLabObservation("1988-5", "Vitamin D", input.VitaminD.Value, "ng/mL");         // Vitamin D

            if (input.VitaminB12.HasValue)
                AddLabObservation("2132-9", "Vitamin B12", input.VitaminB12.Value, "pg/mL");        // Vitamin B12

            if (input.VitaminB12.HasValue)
                AddLabObservation("20570-8", "Iron", input.VitaminB12.Value, "µg/dL");               // Iron

            if (input.PlateletCount.HasValue)
                AddLabObservation("26515-7", "Platelet Count", input.PlateletCount.Value, "/uL");     // Platelet Count

            if (input.TSH.HasValue)
                AddLabObservation("3016-3", "TSH", input.TSH.Value, "uIU/mL");            // Thyroid Stimulating Hormone

            if (input.T3.HasValue)
                AddLabObservation("11579-0", "Triiodothyronine (T3)", input.T3.Value, "ng/mL"); // T3

            if (input.T4.HasValue)
                AddLabObservation("3024-7", "Thyroxine (T4)", input.T4.Value, "ug/dL");     // T4

            foreach (var obs in observations)
            {
                var json = JsonSerializer.Serialize(obs);
                var res = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Observation",
                    new StringContent(json, Encoding.UTF8, "application/fhir+json"));
                res.EnsureSuccessStatusCode();
            }

            return $"Saved {observations.Count} lab results for Patient/{input.PatientId}.";
        }

        public async Task<string> SaveImagingResultAsync(ImagingResultInput input)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var timestamp = input.CollectedDateTime?.ToString("o") ?? DateTime.UtcNow.ToString("o");

            var observation = new
            {
                resourceType = "Observation",
                status = "final",
                category = new[]
                {
            new
            {
                coding = new[]
                {
                    new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "imaging", display = "Imaging" }
                }
            }
        },
                code = new
                {
                    coding = new[]
                    {
                new { system = "http://loinc.org", code = input.LoincCode ?? "18748-4", display = input.ImagingType ?? "Radiology Study Observation" }
            }
                },
                subject = new { reference = $"Patient/{input.PatientId}" },
                effectiveDateTime = timestamp,
                valueString = input.ResultSummary
            };

            var json = JsonSerializer.Serialize(observation);
            var res = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Observation",
                new StringContent(json, Encoding.UTF8, "application/fhir+json"));
            res.EnsureSuccessStatusCode();

            return $"Saved imaging result for Patient/{input.PatientId}.";
        }

        public async Task<string> SaveProvidersReportedObservationsAsync(ProvidersReportedObservationsInput input)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var timestamp = input.CollectedDateTime?.ToString("o") ?? DateTime.UtcNow.ToString("o");

            var observations = new List<object>();
            var pcpId = input.ProviderId;
            // 1. Survey Response (PHQ-9 Depression Score)
            if (input.Phq9Score.HasValue)
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    performer = new[] { new { reference = $"Practitioner/{pcpId}" } },
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "survey", display = "Survey" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://loinc.org", code = "44249-1", display = "PHQ-9 total score" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = input.Phq9Score.Value,
                        unit = "score",
                        system = "http://unitsofmeasure.org",
                        code = "{score}"
                    }
                });
            }

            if (!string.IsNullOrEmpty(input.StressLevel))
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    performer = new[] { new { reference = $"Practitioner/{pcpId}" } },
                    category = new[] {
    new {
        coding = new[] {
            new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "survey" }
        }
    }
},
                    code = new
                    {
                        coding = new[] {
              new { system = "http://example.org/custom", code = "stress-level", display = "Stress level" }
          }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueString = input.StressLevel
                });

            // 2. Physical Exam Findings (e.g., Lung Sounds: Clear)
            if (!string.IsNullOrEmpty(input.PhysicalExamFinding))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    performer = new[] { new { reference = $"Practitioner/{pcpId}" } },
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "exam", display = "Physical Exam" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://example.org/custom", code = "lung-sounds", display = "Lung Sounds" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueString = input.PhysicalExamFinding
                });
            }

            // 3. Social History Observations (Smoking, Alcohol, Occupation)
            if (!string.IsNullOrEmpty(input.SmokingStatus))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    performer = new[] { new { reference = $"Practitioner/{pcpId}" } },
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "social-history", display = "Social History" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://loinc.org", code = "72166-2", display = "Tobacco smoking status" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueString = input.SmokingStatus
                });
            }

            if (!string.IsNullOrEmpty(input.AlcoholUse))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    performer = new[] { new { reference = $"Practitioner/{pcpId}" } },
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "social-history", display = "Social History" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://loinc.org", code = "74013-4", display = "Alcohol use" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueString = input.AlcoholUse
                });
            }

            if (!string.IsNullOrEmpty(input.Occupation))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    performer = new[] { new { reference = $"Practitioner/{pcpId}" } },
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "social-history", display = "Social History" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://loinc.org", code = "11341-5", display = "Occupation" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueString = input.Occupation
                });
            }

            // 4. Lifestyle Observations (Exercise, Diet)
            if (!string.IsNullOrEmpty(input.ExerciseFrequency))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    performer = new[] { new { reference = $"Practitioner/{pcpId}" } },
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                        new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "lifestyle", display = "Lifestyle" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://example.org/custom", code = "exercise-frequency", display = "Exercise Frequency" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueString = input.ExerciseFrequency
                });
            }

            if (!string.IsNullOrEmpty(input.DietHabits))
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    performer = new[] { new { reference = $"Practitioner/{pcpId}" } },
                    category = new[]
                    {
                new
                {
                    coding = new[]
                    {
                      new { system = "http://terminology.hl7.org/CodeSystem/observation-category", code = "lifestyle", display = "Lifestyle" }
                    }
                }
            },
                    code = new
                    {
                        coding = new[]
                        {
                    new { system = "http://example.org/custom", code = "diet-habits", display = "Diet Habits" }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueString = input.DietHabits
                });
            }

            // Post all observations
            foreach (var obs in observations)
            {
                var json = JsonSerializer.Serialize(obs);
                var res = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Observation",
                    new StringContent(json, Encoding.UTF8, "application/fhir+json"));
                res.EnsureSuccessStatusCode();
            }

            return $"Saved {observations.Count} additional observations for Patient/{input.PatientId}.";
        }

        public async Task<Dictionary<string, List<ObservationSummary>>> GetProviderReportedObservationsByCategoryAsync(string patientId)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var categories = new List<string>
            {
                 "vital-signs",
                 "social-history",
                 "activity",
                 "survey",
                 "lifestyle",
                 "exam" // custom for physical exams
            };

            var result = new Dictionary<string, List<ObservationSummary>>();

            foreach (var category in categories)
            {
                var observations = new List<ObservationSummary>();

                var response = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Observation?subject=Patient/{patientId}&category={category}&_sort=-date&_count=100");
                if (!response.IsSuccessStatusCode)
                {
                    // If category not found (404), skip it
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);

                if (!jsonDoc.RootElement.TryGetProperty("entry", out var entries))
                {
                    result[category] = new List<ObservationSummary>();
                    continue;
                }

                foreach (var entry in entries.EnumerateArray())
                {
                    var resource = entry.GetProperty("resource");
                    var codingArray = resource.GetProperty("code").GetProperty("coding");
                    var firstCoding = codingArray[0];
                    var codeDisplay = firstCoding.GetProperty("display").GetString();
                    var codeSystem = firstCoding.GetProperty("system").GetString();
                    var codeValue = firstCoding.GetProperty("code").GetString();
                    var effectiveDateTime = resource.TryGetProperty("effectiveDateTime", out var effTime) ? effTime.GetString() : null;

                    string value = "";
                    if (resource.TryGetProperty("valueQuantity", out var valueQuantity))
                        value = $"{valueQuantity.GetProperty("value").GetDouble()} {valueQuantity.GetProperty("unit").GetString()}";
                    else if (resource.TryGetProperty("valueString", out var valueString))
                        value = valueString.GetString();
                    else if (resource.TryGetProperty("valueInteger", out var valueInt))
                        value = valueInt.GetInt32().ToString();

                    //string device = string.Empty;
                    //string performer = string.Empty;
                    //if (resource.TryGetProperty("device", out var deviceId))
                    //    device = deviceId.GetProperty("reference").GetString() ?? "";
                    //if (resource.TryGetProperty("performer", out var performerIdentity))
                    //    performer = performerIdentity.GetProperty("reference").GetString() ?? "";

                    observations.Add(new ObservationSummary
                    {
                        CodeDisplay = codeDisplay ?? "Unknown",
                        CodeSystem = codeSystem ?? "",
                        CodeValue = codeValue ?? "",
                        Categories = category,
                        Value = value,
                        // CapturedBy = !string.IsNullOrEmpty(device) ? device : (!string.IsNullOrEmpty(performer) ? performer : $"self/{patientId}"),
                        EffectiveDateTime = effectiveDateTime
                    });
                }

                result[category] = observations;
            }

            return result;
        }

        public async Task<PatientLabResultsOutput> GetPatientLabResultsAsync(string patientId)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var output = new PatientLabResultsOutput
            {
                GeneralLabs = new List<ObservationSummary>(),
                ImagingResults = new List<ObservationSummary>()
            };

            // Fetch General Lab Results (category=laboratory)
            var labResponse = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Observation?subject=Patient/{patientId}&category=laboratory&_sort=-date&_count=100");
            if (labResponse.IsSuccessStatusCode)
            {
                var content = await labResponse.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);

                if (jsonDoc.RootElement.TryGetProperty("entry", out var entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        var resource = entry.GetProperty("resource");
                        output.GeneralLabs.Add(ParseObservation(resource, "laboratory", patientId));
                    }
                }
            }

            // Fetch Imaging/Scan Results (category=imaging)
            var imagingResponse = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Observation?subject=Patient/{patientId}&category=imaging&_sort=-date&_count=100");
            if (imagingResponse.IsSuccessStatusCode)
            {
                var content = await imagingResponse.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);

                if (jsonDoc.RootElement.TryGetProperty("entry", out var entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        var resource = entry.GetProperty("resource");
                        output.ImagingResults.Add(ParseObservation(resource, "imaging", patientId));
                    }
                }
            }

            return output;
        }

        // Helper to parse Observation resource
        private ObservationSummary ParseObservation(JsonElement resource, string? category, string? patientId)
        {
            var id = resource.GetProperty("id").GetString();
            var codingArray = resource.GetProperty("code").GetProperty("coding");
            var firstCoding = codingArray[0];

            var codeDisplay = firstCoding.GetProperty("display").GetString();
            var codeSystem = firstCoding.GetProperty("system").GetString();
            var codeValue = firstCoding.GetProperty("code").GetString();
            var effectiveDateTime = resource.TryGetProperty("effectiveDateTime", out var effTime) ? effTime.GetString() : null;

            string value = ""; decimal? decValue = null; string statusCode = string.Empty; string? unit = string.Empty;
            if (resource.TryGetProperty("valueQuantity", out var valueQuantity))
            {
                value = $"{valueQuantity.GetProperty("value").GetDouble()} {valueQuantity.GetProperty("unit").GetString()}";
                decValue = valueQuantity.GetProperty("value").GetDecimal();
                unit = valueQuantity.GetProperty("unit").GetString();
            }
            else if (resource.TryGetProperty("valueCodeableConcept", out var valueCodeableConcept))
            {
                var codeableArray = resource.GetProperty("valueCodeableConcept").GetProperty("coding");
                var firstCode = codeableArray[0];
                value = firstCode.GetProperty("display").GetString() ?? string.Empty;
                statusCode = firstCode.GetProperty("code").GetString() ?? string.Empty;
            }
            else if (resource.TryGetProperty("valueString", out var valueString))
                value = valueString.GetString() ?? string.Empty;
            else if (resource.TryGetProperty("valueInteger", out var valueInt))
                value = valueInt.GetInt32().ToString();

            if (string.IsNullOrEmpty(category))
                category = resource.TryGetProperty("category", out var categoryArray) && categoryArray.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", categoryArray.EnumerateArray().Select(c => c.GetProperty("coding")[0].GetProperty("code").GetString()))
                    : "";

            string device = string.Empty;
            string performer = string.Empty;

            if (resource.TryGetProperty("device", out var deviceObj) && deviceObj.ValueKind == JsonValueKind.Object)
            {
                device = deviceObj.TryGetProperty("reference", out var refProp) ? refProp.GetString() ?? "" : "";
            }

            if (resource.TryGetProperty("performer", out var performerArray) && performerArray.ValueKind == JsonValueKind.Array)
            {
                var firstPerformer = performerArray.EnumerateArray().FirstOrDefault();
                performer = firstPerformer.ValueKind == JsonValueKind.Object &&
                            firstPerformer.TryGetProperty("reference", out var performerRef)
                    ? performerRef.GetString() ?? ""
                    : "";
            }

            return new ObservationSummary
            {
                Id = id ?? string.Empty,
                CodeDisplay = codeDisplay ?? "Unknown",
                CodeSystem = codeSystem ?? "",
                CodeValue = codeValue ?? "",
                Categories = category,
                CapturedBy = !string.IsNullOrEmpty(device) ? device : (!string.IsNullOrEmpty(performer) ? performer : (category.Equals("imaging") || category.Equals("laboratory")) ? $"lab/{patientId}" : $"self/{patientId}"),
                Value = value ?? "",
                decValue = decValue,
                unit = unit ?? string.Empty,
                StatusCode = statusCode,
                EffectiveDateTime = effectiveDateTime ?? DateTime.Now.ToString("o")
            };
        }

        private async Task SyncConsentAsync(string patientId, List<ConsentInput> current)
        {
            if (string.IsNullOrEmpty(patientId))
                throw new ArgumentException("Patient ID is required for update.");

            var previous = await GetPatientConsentAsync(patientId);
            // INSERT new conditions
            var toInsert = current
                .Where(c => string.IsNullOrEmpty(c.Id))
                .ToList();

            foreach (var consent in toInsert)
            {
                consent.Id = await CreateConsentAsync(patientId, consent);
            }

            // UPDATE modified (if needed)
            foreach (var consent in current.Where(c => !string.IsNullOrEmpty(c.Id)))
            {
                var original = previous.FirstOrDefault(x => x.Id == consent.Id);
                if (original != null && (original.IsSelected != consent.IsSelected))
                {
                    await UpdateConsentAsync(patientId, consent);
                }
            }

            // DELETE unselected
            var toDelete = previous
                .Where(existing => current.All(c => c.Id != existing.Id))
                .ToList();

            foreach (var consent in toDelete)
            {
                if (!string.IsNullOrEmpty(consent.Id))
                {
                    await DeleteConsentAsync(consent.Id);
                }
            }
        }

        public async Task<string> CreateConsentAsync(string patientId, ConsentInput consent)
        {
            var payLoad = new
            {
                resourceType = "Consent",
                status = "active",
                scope = new
                {
                    coding = new[]
                    {
                        new {
                            system = "http://terminology.hl7.org/CodeSystem/consentscope",
                            code = consent.Code,
                            display= consent.Display
                        }
                    }
                },
                category = new[]
                {
                    new {
                        coding = new[]
                        {
                            new {
                                system = "http://terminology.hl7.org/CodeSystem/consentcategorycodes",
                                code = consent.Code,
                                display = consent.Display
                            }
                        }
                    }
                },
                patient = new { reference = $"Patient/{patientId}" },
                dateTime = DateTime.UtcNow.ToString("o"),
                policyRule = new
                {
                    coding = new[]
                    {
                        new {
                            system = "http://terminology.hl7.org/CodeSystem/consentpolicy",
                            code = consent.IsSelected ? "opt-in" : "opt-out"
                        }
                    }
                }
            };

            var result = await FhirPostAsync("Consent", payLoad);
            return result;
        }

        public async Task<string> UpdateConsentAsync(string patientId, ConsentInput consent)
        {
            var payLoad = new
            {
                resourceType = "Consent",
                id = consent.Id,
                status = "active",
                scope = new
                {
                    coding = new[]
                    {
                        new {
                            system = "http://terminology.hl7.org/CodeSystem/consentscope",
                            code = consent.Code,
                            display= consent.Display
                        }
                    }
                },
                category = new[]
                {
                    new {
                        coding = new[]
                        {
                            new {
                                system = "http://terminology.hl7.org/CodeSystem/consentcategorycodes",
                                code = consent.Code,
                                display = consent.Display
                            }
                        }
                    }
                },
                patient = new { reference = $"Patient/{patientId}" },
                dateTime = DateTime.UtcNow.ToString("o"),
                policyRule = new
                {
                    coding = new[]
                    {
                        new {
                            system = "http://terminology.hl7.org/CodeSystem/consentpolicy",
                            code = consent.IsSelected ? "opt-in" : "opt-out"
                        }
                    }
                }
            };

            var result = await FhirPutAsync("Consent", payLoad, consent.Id);
            return result;
        }

        public async Task DeleteConsentAsync(string consentId)
        {
            await FhirDeleteAsync("Consent", consentId);
        }

        private async Task<JsonElement> FhirGetAsync(string resourceType, string query)
        {
            string apiUrl = $"{_apiBaseUrl}/{resourceType}";
            if (!string.IsNullOrEmpty(query))
                apiUrl = $"{_apiBaseUrl}/{resourceType}?{query}";

            var response = await _httpClient.GetAsync(apiUrl);

            response.EnsureSuccessStatusCode();
            var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            return jsonDoc;
        }

        private async Task<string> FhirPostAsync(string resourceType, object objData)
        {
            var obsJson = JsonSerializer.Serialize(objData);
            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/{resourceType}",
                new StringContent(obsJson, Encoding.UTF8, "application/fhir+json"));

            response.EnsureSuccessStatusCode();
            var id = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();
            return id ?? string.Empty;
        }

        private async Task<string> FhirPutAsync(string resourceType, object objData, string id)
        {
            var obsJson = JsonSerializer.Serialize(objData);
            var response = await _httpClient.PutAsync($"{_apiBaseUrl}/{resourceType}/{id}",
                new StringContent(obsJson, Encoding.UTF8, "application/fhir+json"));

            response.EnsureSuccessStatusCode();
            return id ?? string.Empty;
        }

        public async Task FhirDeleteAsync(string resourceType, string id)
        {
            var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/{resourceType}/{id}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<AlarmNotification>> GetAlarmNotification(string patientId)
        {
            //var result = await _twilioService.SendSmsAsync(notificationRequest.PhoneNumber, notificationRequest.Message);

            // logic to set the s3 location with patientid and current date
            var dateToken = DateTime.UtcNow.ToString("yyyy/MM/dd");
            var s3location = $"s3://vh-alarm-notifications/{patientId}/{dateToken}/";
            //s3://vh-alarm-notifications/sadsd1212/2025/06/21/
            //s3://vh-alarm-notifications/01978609-4506-72a9-a00e-8083bbf66207/2025/06/21/
            //read the all the json files from the s3 location and filter the last 10 latest files based on the file named with created timestamp 
            var alarmNotificationList = await _s3alarmReader.GetAlarmNotification(patientId);
            return alarmNotificationList;
        }
    
        public async Task<string> CreatePatientPrescriptionAsync(Prescription prescription)
        {
            //prescription.PrescriptionId ??= Guid.NewGuid().ToString();
            // prescription.DateWritten = DateTime.UtcNow;
            var token = await GetAccessTokenAsync();

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            foreach (var medOrder in prescription.Medications)
            {
                // Build FHIR MedicationRequest resource for EACH medication
                var noteList = new List<object>();

                if (prescription.PharmacyInstructions != null)
                {
                    noteList.AddRange(prescription.PharmacyInstructions.Select(i => new { text = i }));
                }

                if (prescription.Warnings != null)
                {
                    noteList.AddRange(prescription.Warnings.Select(w => new { text = w }));
                }

                if (prescription.Pharmacy != null)
                {
                    noteList.Add(new
                    {
                        text = $"Pharmacy: {prescription.Pharmacy.Name}, Address: {prescription.Pharmacy.Address}, Phone: {prescription.Pharmacy.Phone}"
                    });
                }

                var medicationRequest = new
                {
                    resourceType = "MedicationRequest",
                    status = "active",
                    intent = "order",
                    authoredOn = DateTime.UtcNow,
                    subject = new
                    {
                        reference = $"Patient/{prescription.Patient.PatientId}",
                        display = $"{prescription.Patient.FirstName} {prescription.Patient.LastName}"
                    },
                    requester = new
                    {
                        display = prescription.Prescriber.Name
                    },
                    medicationCodeableConcept = new
                    {
                        text = medOrder.Medication.Name
                    },
                    dosageInstruction = new[]
                    {
                        new
                        {
                            text = $"{medOrder.Directions} for {medOrder.Duration}"
                        }
                    },
                    dispenseRequest = new
                    {
                        quantity = new
                        {
                            value = medOrder.Quantity.Amount,
                            unit = medOrder.Quantity.Unit
                        },
                        numberOfRepeatsAllowed = medOrder.Refills
                    },
                    note = noteList
                };

                // Serialize and POST
                var json = JsonSerializer.Serialize(medicationRequest);
                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/MedicationRequest",
                    new StringContent(json, Encoding.UTF8, "application/fhir+json"));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to create MedicationRequest: {response.StatusCode} - {error}");
                }
            }

            return "All medications submitted successfully to Medplum.";
        }
        public async Task<List<JsonElement>> GetPatientPrescriptionsAsync(string patientId)
        {
            var token = await GetAccessTokenAsync();

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            var url = $"{_apiBaseUrl}/MedicationRequest?subject=Patient/{patientId}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to fetch prescriptions: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content).RootElement;

            var prescriptions = new List<JsonElement>();

            if (json.TryGetProperty("entry", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    prescriptions.Add(entry.GetProperty("resource"));
                }
            }

            return prescriptions;
        }
       
    }
}
