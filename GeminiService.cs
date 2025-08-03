namespace VirtualHealthAPI
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;

        public GeminiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetInsightFromPrompt(string prompt, string user_id)
        {
            var payload = JsonContent.Create(new { prompt, user_id });

            var response = await _httpClient.PostAsync(
                "https://gemini-langchain-api-907878265543.us-central1.run.app/generate", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API call failed. Status: {(int)response.StatusCode}, Body: {errorDetails}");
            }

            return await response.Content.ReadAsStringAsync();
        }

    }

}
