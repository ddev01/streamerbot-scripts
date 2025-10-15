using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

// ===== CONFIGURATION =====
public static class TriviaConfig
{
    public const string ANSWER_CHECKER_ACTION_ID = "174c7412-3436-4169-a7cf-720f8e205f8a";
    public const string TRIVIA_API_URL =
        "https://the-trivia-api.com/v2/questions?limit=1&difficulty=easy";
    public const int TRIVIA_DURATION_SECONDS = 30;
    public const int POINTS_REWARD = 1500;
}

// ===== API MODELS =====
public class TriviaQuestion
{
    public string Category { get; set; }
    public string Id { get; set; }
    public string CorrectAnswer { get; set; }
    public List<string> IncorrectAnswers { get; set; }
    public QuestionText Question { get; set; }
    public List<string> Tags { get; set; }
    public string Type { get; set; }
    public string Difficulty { get; set; }
}

public class QuestionText
{
    public string Text { get; set; }
}

public class CPHInline
{
    // Global variable keys (constants for maintainability)
    private const string KEY_TRIVIA_ACTIVE = "triviaActive";
    private const string KEY_TRIVIA_WON = "triviaWon";
    private const string KEY_CORRECT_ANSWER = "triviaCorrectAnswer";
    private const string KEY_CORRECT_TEXT = "triviaCorrectText";
    private const string KEY_PARTICIPATED_USERS = "triviaParticipatedUsers";

    public bool Execute()
    {
        try
        {
            // Check if trivia is already active
            if (CPH.GetGlobalVar<bool>(KEY_TRIVIA_ACTIVE, false))
            {
                CPH.SendMessage("🎯 Trivia is already active! Please wait for it to finish.");
                return false;
            }

            // Fetch trivia question from API
            var triviaData = FetchTriviaQuestion().Result;
            if (triviaData == null || triviaData.Count == 0)
            {
                CPH.SendMessage("❌ Failed to fetch trivia question. Please try again.");
                return false;
            }

            var question = triviaData[0];

            // Create and shuffle answer options (A, B, C, D)
            var shuffledAnswers = CreateShuffledAnswers(question);
            var correctAnswerLetter = GetCorrectAnswerLetter(
                question.CorrectAnswer,
                shuffledAnswers
            );

            // Store trivia data in global variables (non-persistent)
            SetTriviaState(correctAnswerLetter, question.CorrectAnswer);

            // Send trivia question to chat
            SendTriviaQuestion(question, shuffledAnswers);
            // Enable the answer checker action
            CPH.EnableActionById(TriviaConfig.ANSWER_CHECKER_ACTION_ID);

            // Wait for answer window
            CPH.Wait(TriviaConfig.TRIVIA_DURATION_SECONDS * 1000);

            // Check if someone won
            if (!CPH.GetGlobalVar<bool>(KEY_TRIVIA_WON, false))
            {
                // Nobody won - reveal the answer
                CPH.SendMessage(
                    $"⏰ Time's up! Nobody got it right. The correct answer was {correctAnswerLetter}: {question.CorrectAnswer}"
                );
            }

            // Clean up
            CleanupTrivia();

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"Trivia error: {ex.Message}");
            CPH.SendMessage("❌ An error occurred while setting up trivia. Please try again.");
            CleanupTrivia();
            return false;
        }
    }

    public bool TriviaAnswerChecker()
    {
        try
        {
            // Early exit if trivia is not active or already won
            if (!CPH.GetGlobalVar<bool>(KEY_TRIVIA_ACTIVE, false))
                return true;
            if (CPH.GetGlobalVar<bool>(KEY_TRIVIA_WON, false))
                return true;

            // Get user input
            var (userMessage, username, userId) = GetUserInput();
            if (string.IsNullOrEmpty(userMessage) || string.IsNullOrEmpty(username))
                return true;

            // Only process single letter answers (A, B, C, D)
            string trimmedMessage = userMessage.Trim().ToUpper();
            if (trimmedMessage.Length != 1 || !IsValidAnswer(trimmedMessage))
            {
                return true; // Not a valid trivia answer, ignore
            }

            // Check if user has already participated
            if (HasUserParticipated(username))
            {
                return true; // User already made a guess, ignore this message
            }

            // Add user to participated list
            AddUserToParticipated(username);

            // Get trivia state
            string correctAnswer = CPH.GetGlobalVar<string>(KEY_CORRECT_ANSWER, false);
            string correctText = CPH.GetGlobalVar<string>(KEY_CORRECT_TEXT, false);

            // Check if answer is correct
            if (trimmedMessage.Equals(correctAnswer, StringComparison.OrdinalIgnoreCase))
            {
                HandleCorrectAnswer(username, userId, correctAnswer, correctText);
            }
            else
            {
                // Send message for incorrect answer
                CPH.SendMessage($"❌ Sorry {username}, that's not correct!");
            }

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"TriviaAnswerChecker error: {ex.Message}");
            return true; // Don't break the chat message flow
        }
    }

    // ===== PRIVATE HELPER METHODS =====
    /// Sets the trivia state in global variables
    private void SetTriviaState(string correctAnswerLetter, string correctAnswerText)
    {
        CPH.SetGlobalVar(KEY_TRIVIA_ACTIVE, true, false);
        CPH.SetGlobalVar(KEY_TRIVIA_WON, false, false);
        CPH.SetGlobalVar(KEY_CORRECT_ANSWER, correctAnswerLetter, false);
        CPH.SetGlobalVar(KEY_CORRECT_TEXT, correctAnswerText, false);
    }

    /// Creates shuffled answer list from question
    private List<string> CreateShuffledAnswers(TriviaQuestion question)
    {
        var allAnswers = new List<string> { question.CorrectAnswer };
        allAnswers.AddRange(question.IncorrectAnswers);
        return ShuffleAnswers(allAnswers);
    }

    /// Extracts user input from args dictionary
    private (string message, string username, string userId) GetUserInput()
    {
        string message = args.ContainsKey("rawInput") ? args["rawInput"].ToString() : "";
        string username = args.ContainsKey("user") ? args["user"].ToString() : "";
        string userId = args.ContainsKey("userId") ? args["userId"].ToString() : "";
        return (message, username, userId);
    }

    /// Handles correct answer - awards points and announces winner
    private void HandleCorrectAnswer(
        string username,
        string userId,
        string correctAnswer,
        string correctText
    )
    {
        // Mark trivia as won
        CPH.SetGlobalVar(KEY_TRIVIA_WON, true, false);

        // Clear participated users list since someone won
        ClearParticipatedUsers();

        // Award points using Twitch user variables
        AwardPoints(userId, TriviaConfig.POINTS_REWARD);

        // Announce winner
        CPH.SendMessage(
            $"🎉 {username} got the right answer! It was {correctAnswer}: {correctText} and received {TriviaConfig.POINTS_REWARD:N0} points!"
        );

        // Disable answer checker
        CPH.DisableActionById(TriviaConfig.ANSWER_CHECKER_ACTION_ID);
    }

    /// Awards points to a user using Twitch user variables
    private void AwardPoints(string userId, int points)
    {
        string pointsVarName = "points";
        string currentPointsStr = CPH.GetTwitchUserVarById<string>(userId, pointsVarName, true);

        long currentPoints = 0;
        if (
            !string.IsNullOrEmpty(currentPointsStr)
            && long.TryParse(currentPointsStr, out currentPoints)
        )
        {
            currentPoints += points;
        }
        else
        {
            currentPoints = points;
        }

        CPH.SetTwitchUserVarById(userId, pointsVarName, currentPoints, true);
    }

    /// Fetches trivia question from The Trivia API
    private async Task<List<TriviaQuestion>> FetchTriviaQuestion()
    {
        try
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                var response = await httpClient.GetStringAsync(TriviaConfig.TRIVIA_API_URL);
                return JsonConvert.DeserializeObject<List<TriviaQuestion>>(response);
            }
        }
        catch (Exception ex)
        {
            CPH.LogError($"Failed to fetch trivia from API: {ex.Message}");
            return null;
        }
    }

    /// Shuffles answer list using Fisher-Yates algorithm
    private List<string> ShuffleAnswers(List<string> answers)
    {
        var shuffled = new List<string>(answers);
        var random = new Random();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    /// Finds which letter (A, B, C, D) corresponds to the correct answer
    private string GetCorrectAnswerLetter(string correctAnswer, List<string> shuffledAnswers)
    {
        int index = shuffledAnswers.IndexOf(correctAnswer);
        return index >= 0 ? ((char)('A' + index)).ToString() : "A";
    }

    /// Sends trivia question and answers to Twitch chat
    private void SendTriviaQuestion(TriviaQuestion question, List<string> answers)
    {
        string category = CapitalizeFirst(question.Category);
        CPH.SendMessage(
            $"🎯 TRIVIA TIME! Category: {category} | Respond with A, B, C, or D to win {TriviaConfig.POINTS_REWARD:N0} points!"
        );

        CPH.SendMessage($"❓ {question.Question.Text}");
        for (int i = 0; i < answers.Count; i++)
        {
            char letter = (char)('A' + i);
            CPH.SendMessage($"{letter}. {answers[i]}");
        }

        CPH.SendMessage($"⏱️ You have {TriviaConfig.TRIVIA_DURATION_SECONDS} seconds to answer!");
    }

    /// Capitalizes first letter of a string
    private string CapitalizeFirst(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        return char.ToUpper(text[0]) + text.Substring(1).ToLower();
    }

    /// Cleans up trivia state and disables answer checker
    private void CleanupTrivia()
    {
        CPH.DisableAction(TriviaConfig.ANSWER_CHECKER_ACTION_ID);
        CPH.UnsetGlobalVar(KEY_TRIVIA_ACTIVE, false);
        CPH.UnsetGlobalVar(KEY_TRIVIA_WON, false);
        CPH.UnsetGlobalVar(KEY_CORRECT_ANSWER, false);
        CPH.UnsetGlobalVar(KEY_CORRECT_TEXT, false);
        CPH.UnsetGlobalVar(KEY_PARTICIPATED_USERS, false);
    }

    /// Checks if a user has already participated in the current trivia
    private bool HasUserParticipated(string username)
    {
        string participatedUsersStr = CPH.GetGlobalVar<string>(KEY_PARTICIPATED_USERS, false);
        if (string.IsNullOrEmpty(participatedUsersStr))
            return false;
        var participatedUsers = participatedUsersStr.Split(',');
        return Array.Exists(
            participatedUsers,
            user => user.Equals(username, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// Adds a user to the participated users list
    private void AddUserToParticipated(string username)
    {
        string participatedUsersStr = CPH.GetGlobalVar<string>(KEY_PARTICIPATED_USERS, false);
        if (string.IsNullOrEmpty(participatedUsersStr))
        {
            CPH.SetGlobalVar(KEY_PARTICIPATED_USERS, username, false);
        }
        else
        {
            CPH.SetGlobalVar(KEY_PARTICIPATED_USERS, participatedUsersStr + "," + username, false);
        }
    }

    /// Clears the participated users list
    private void ClearParticipatedUsers()
    {
        CPH.UnsetGlobalVar(KEY_PARTICIPATED_USERS, false);
    }

    /// Validates if the answer is a valid trivia option (A, B, C, or D)
    private bool IsValidAnswer(string answer)
    {
        return answer == "A" || answer == "B" || answer == "C" || answer == "D";
    }
}
