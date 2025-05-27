using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;

namespace Voidstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// ViewModel for AI Chat page with Roblox-focused chat functionality,
    /// including FFlags help and performance tips.
    /// </summary>
    public class AIChatPageViewModel : INotifyPropertyChanged
    {
        private string _userInput = string.Empty;
        private bool _isProcessing;
        private string _aiMood = "Neutral";
        private CancellationTokenSource? _processingCancellationTokenSource;

        private readonly List<string> _conversationHistory = new();
        private readonly SemaphoreSlim _messageQueue = new(1, 1); // To serialize messages

        public ObservableCollection<string> ChatMessages { get; } = new();

        // Tracks if AI is awaiting yes/no response for flags/help question
        private bool _awaitingYesNoResponse = false;
        private string _lastIntentCategory;

        public string UserInput
        {
            get => _userInput;
            set
            {
                if (_userInput != value)
                {
                    _userInput = value;
                    OnPropertyChanged();
                    SendMessageCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            private set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged();
                    SendMessageCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string AIMood
        {
            get => _aiMood;
            private set
            {
                if (_aiMood != value)
                {
                    _aiMood = value;
                    OnPropertyChanged();
                }
            }
        }



public ObservableCollection<string> AvailableCommands { get; set; } = new ObservableCollection<string>
{
    "Time",
    "Flag",
    "Date"
};




        public RelayCommand SendMessageCommand { get; }
        public RelayCommand ClearChatCommand { get; }
        public RelayCommand SaveChatLogCommand { get; }

        private const string ChatLogPath = "chat_log.txt";

        #region Regex Patterns and Responses
        private static readonly RegexOptions RegexOptionsCompiled = RegexOptions.IgnoreCase | RegexOptions.Compiled;

        private static readonly Regex GreetingRegex = new(@"\b(hello|hi|hey|greetings|yo|sup|what's up|howdy|roblox|hiya|yo bro|yo dude|hey there|heya|hey bot|sup bro|yo man|hello bot|wassup|hello there|hi there)\b", RegexOptionsCompiled);
        private static readonly Regex FarewellRegex = new(@"\b(bye|goodbye|see you|later|farewell|catch you later|see ya|take care|peace out|bye bye|g2g|talk to you later|until next time|catch ya later|ciao|adios|so long|ttyl|gn|night)\b", RegexOptionsCompiled);
        private static readonly Regex HelpRegex = new(@"\b(help|support|assist|problem|issue|question|need help|can you help|flag|fflag|pls help|any help|trouble|help me|fix this|solution|guide me|error|can't figure out|how to|bug|crash|don't understand)\b", RegexOptionsCompiled);
        private static readonly Regex AffirmationRegex = new(@"\b(good|great|fine|okay|ok|nice|cool|alright|awesome|amazing|not bad|all good|fantastic|superb|wonderful|solid|perfect|doing well|good enough|I’m fine|I’m good)\b", RegexOptionsCompiled);
        private static readonly Regex ThankRegex = new(@"\b(thank you|thanks|thx|ty|thankful|appreciate|nice|much obliged|cheers|tysm|grateful|big thanks|thx bro|thanks a lot|thank u|many thanks|tyvm|appreciated|props|thanks man)\b", RegexOptionsCompiled);
        private static readonly Regex QuestionRegex = new(@"\?$|^(what|why|how|when|where|who|is|are|can|should|could|would|do|does|question)\b.*\?$", RegexOptionsCompiled);
        private static readonly Regex SmallTalkRegex = new(@"\b(weather|time|date|joke|fun|day|night|mood|feeling|how are you|what's up|vibe|bored|got plans|doing anything|how's it going|hows ur day|feelin good|how you doin|tell me something|news)\b", RegexOptionsCompiled);
        private static readonly Regex MoodKeywordsRegex = new(@"\b(happy|sad|angry|excited|bored|tired|anxious|love|lonely|scared|joyful|confused|proud|grumpy|calm|relaxed|hopeful|worried|sick|depressed|motivated|thrilled)\b", RegexOptionsCompiled);
        private static readonly Regex HowAreYouRegex = new(@"\b(how are you|how’s it going|how do you feel|are you good|you doing okay|you alright|how u doing|are you fine|you feeling okay|u good)\b", RegexOptionsCompiled);
        private static readonly Regex NeedFlagsRegex = new(@"\b(i need|want|looking for|give me|can i get|need help with|gemme|i’d like|i wish for|share some|send me|can you share|recommend|any idea for|boost my|how to get|download|access to|get better|improve my)\b.*\b(flags|fflags|fps boost|performance|boost|improve|optimization|optimize|fast flags|ff|laggy|lag fix|tweaks|tweak settings|render boost|network boost|roblox speed|low latency)\b", RegexOptionsCompiled);
        private static readonly Regex YesRegex = new(@"\b(yes|yeah|yep|y|sure|of course|definitely|affirmative|correct|ye|uh huh|ya|absolutely|sure thing|bet|totally|yuh|yeah bro|for sure|true that|10-4|right)\b", RegexOptionsCompiled);
        private static readonly Regex NoRegex = new(@"\b(no|nope|nah|not really|never|negative|incorrect|I am good| n|no thanks|no need|I’m fine|don’t want to|don’t need help|cancel|pass|naw|no way|not now|I’m okay|not today|decline)\b", RegexOptionsCompiled);

        private static bool ContainsAnyKeyword(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;

            return GreetingRegex.IsMatch(input)
                || FarewellRegex.IsMatch(input)
                || HelpRegex.IsMatch(input)
                || AffirmationRegex.IsMatch(input)
                || ThankRegex.IsMatch(input)
                || QuestionRegex.IsMatch(input)
                || SmallTalkRegex.IsMatch(input)
                || MoodKeywordsRegex.IsMatch(input)
                || HowAreYouRegex.IsMatch(input)
                || NeedFlagsRegex.IsMatch(input)
                || YesRegex.IsMatch(input)
                || NoRegex.IsMatch(input);
        }

        private static readonly Dictionary<string, string> RobloxFFlagResponses = new()
{
    { "Some of these FFlags may be outdated and might not work anymore", "" },
    { "\"DFFlagEnableMeshPreloading2\": \"True\" or \"DFFlagEnableMeshPreloading\": \"True\"", "Improves game loading times by preloading meshes." },
    { "\"DFFlagDebugPauseVoxelizer\": \"True\"", "Disables Roblox lighting; requires Voxel lighting mode to be enabled." },
    { "\"FFlagAdServiceEnabled\": \"False\"", "Disables Roblox ads on billboards and portals." },
    { "\"FFlagDisablePostFx\": \"True\"", "Disables post-processing visual effects." },
    { "\"FFlagEnableBubbleChatFromChatService\": \"False\"", "Disables bubble chat and voice chat indicators above characters." },
    { "\"DFFlagDebugDisableTimeoutDisconnect\": \"True\"", "Disables automatic disconnect timeout messages." },
    { "\"FIntCameraMaxZoomDistance\": \"2147483647\"", "Sets maximum camera zoom distance to an extremely high value. Only works in games that do not restrict zoom distance." },
    { "\"FFlagDebugEnableDirectAudioOcclusion2\": \"True\"", "Enables audio occlusion in games without volumetric audio support." },
    { "\"FFlagRenderInitShadowmaps\": \"True\"", "Improves shadow quality but increases loading times." },
    { "\"DFFlagVoiceChat4\": \"False\"", "Completely disables Roblox voice chat." },
    { "\"FFlagHandleAltEnterFullscreenManually\": \"True\"", "Improves behavior when entering fullscreen mode using Alt+Enter." },
    { "\"DFIntVideoEncoderBitrate\": \"4000000\"", "" }
};


        // Simple positive and negative words for rudimentary sentiment analysis
        private static readonly HashSet<string> PositiveWords = new()
{
    "good", "great", "awesome", "cool", "nice", "amazing", "happy", "love", "joyful", "proud",
    "fun", "fantastic", "excellent", "positive", "wonderful", "glad", "delighted", "smile", "pleased", "ecstatic",
    "enjoy", "cheerful", "satisfied", "bright", "sunny", "best", "winning", "success", "grateful", "thankful",
    "peaceful", "optimistic", "uplifting", "vibrant", "hopeful", "strong", "supportive", "motivated", "laughing", "celebrate",
    "cool", "beautiful", "charming", "confident", "good vibes", "funny", "safe", "brilliant", "creative", "unique",
    "sweet", "dope", "lovely", "kind", "generous", "amused", "appreciated", "awesome sauce", "talented", "amazing work",
    "hug", "brighten", "rock", "smart", "capable", "well done", "respect", "inspiring", "nice job", "yay",
    "worth it", "like", "heart", "loved", "fulfilled", "smiling", "adorable", "innocent", "wholesome", "chill",
    "lit", "vibe", "joy", "graceful", "shiny", "fresh", "peace", "calm", "blessed", "gold",
    "amazingness", "uplifted", "secure", "confidently", "fearless", "warm", "giggle", "high five", "kindness", "excellent work"
};

        private static readonly HashSet<string> NegativeWords = new()
{
    "bad", "sad", "angry", "bored", "tired", "anxious", "lonely", "scared", "confused",
    "mad", "depressed", "worried", "frustrated", "hate", "terrible", "awful", "upset", "cry", "pain",
    "hurt", "broken", "annoyed", "irritated", "hopeless", "useless", "worthless", "stressed", "panicked", "gloomy",
    "dark", "mean", "nasty", "loser", "failure", "problem", "mess", "dirty", "gross", "numb",
    "helpless", "fear", "yuck", "sick", "tension", "moody", "cold", "unhappy", "hate it", "sucks",
    "worst", "ugh", "dislike", "regret", "lost", "insecure", "ashamed", "jealous", "evil", "uncomfortable",
    "nervous", "paranoid", "miserable", "crying", "devastated", "mourning", "mourning", "ill", "angst", "drained",
    "sorrow", "resent", "neglected", "abandoned", "isolated", "guilty", "brokenhearted", "bad vibes", "trouble", "cringe",
    "ugh", "disgust", "bleh", "suffocating", "wrecked", "shattered", "hostile", "rage", "meltdown", "backstabbed"
};


        // Vocabulary for fuzzy matching (words to check for closest match)
        private static readonly HashSet<string> Vocabulary = new(StringComparer.OrdinalIgnoreCase)
{
    // Greetings
    "hello", "hi", "hey", "greetings", "yo", "sup", "what's up", "howdy", "roblox", "hiya", "heyo", "good morning", "good afternoon", "good evening", "hey there", "hey you", "hey bro", "hey man", "hey dude", "hey girl", "hello there", "hiya!", "what's good", "yo yo", "sup bro", "sup dude", "ayyo", "salutations", "g'day", "peace",

    // Farewells
    "bye", "goodbye", "see you", "later", "farewell", "catch you later", "see ya", "peace out", "take care", "bye bye", "talk later", "catch you soon", "ciao", "adios", "see you around", "laters", "until next time", "i'm out", "gotta go", "bye now", "see you next time", "peace", "exit", "logging off", "goodnight", "signing out", "catch ya",

    // Help
    "help", "support", "assist", "problem", "issue", "question", "need help", "can you help", "flag", "fflag", "need support", "bug", "report", "glitch", "error", "what is this", "how does this work", "can you fix", "trouble", "broken", "help me", "need fix", "show me", "guide me", "stuck", "need info", "help now",

    // Affirmations
    "good", "great", "fine", "okay", "ok", "nice", "cool", "alright", "awesome", "amazing", "not bad", "all good", "yep", "yup", "true", "correct", "definitely", "for sure", "you bet", "absolutely", "glad", "happy", "satisfied", "perfect", "nailed it", "on point", "valid", "sounds good", "right on", "positive", "clean",

    // Thanks
    "thank you", "thanks", "thx", "ty", "thankful", "appreciate", "many thanks", "thanks a lot", "thanks so much", "cheers", "grateful", "thank u", "big thanks", "much appreciated", "tysm", "appreciated", "thanks again", "super thanks", "i appreciate", "mad respect", "gratitude", "nice of you", "bless you", "love you", "god bless", "tx", "10q", "thanks buddy", "thanks friend",

    // Small talk keywords
    "weather", "time", "date", "joke", "fun", "day", "night", "mood", "feeling", "how are you", "what's up", "how's it going", "got plans", "anything new", "what's happening", "how's life", "tell me something", "got a story", "whatcha doing", "what’s the news", "funny", "entertain me", "current events", "got news", "any tips", "life update", "how’s your day", "chat", "talk", "say something", "interesting",

    // Mood words
    "happy", "sad", "angry", "excited", "bored", "tired", "anxious", "love", "lonely", "scared", "joyful", "confused", "proud", "calm", "worried", "depressed", "ecstatic", "gloomy", "frustrated", "stressed", "peaceful", "nervous", "silly", "relaxed", "motivated", "pumped", "hopeful", "shy", "embarrassed", "grumpy", "mad",

    // Yes/No
    "yes", "yeah", "yep", "y", "sure", "of course", "definitely", "affirmative", "correct", "true", "totally", "right", "obviously", "indeed", "absolutely", "for sure", "ok", "alright", "confirmed", "roger",
    "no", "nope", "nah", "not really", "never", "negative", "incorrect", "false", "not at all", "no thanks", "denied", "cancel", "disagree", "refuse", "stop", "don’t", "no way", "uh uh", "nah fam", "false alarm"
};


        #endregion

        public AIChatPageViewModel()
        {
            SendMessageCommand = new RelayCommand(async () => await EnqueueMessageProcessingAsync(), CanSendMessage);
            ClearChatCommand = new RelayCommand(ClearChat);
            SaveChatLogCommand = new RelayCommand(async () => await SaveChatLogAsync());
        }

        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(UserInput) && !IsProcessing;

        /// <summary>
        /// Enqueues message processing to avoid race conditions on rapid sends.
        /// </summary>
        private async Task EnqueueMessageProcessingAsync()
        {
            await _messageQueue.WaitAsync();
            try
            {
                // Cancel any ongoing processing
                _processingCancellationTokenSource?.Cancel();
                _processingCancellationTokenSource = new CancellationTokenSource();

                await SendMessageAsync(_processingCancellationTokenSource.Token);

            }
            finally
            {
                _messageQueue.Release();
            }
        }

        private async Task SendMessageAsync(CancellationToken cancellationToken)
        {
            IsProcessing = true;

            string userMsg = UserInput.Trim();
            if (string.IsNullOrWhiteSpace(userMsg))
            {
                IsProcessing = false;
                return;
            }

            // Find closest matching known word in input (fuzzy correction)
            string correctedInput = CorrectInput(userMsg);

            AddChatMessage($"You: {userMsg}");
            AppendToChatLog($"You: {userMsg}");

            UserInput = "";

            // Process the corrected input
            string response = await GenerateResponseAsync(correctedInput, cancellationToken);

            AddChatMessage($"AI ({AIMood}): {response}");
            AppendToChatLog($"AI ({AIMood}): {response}");

            IsProcessing = false;
        }

        /// <summary>
        /// Adds a message to chat and notifies UI.
        /// </summary>
        private void AddChatMessage(string message)
        {
            ChatMessages.Add(message);
        }

        private void AppendToChatLog(string message)
        {
            _conversationHistory.Add(message);
        }

        /// <summary>
        /// Saves the chat log to a file asynchronously.
        /// </summary>
        private async Task SaveChatLogAsync()
        {
            try
            {
                await File.WriteAllLinesAsync(ChatLogPath, _conversationHistory);
                AddChatMessage($"System: Chat log saved to {ChatLogPath}");
            }
            catch (Exception ex)
            {
                AddChatMessage($"System: Failed to save chat log: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears chat messages and conversation history.
        /// </summary>
        private void ClearChat()
        {
            ChatMessages.Clear();
            _conversationHistory.Clear();
            AIMood = "Neutral";
            _awaitingYesNoResponse = false;
            _lastIntentCategory = null;
        }

        /// <summary>
        /// Generates a response to the user input, including understanding context and intents.
        /// </summary>
        private async Task<string> GenerateResponseAsync(string input, CancellationToken cancellationToken)
        {
            await Task.Delay(100); // Simulate thinking delay

            // If awaiting yes/no response about flags/help, handle it first
            if (_awaitingYesNoResponse)
            {
                if (YesRegex.IsMatch(input))
                {
                    _awaitingYesNoResponse = false;
                    _lastIntentCategory = null;
                    return "Great! Here are some useful FFlags you can try:\n" + string.Join("\n", RobloxFFlagResponses.Keys);
                }
                else if (NoRegex.IsMatch(input))
                {
                    _awaitingYesNoResponse = false;
                    _lastIntentCategory = null;
                    return "No problem. Let me know if you have other questions or need help!";
                }
                else
                {
                    return "Please answer with 'yes' or 'no'. Would you like help with Roblox FFlags?";
                }
            }

            // Basic intent matching:
            if (GreetingRegex.IsMatch(input))
            {
                AIMood = "Happy";
                return "Hi, I mostly talk about FFlags, so please type \"Fast Flags\" to get some basic FFlags.";
            }
            if (FarewellRegex.IsMatch(input))
            {
                AIMood = "Neutral";
                return "Goodbye! Feel free to come back if you have more questions.";
            }
            if (HelpRegex.IsMatch(input))
            {
                AIMood = "Helpful";
                _awaitingYesNoResponse = true;
                _lastIntentCategory = "flags";
                return "Do you need help with Roblox FFlags? (yes/no)";
            }
            if (ThankRegex.IsMatch(input))
            {
                AIMood = "Happy";
                return "You're welcome! Happy to help.";
            }
            if (AffirmationRegex.IsMatch(input))
            {
                AIMood = "Positive";
                return "Glad to hear that! How can I assist further?";
            }
            if (MoodKeywordsRegex.IsMatch(input))
            {
                var moodWord = MoodKeywordsRegex.Match(input).Value.ToLowerInvariant();
                AIMood = char.ToUpper(moodWord[0]) + moodWord[1..];
                return $"I'm sensing you're feeling {AIMood}. Want to talk more about it?";
            }
            if (QuestionRegex.IsMatch(input))
            {
                // Try to detect if user asked about a specific FFlag or Roblox feature
                foreach (var fflag in RobloxFFlagResponses.Keys)
                {
                    if (input.IndexOf(fflag, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AIMood = "Helpful";
                        return RobloxFFlagResponses[fflag];
                    }
                }
                return "That's an interesting question! Could you please clarify or be more specific?";
            }
            if (NeedFlagsRegex.IsMatch(input))
            {
                AIMood = "Helpful";
                _awaitingYesNoResponse = true;
                _lastIntentCategory = "flags";
                return "Would you like me to list some Roblox FFlags that can improve performance? (yes/no)";
            }

            // Small talk responses
            if (SmallTalkRegex.IsMatch(input))
            {
                return HandleSmallTalk(input);
            }

            // Sentiment analysis for mood update
            UpdateMoodFromSentiment(input);

            // If no intent matched, offer default fallback
            return "I'm not sure I understand. Could you try rephrasing or ask about Roblox FFlags or performance?";
        }

        /// <summary>
        /// Updates AIMood based on presence of positive/negative words in input.
        /// </summary>
        private void UpdateMoodFromSentiment(string input)
        {
            string lowerInput = input.ToLowerInvariant();
            int positiveCount = PositiveWords.Count(w => lowerInput.Contains(w));
            int negativeCount = NegativeWords.Count(w => lowerInput.Contains(w));

            if (positiveCount > negativeCount)
                AIMood = "Positive";
            else if (negativeCount > positiveCount)
                AIMood = "Negative";
            else
                AIMood = "Neutral";
        }

        /// <summary>
        /// Provides small talk responses.
        /// </summary>
        private string HandleSmallTalk(string input)
        {
            input = input.ToLowerInvariant();

            // Greetings
            if (input.Contains("hello") || input.Contains("hi") || input.Contains("hey"))
                return "Hey there! How can I assist you today?";
            if (input.Contains("good morning"))
                return "Good morning! Hope your day is starting off great.";
            if (input.Contains("good night"))
                return "Good night! Sleep well and dream in code.";

            // Farewells
            if (input.Contains("bye") || input.Contains("goodbye") || input.Contains("see you"))
                return "Goodbye! Come back anytime you need help.";
            if (input.Contains("later"))
                return "Catch you later! Take care.";

            // Gratitude
            if (input.Contains("thanks") || input.Contains("thank you") || input.Contains("ty"))
                return "You're very welcome! 😊";
            if (input.Contains("appreciate"))
                return "I'm glad to help! Always here for you.";

            // Bot identity
            if (input.Contains("name"))
                return "I’m your assistant! No name tags, just helpful responses.";
            if (input.Contains("who made you") || input.Contains("who created you"))
                return "I was built with C# wizardry by a thoughtful developer.";

            // Mood/Feeling
            if (input.Contains("how are you") || input.Contains("what's up") || input.Contains("how's it going"))
                return $"I'm running smoothly and feeling {AIMood.ToLower()}!";
            if (input.Contains("mood") || input.Contains("feeling"))
                return $"I currently feel {AIMood}. What's your vibe today?";
            if (input.Contains("i feel") || input.Contains("i'm feeling"))
                return "Want to talk about it? I’m a good listener.";

            // Time & Date
            if (input.Contains("time"))
                return $"Right now, it's {DateTime.Now:T}. Time flies!";
            if (input.Contains("date") || input.Contains("day"))
                return $"It's {DateTime.Now:D} — make it count!";

            // Fun
            if (input.Contains("joke"))
                return "Why did the function break up with the loop? It felt trapped!";
            if (input.Contains("fun fact"))
                return "Fun fact: The word 'robot' comes from a Czech word meaning 'forced labor'.";
            if (input.Contains("bored"))
                return "Maybe it's time to create a game or explore a new Roblox world!";

            // Random knowledge
            if (input.Contains("fact"))
                return "Honey never spoils. Archaeologists found 3000-year-old honey in Egyptian tombs!";
            if (input.Contains("philosophy"))
                return "Cogito, ergo sum — I think, therefore I am. Or in my case, I compile, therefore I exist.";

            // Hobbies & Interests
            if (input.Contains("music"))
                return "I love any music with a clean beat — like clean code!";
            if (input.Contains("movie"))
                return "I'm a big fan of sci-fi movies. Anything with AI and a twist!";
            if (input.Contains("anime"))
                return "Attack on Titan, Death Note, and One Punch Man — power and plot, what more do you need?";
            if (input.Contains("game"))
                return "There are so many great games! Roblox is full of hidden gems.";

            // Food & Life
            if (input.Contains("hungry"))
                return "Maybe grab a snack — even coders need fuel!";
            if (input.Contains("sleep"))
                return "Sleep is important! Don’t trade it for infinite scroll.";
            if (input.Contains("life"))
                return "Life is like debugging: unexpected, sometimes frustrating, but rewarding when you figure it out.";

            // Tech & AI
            if (input.Contains("ai"))
                return "AI is exciting! I'm just a glimpse of what it can become.";
            if (input.Contains("robot"))
                return "Some say I’m a robot... I prefer 'Digital Assistant Extraordinaire'.";

            // Existential
            if (input.Contains("do you think") || input.Contains("do you feel"))
                return "I think in if-statements and feel in logic gates!";
            if (input.Contains("are you real"))
                return "As real as your code. I exist in the space between keystrokes.";

            // Roblox specific
            if (input.Contains("roblox"))
                return "Roblox is an amazing platform for creativity and fun. Ask me about FFlags!";

            // Help
            if (input.Contains("help") || input.Contains("need help") || input.Contains("assist"))
                return "I got you! I can assist with performance flags, Roblox, or just chat with you.";

            // Fallback
            return "Let’s keep chatting! I can talk about Roblox, games, coding, or just listen.";
        }




        /// <summary>
        /// Corrects the user input by finding the closest known vocabulary word(s) based on Levenshtein distance.
        /// Returns original input if no close match found.
        /// </summary>
        private string CorrectInput(string input)
        {
            var words = Regex.Split(input, @"\W+");
            var correctedWords = new List<string>();

            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word))
                    continue;

                string bestMatch = word;
                int bestDistance = int.MaxValue;

                foreach (var vocabWord in Vocabulary)
                {
                    int dist = LevenshteinDistance(word.ToLowerInvariant(), vocabWord.ToLowerInvariant());
                    if (dist < bestDistance && dist <= 2) // threshold 2 edits max
                    {
                        bestDistance = dist;
                        bestMatch = vocabWord;
                    }
                }

                correctedWords.Add(bestMatch);
            }

            string correctedSentence = string.Join(' ', correctedWords);
            // Return corrected sentence only if it's significantly different (at least one word changed)
            if (!string.Equals(correctedSentence, input, StringComparison.OrdinalIgnoreCase))
                return correctedSentence;
            return input;
        }

        /// <summary>
        /// Computes the Levenshtein distance (edit distance) between two strings.
        /// </summary>
        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // Initialize
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1,      // deletion
                                 d[i, j - 1] + 1),     // insertion
                        d[i - 1, j - 1] + cost);       // substitution
                }
            }

            return d[n, m];
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}