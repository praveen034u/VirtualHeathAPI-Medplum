namespace VirtualHealthAPI
{
    public class PromptLibraryService
    {
        private readonly Dictionary<string, string> _prompts;

        public PromptLibraryService()
        {
            _prompts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "How is my blood pressure trend?",
                    @"You are a virtual health assistant.

Here is the user's health data:
- Systolic blood pressure readings over the past 7 days: [{bp-sys-readings}]
- Diastolic blood pressure readings over the past 7 days: [{bp-dist-readings}]
- Data collected daily at 8 AM
- User is {age} years old, {gender}, with {prehypertension-flag} history of prehypertension
- Currently managing via lifestyle changes (not on medication)

User Query: How is my blood pressure trend?

Please analyze the trend and provide personalized feedback in a clinical but easy-to-understand manner.

Return the answer as clean HTML wrapped in a single <div> tag so it can be rendered directly in a web page. Include <p>, <ul>, <strong>, <h3> etc. to structure the response."
                },
                {
                    "Am I getting enough quality sleep?",
                    @"You are a virtual health assistant.

Here is the user's health data:
- Sleep duration over the past 7 days (in hours): [{sleep-duration-readings}]
- Sleep restlessness index over the past 7 days: [{sleep-restlessness-indexes}]
- Average bedtime: {average-bedtime}
- Wake-up time: {average-wake-up-time}
- User is {age} years old, {gender}, with no known sleep disorders

User Query: Am I getting enough quality sleep?

Please evaluate the user's sleep trends and provide clinical but easy-to-understand insights.

Return the answer as clean HTML wrapped in a single <div> tag so it can be rendered directly in a web page. Include <p>, <ul>, <strong>, <h3> etc. to structure the response."
                },
                {
                    "What can I do to reduce stress?",
                    @"You are a virtual health assistant.

Here is the user's health data:
- Stress indicator from wearable (based on HRV or reported scale) over the past 7 days: [{stress-readings}]
- Resting heart rate average: {resting-heart-rate} bpm
- Sleep quality score average: {sleep-score}
- User is {age} years old, {gender}, reports a {stress-level-description} level of daily stress due to work/lifestyle factors

User Query: What can I do to reduce stress?

Provide lifestyle, behavioral, and wellness suggestions supported by clinical best practices. Keep the response empathetic and practical.

Return the answer as clean HTML wrapped in a single <div> tag so it can be rendered directly in a web page. Include <p>, <ul>, <strong>, <h3> etc. to structure the response."
                },
                {
                    "How many steps should I take daily?",
                    @"You are a virtual health assistant.

Here is the user's health data:
- Average daily steps over the past 7 days: [{steps-readings}]
- Steps goal completion rate: {steps-goal-completion}%
- Physical activity level: {activity-level} (e.g., sedentary, moderately active)
- User is {age} years old, {gender}, with goal to improve general fitness and cardiovascular health

User Query: How many steps should I take daily?

Provide a personalized step goal recommendation based on current activity level, general health, and age. Include best practices and motivational tips.

Return the answer as clean HTML wrapped in a single <div> tag so it can be rendered directly in a web page. Include <p>, <ul>, <strong>, <h3> etc. to structure the response."
                },
                {
                    "Should I be concerned about my heart rate?",
                    @"You are a virtual health assistant.

Here is the user's health data:
- Average resting heart rate over the past 7 days: [{resting-heart-rate-readings}] bpm
- Maximum heart rate during exercise sessions: [{max-heart-rate-readings}] bpm
- Heart rate variability (HRV): {hrv-value}
- User is {age} years old, {gender}, with {cardiac-history-flag} history of cardiovascular issues

User Query: Should I be concerned about my heart rate?

Please evaluate the heart rate data in the context of general health and age. Identify any warning signs and recommend if further medical attention is necessary.

Return the answer as clean HTML wrapped in a single <div> tag so it can be rendered directly in a web page. Include <p>, <ul>, <strong>, <h3> etc. to structure the response."
                }
            };
        }

        public string GetPrimedPrompt(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt)) return null;

            // If prompt is found, return the associated primed prompt
            if (_prompts.TryGetValue(userPrompt.Trim(), out var primed))
                return primed;

            // Fallback for unknown prompt: send a meaningful response
            return @$"You are a virtual health assistant.

The user asked: ""{userPrompt}""

You do not have access to structured health data for this query, but you can still provide general, friendly and health-aware guidance based on your knowledge.

Answer as a helpful assistant, keeping your tone empathetic, accurate, and clinically reasonable.

Wrap the output inside a single <div> with clean HTML using <p>, <ul>, <h3> etc.";
        }
    }
}
