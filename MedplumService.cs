using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace VirtualHealthAPI
{
    public class MedplumService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public MedplumService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var client = _httpClientFactory.CreateClient();
            var tokenUrl = _config["Medplum:TokenUrl"];
            var clientId = _config["Medplum:ClientId"];
            var clientSecret = _config["Medplum:ClientSecret"];

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = "system/*.*"
            };

            var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(formData));
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var token = JsonDocument.Parse(json).RootElement.GetProperty("access_token").GetString();
            return token!;
        }

        public async Task<string> IngestWearableObservationsAsync(WearableVitalsInput input)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var timestamp = DateTime.UtcNow.ToString("o");
            var observations = new List<object>();

            if (input.HeartRate.HasValue)
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    category = new[] {
                new {
                    coding = new[] {
                        new {
                            system = "http://terminology.hl7.org/CodeSystem/observation-category",
                            code = "vital-signs"
                        }
                    }
                }
            },
                    code = new
                    {
                        coding = new[] {
                    new {
                        system = "http://loinc.org",
                        code = "8867-4",
                        display = "Heart rate"
                    }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = input.HeartRate.Value,
                        unit = "beats/minute",
                        system = "http://unitsofmeasure.org",
                        code = "/min"
                    }
                });
            }

            if (input.Systolic.HasValue && input.Diastolic.HasValue)
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    category = new[] {
                new {
                    coding = new[] {
                        new {
                            system = "http://terminology.hl7.org/CodeSystem/observation-category",
                            code = "vital-signs"
                        }
                    }
                }
            },
                    code = new
                    {
                        coding = new[] {
                    new {
                        system = "http://loinc.org",
                        code = "85354-9",
                        display = "Blood pressure"
                    }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    component = new[] {
                new {
                    code = new {
                        coding = new[] {
                            new { system = "http://loinc.org", code = "8480-6", display = "Systolic" }
                        }
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
                        coding = new[] {
                            new { system = "http://loinc.org", code = "8462-4", display = "Diastolic" }
                        }
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
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    code = new
                    {
                        coding = new[] {
                    new {
                        system = "http://loinc.org",
                        code = "59408-5",
                        display = "Oxygen saturation in Arterial blood by Pulse oximetry"
                    }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = input.Spo2.Value,
                        unit = "%",
                        system = "http://unitsofmeasure.org",
                        code = "%"
                    }
                });
            }

            if (input.Temperature.HasValue)
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    code = new
                    {
                        coding = new[] {
                    new {
                        system = "http://loinc.org",
                        code = "8310-5",
                        display = "Body temperature"
                    }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = input.Temperature.Value,
                        unit = "°F",
                        system = "http://unitsofmeasure.org",
                        code = "°F"
                    }
                });
            }

            if (input.Steps.HasValue)
            {
                observations.Add(new
                {
                    resourceType = "Observation",
                    status = "final",
                    code = new
                    {
                        coding = new[] {
                    new {
                        system = "http://loinc.org",
                        code = "41950-7",
                        display = "Number of steps in 24 hour Measured"
                    }
                }
                    },
                    subject = new { reference = $"Patient/{input.PatientId}" },
                    effectiveDateTime = timestamp,
                    valueQuantity = new
                    {
                        value = input.Steps.Value,
                        unit = "steps",
                        system = "http://unitsofmeasure.org",
                        code = "steps"
                    }
                });
            }

            foreach (var obs in observations)
            {
                var json = JsonSerializer.Serialize(obs);
                var res = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Observation",
                    new StringContent(json, Encoding.UTF8, "application/fhir+json"));
                res.EnsureSuccessStatusCode();
            }

            return $"Ingested {observations.Count} wearable vitals for Patient/{input.PatientId}.";
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

            // 3. Add Vitals (loop)
            foreach (var vital in input.Vitals)
            {
                var observationPayload = new
                {
                    resourceType = "Observation",
                    status = "final",
                    category = new[] {
                new {
                    coding = new[] {
                        new {
                            system = "http://terminology.hl7.org/CodeSystem/observation-category",
                            code = "vital-signs"
                        }
                    }
                }
            },
                    code = new
                    {
                        coding = new[] {
                    new {
                        system = "http://loinc.org",
                        code = vital.Type,
                        display = vital.Type
                    }
                }
                    },
                    subject = new { reference = $"Patient/{patientId}" },
                    effectiveDateTime = vital.Timestamp.ToString("o"),
                    valueQuantity = new
                    {
                        value = vital.Value,
                        unit = vital.Unit,
                        system = "http://unitsofmeasure.org",
                        code = vital.Unit
                    }
                };

                var obsJson = JsonSerializer.Serialize(observationPayload);
                var obsRes = await client.PostAsync($"{_config["Medplum:FhirUrl"]}/Observation",
                    new StringContent(obsJson, Encoding.UTF8, "application/fhir+json"));
                obsRes.EnsureSuccessStatusCode();
            }

            // 4. Fetch Conditions if exists
            foreach (var condition in input.Conditions)
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

            return $"Patient {patientId} created with PCP and {input.Vitals.Count} vitals.";
        }

        public async Task<List<ObservationResult>> GetCurrentObservationsAsync(string patientId)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            // Get latest Observations for this patient
            var response = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Observation?subject=Patient/{patientId}&_sort=-date&_count=50");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(content).RootElement;

            var observations = new List<ObservationResult>();

            if (root.TryGetProperty("entry", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var resource = entry.GetProperty("resource");
                    var codeDisplay = resource.GetProperty("code").GetProperty("coding")[0].GetProperty("display").GetString();
                    var observationTime = resource.GetProperty("effectiveDateTime").GetString();
                    string? value = null;

                    if (resource.TryGetProperty("valueQuantity", out var valueQuantity))
                    {
                        value = $"{valueQuantity.GetProperty("value")} {valueQuantity.GetProperty("unit")}";
                    }
                    else if (resource.TryGetProperty("component", out var components))
                    {
                        value = string.Join(", ", components.EnumerateArray().Select(comp =>
                            $"{comp.GetProperty("code").GetProperty("coding")[0].GetProperty("display").GetString()}: {comp.GetProperty("valueQuantity").GetProperty("value").GetDecimal()} {comp.GetProperty("valueQuantity").GetProperty("unit").GetString()}"
                        ));
                    }

                    observations.Add(new ObservationResult
                    {
                        Type = codeDisplay!,
                        Value = value ?? "N/A",
                        Timestamp = observationTime ?? "N/A"
                    });
                }
            }

            return observations;
        }

        public async Task<List<VitalTrendResult>> GetVitalsTrendAsync(string patientId)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7).ToString("o");

            var response = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Observation?subject=Patient/{patientId}&date=ge{sevenDaysAgo}&_count=100");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(content).RootElement;

            var vitals = new List<VitalTrendResult>();

            if (root.TryGetProperty("entry", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var resource = entry.GetProperty("resource");
                    var code = resource.GetProperty("code").GetProperty("coding")[0].GetProperty("code").GetString();
                    var effectiveDate = resource.GetProperty("effectiveDateTime").GetString();
                    DateTime timestamp = DateTime.Parse(effectiveDate!);

                    if (code == "8867-4") // Heart Rate
                    {
                        var value = resource.GetProperty("valueQuantity").GetProperty("value").GetDecimal();
                        vitals.Add(new VitalTrendResult
                        {
                            Timestamp = timestamp,
                            Type = "HeartRate",
                            Value = (double)value
                        });
                    }
                    else if (code == "85354-9") // Blood Pressure
                    {
                        foreach (var comp in resource.GetProperty("component").EnumerateArray())
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
                }
            }

            return vitals;
        }


        public async Task<PatientProfileInput> GetPatientFullProfileAsync(string patientId)
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("Accept", "application/fhir+json");

            // 1. Fetch Patient Resource
            var patientRes = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Patient/{patientId}");
            patientRes.EnsureSuccessStatusCode();
            var patientJson = JsonDocument.Parse(await patientRes.Content.ReadAsStringAsync()).RootElement;

            var patientProfile = new PatientProfileInput
            {
                PatientId = patientId,
                PatientName = $"{patientJson.GetProperty("name")[0].GetProperty("given")[0].GetString()} {patientJson.GetProperty("name")[0].GetProperty("family").GetString()}",
                FirstName= $"{patientJson.GetProperty("name")[0].GetProperty("given")[0].GetString()}",
                LastName= $"{patientJson.GetProperty("name")[0].GetProperty("family").GetString()}",
                BirthDate = patientJson.GetProperty("birthDate").GetString() ?? "",
                Gender = patientJson.GetProperty("gender").GetString() ?? ""
            };

            // 4. Fetch Practitioner (PCP) if exists
            if (patientJson.TryGetProperty("generalPractitioner", out var gpList))
            {
                var practitionerRef = gpList[0].GetProperty("reference").GetString(); // e.g., Practitioner/123
                if (practitionerRef != null)
                {
                    var practitionerId = practitionerRef.Split('/')[1];
                    var practitionerRes = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Practitioner/{practitionerId}");
                    practitionerRes.EnsureSuccessStatusCode();
                    var practitionerJson = JsonDocument.Parse(await practitionerRes.Content.ReadAsStringAsync()).RootElement;

                    patientProfile.Pcp = new PractitionerInput
                    {
                        PractitionerId = practitionerId,
                        PractitionerName = $"{practitionerJson.GetProperty("name")[0].GetProperty("given")[0].GetString()} {practitionerJson.GetProperty("name")[0].GetProperty("family").GetString()}",
                        FirstName = $"{practitionerJson.GetProperty("name")[0].GetProperty("given")[0].GetString()}",
                        LastName = $"{practitionerJson.GetProperty("name")[0].GetProperty("family").GetString()}",
                        Gender=  practitionerJson.GetProperty("gender").GetString()?? "",
                        Email = practitionerJson.TryGetProperty("telecom", out var telecom)
                                ? telecom[0].GetProperty("value").GetString() ?? ""
                                : ""
                    };
                }
            }



            // 3. Fetch Latest Vitals (limit to 10)
            var vitalsRes = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Observation?subject=Patient/{patientId}&_sort=-date&_count=10");
            vitalsRes.EnsureSuccessStatusCode();
            var vitalsJson = JsonDocument.Parse(await vitalsRes.Content.ReadAsStringAsync()).RootElement;

            if (vitalsJson.TryGetProperty("entry", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var resource = entry.GetProperty("resource");
                    var code = resource.GetProperty("code").GetProperty("coding")[0].GetProperty("display").GetString();
                    var effectiveDate = resource.GetProperty("effectiveDateTime").GetString();
                    DateTime timestamp = DateTime.Parse(effectiveDate ?? DateTime.UtcNow.ToString());

                    if (resource.TryGetProperty("valueQuantity", out var valueQuantity))
                    {
                        patientProfile.Vitals.Add(new VitalSignsInput
                        {
                            Type = code ?? "Unknown",
                            Value = valueQuantity.GetProperty("value").GetDouble(),
                            Unit = valueQuantity.GetProperty("unit").GetString() ?? "",
                            Timestamp = timestamp
                        });
                    }
                    else if (resource.TryGetProperty("component", out var components)) // BP
                    {
                        foreach (var component in components.EnumerateArray())
                        {
                            patientProfile.Vitals.Add(new VitalSignsInput
                            {
                                Type = component.GetProperty("code").GetProperty("coding")[0].GetProperty("display").GetString() ?? "Unknown",
                                Value = component.GetProperty("valueQuantity").GetProperty("value").GetDouble(),
                                Unit = component.GetProperty("valueQuantity").GetProperty("unit").GetString() ?? "",
                                Timestamp = timestamp
                            });
                        }
                    }
                }
            }

            // 4. Fetch Conditions
            var conditionRes = await client.GetAsync($"{_config["Medplum:FhirUrl"]}/Condition?subject=Patient/{patientId}&_count=50");
            conditionRes.EnsureSuccessStatusCode();
            var conditionJson = JsonDocument.Parse(await conditionRes.Content.ReadAsStringAsync()).RootElement;

            if (conditionJson.TryGetProperty("entry", out var conditionEntries))
            {
                foreach (var entry in conditionEntries.EnumerateArray())
                {
                    var resource = entry.GetProperty("resource");
                    var code = resource.GetProperty("code").GetProperty("coding")[0].GetProperty("display").GetString();
                    var codeValue = resource.GetProperty("code").GetProperty("coding")[0].GetProperty("code").GetString();

                    patientProfile.Conditions.Add(new ConditionInput
                    {
                        Code = codeValue ?? "Unknown",
                        Display = code ?? "Unknown Condition"
                    });
                }
            }

            return patientProfile;
        }


    }
}
