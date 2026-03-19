using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using ResumePulse.Functions.Models;

namespace ResumePulse.Functions.Services
{
    public class GeminiService
    {
        private readonly Client _client;
        private readonly string _model;

        public GeminiService(string apiKey)
        {
            _client = new Client(apiKey: apiKey);
            _model = "gemini-3-flash-preview"; // Latest available model
        }

        public async Task<ResumeAnalysisReport> AnalyzeResumeAsync(
            string resumeText,
            string jobDescription,
            string jobId,
            string applicantEmail)
        {
            var jsonFormat = @"{
  ""overallScore"": 0,
  ""scoreSummary"": ""2-3 sentence summary"",
  ""matchedSkills"": [""skill1"", ""skill2""],
  ""missingSkills"": [""skill1"", ""skill2""],
  ""recommendation"": ""Strong Match / Good Match / Partial Match / Not a Match""
}";

            var prompt = $@"
You are an expert HR analyst.
Analyze the following resume against the job description.

JOB DESCRIPTION:
{jobDescription}

RESUME TEXT:
{resumeText}

Return ONLY a valid JSON object.
Do not add explanations, comments, markdown, or text outside the JSON.
Do not wrap the JSON in code fences.
Do not include trailing commas.
Do not invent extra fields.
The JSON must strictly match this schema:

{jsonFormat}

Output must be a single JSON object and nothing else.
";

            var contents = new List<Content>
            {
                new Content
                {
                    Role = "user",
                    Parts = new List<Part> { new Part { Text = prompt } }
                }
            };

            var config = new GenerateContentConfig
            {
                Temperature = 0.1,
                MaxOutputTokens = 1000,
                ResponseMimeType = "application/json"
            };

            string textContent = "";
            int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    textContent = "";
                    await foreach (var chunk in _client.Models.GenerateContentStreamAsync(_model, contents, config))
                    {
                        foreach (var candidate in chunk.Candidates)
                        {
                            foreach (var part in candidate.Content.Parts)
                            {
                                if (!string.IsNullOrEmpty(part.Text))
                                    textContent += part.Text;
                            }
                        }
                    }

                    // Sanitize: remove markdown and extract first JSON block
                    textContent = textContent
                        .Replace("```json", "")
                        .Replace("```", "")
                        .Trim();

                    var match = Regex.Match(textContent, @"\{.*\}", RegexOptions.Singleline);
                    if (match.Success)
                        textContent = match.Value;

                    // Try parsing — break if successful
                    JsonSerializer.Deserialize<JsonElement>(textContent);
                    break;
                }
                catch (Google.GenAI.ClientError ex) when (ex.Message.Contains("quota") || ex.Message.Contains("429"))
                {
                    var waitSeconds = attempt * 30;
                    Console.WriteLine($"[GeminiService] Quota exceeded. Waiting {waitSeconds}s before retry {attempt}/{maxRetries}...");
                    if (attempt < maxRetries)
                        await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                }
                catch (JsonException)
                {
                    Console.WriteLine($"[GeminiService] Invalid JSON on attempt {attempt}. Retrying...");
                    if (attempt < maxRetries)
                        await Task.Delay(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GeminiService] Error on attempt {attempt}: {ex.Message}");
                    if (attempt < maxRetries)
                        await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }

            // Safe parse with fallbacks — never crashes
            JsonElement result;
            try
            {
                result = JsonSerializer.Deserialize<JsonElement>(textContent);
            }
            catch
            {
                Console.WriteLine("[GeminiService] All retries failed. Returning default report.");
                return new ResumeAnalysisReport
                {
                    JobId = jobId,
                    ApplicantEmail = applicantEmail,
                    OverallScore = 0,
                    ScoreSummary = "Analysis could not be completed due to AI service limitations. Please try again.",
                    MatchedSkills = new List<string>(),
                    MissingSkills = new List<string>(),
                    Recommendation = "Not a Match",
                    GeneratedAt = DateTime.UtcNow
                };
            }

            // Safe property extraction with fallbacks
            return new ResumeAnalysisReport
            {
                JobId = jobId,
                ApplicantEmail = applicantEmail,
                OverallScore = SafeGetInt(result, "overallScore", 0),
                ScoreSummary = SafeGetString(result, "scoreSummary", "No summary available."),
                MatchedSkills = SafeGetStringList(result, "matchedSkills"),
                MissingSkills = SafeGetStringList(result, "missingSkills"),
                Recommendation = SafeGetString(result, "recommendation", "Not a Match"),
                GeneratedAt = DateTime.UtcNow
            };
        }

        private static int SafeGetInt(JsonElement element, string propertyName, int defaultValue)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number)
                        return prop.GetInt32();
                    if (prop.ValueKind == JsonValueKind.String &&
                        int.TryParse(prop.GetString(), out int parsed))
                        return parsed;
                }
            }
            catch { }
            return defaultValue;
        }

        private static string SafeGetString(JsonElement element, string propertyName, string defaultValue)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop))
                    return prop.GetString() ?? defaultValue;
            }
            catch { }
            return defaultValue;
        }

        private static List<string> SafeGetStringList(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop) &&
                    prop.ValueKind == JsonValueKind.Array)
                    return prop.EnumerateArray()
                               .Select(x => x.GetString() ?? "")
                               .Where(x => !string.IsNullOrEmpty(x))
                               .ToList();
            }
            catch { }
            return new List<string>();
        }
    }
}
