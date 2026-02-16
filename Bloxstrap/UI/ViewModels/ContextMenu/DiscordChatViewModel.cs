using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Voidstrap.UI.Chat
{
    public class DiscordChatViewModel : ObservableObject
    {
        private static readonly string SaveFilePath = Path.Combine(Paths.Base, "GroupChats.json");

        public ObservableCollection<ChatTab> ChatTabs { get; } = new();
        private ChatTab _selectedTab;
        public ChatTab SelectedTab
        {
            get => _selectedTab;
            set => SetProperty(ref _selectedTab, value);
        }

        public ObservableCollection<string> AllEmojis { get; } = new();

        private string _newBotToken;
        public string NewBotToken { get => _newBotToken; set => SetProperty(ref _newBotToken, value); }

        private string _newChannelId;
        public string NewChannelId { get => _newChannelId; set => SetProperty(ref _newChannelId, value); }

        private string _newGroupName;
        public string NewGroupName { get => _newGroupName; set => SetProperty(ref _newGroupName, value); }

        private string _shareCode;
        public string ShareCode { get => _shareCode; set => SetProperty(ref _shareCode, value); }

        private string _inputShareCode;
        public string InputShareCode { get => _inputShareCode; set => SetProperty(ref _inputShareCode, value); }

        public RelayCommand<ChatTab> DeleteGroupChatCommand { get; }
        public RelayCommand JoinCommand { get; }
        public RelayCommand CopyShareCodeCommand { get; }
        public RelayCommand<string> JoinViaCodeCommand { get; }

        public DiscordChatViewModel()
        {
            JoinCommand = new RelayCommand(JoinChat);
            DeleteGroupChatCommand = new RelayCommand<ChatTab>(DeleteGroupChat);

            CopyShareCodeCommand = new RelayCommand(CopyShareCode);
            JoinViaCodeCommand = new RelayCommand<string>(JoinViaCode);

            LoadAllEmojis();
            LoadChats();
        }

        private void LoadAllEmojis()
        {
            for (int i = 0x1F600; i <= 0x1F64F; i++)
                AllEmojis.Add(char.ConvertFromUtf32(i));

            foreach (var c in "❤️👍😂😮😢😡🎉💯🔥✨😎💖🥰🤯🤔")
                AllEmojis.Add(c.ToString());
        }

        #region Chat Management

        private void JoinChat()
        {
            if (string.IsNullOrWhiteSpace(NewBotToken) || string.IsNullOrWhiteSpace(NewChannelId))
            {
                Frontend.ShowMessageBox("Please enter both Bot Token and Channel ID");
                return;
            }

            string groupName = string.IsNullOrWhiteSpace(NewGroupName) ? "Group" : NewGroupName;

            var newTab = new ChatTab(groupName, NewBotToken, NewChannelId, this);
            ChatTabs.Add(newTab);
            SelectedTab = newTab;
            ShareCode = $"{NewBotToken}|{NewChannelId}|{groupName}";

            NewBotToken = "";
            NewChannelId = "";
            NewGroupName = "";

            SaveChats();
        }

        public void DeleteGroupChat(ChatTab tab)
        {
            if (tab == null) return;

            var result = Frontend.ShowMessageBox(
                $"Are you sure you want to delete the group chat '{tab.TabName}'?"
            );

            if (result != MessageBoxResult.Yes)
                return;

            ChatTabs.Remove(tab);

            if (SelectedTab == tab)
                SelectedTab = ChatTabs.FirstOrDefault();

            SaveChats();
        }

        private void CopyShareCode()
        {
            if (SelectedTab == null)
            {
                Frontend.ShowMessageBox("No active tab to copy.");
                return;
            }

            try
            {
                var data = new
                {
                    token = SelectedTab.BotToken,
                    channelId = SelectedTab.ChannelId,
                    groupName = SelectedTab.TabName
                };

                string json = JsonSerializer.Serialize(data);
                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

                Clipboard.SetText(base64);

                Frontend.ShowMessageBox("Share code copied to clipboard!");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to copy share code: {ex.Message}");
            }
        }

        private void JoinViaCode(string? code)
        {
            string input = code ?? InputShareCode;

            if (string.IsNullOrWhiteSpace(input))
            {
                Frontend.ShowMessageBox("Please enter a share code.");
                return;
            }

            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(input));
                var data = JsonSerializer.Deserialize<ShareCodeData>(json);

                if (data == null || string.IsNullOrWhiteSpace(data.token) || string.IsNullOrWhiteSpace(data.channelId))
                {
                    Frontend.ShowMessageBox("Invalid share code.");
                    return;
                }

                var newTab = new ChatTab(data.groupName, data.token, data.channelId, this);
                ChatTabs.Add(newTab);
                SelectedTab = newTab;

                InputShareCode = "";
                SaveChats();
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to join via code: {ex.Message}");
            }
        }

        private class ShareCodeData
        {
            public string token { get; set; }
            public string channelId { get; set; }
            public string groupName { get; set; }
        }

        #endregion

        #region Save/Load

        public void SaveChats()
        {
            try
            {
                var data = ChatTabs.Select(tab => new ChatTabData
                {
                    TabName = tab.TabName,
                    BotToken = tab.BotToken,
                    ChannelId = tab.ChannelId,
                    Messages = tab.Messages.Select(m => new ChatMessageData
                    {
                        Id = m.Id,
                        Author = m.Author,
                        DisplayUsername = m.DisplayUsername,
                        ReplyToText = m.ReplyToText,
                        Content = m.Content,
                        TimestampDateTime = m.TimestampDateTime,
                        Attachments = m.Attachments.ToList(),
                        ReplyToMessageId = m.ReplyToMessageId,
                        ReplyToAuthor = m.ReplyToAuthor,
                        Reactions = m.Reactions.Select(r => new MessageReaction
                        {
                            Emoji = r.Emoji,
                            Count = r.Count
                        }).ToList()
                    }).ToList()
                }).ToList();

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Paths.Base);
                File.WriteAllText(SaveFilePath, json);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to save chats: {ex.Message}");
            }
        }

        public void LoadChats()
        {
            ChatTabs.Clear();

            if (!File.Exists(SaveFilePath)) return;

            try
            {
                var json = File.ReadAllText(SaveFilePath);
                var data = JsonSerializer.Deserialize<List<ChatTabData>>(json);
                if (data == null) return;

                foreach (var tabData in data)
                {
                    var tab = new ChatTab(tabData.TabName, tabData.BotToken, tabData.ChannelId, this);

                    foreach (var msgData in tabData.Messages)
                    {
                        tab.Messages.Add(new ChatMessage
                        {
                            Id = msgData.Id,
                            Author = msgData.Author,
                            DisplayUsername = msgData.DisplayUsername,
                            ReplyToText = msgData.ReplyToText,
                            Content = msgData.Content,
                            TimestampDateTime = msgData.TimestampDateTime,
                            Attachments = new ObservableCollection<string>(msgData.Attachments),
                            ReplyToMessageId = msgData.ReplyToMessageId,
                            ReplyToAuthor = msgData.ReplyToAuthor,
                            Reactions = new ObservableCollection<MessageReaction>(msgData.Reactions)
                        });
                    }

                    ChatTabs.Add(tab);
                }

                SelectedTab = ChatTabs.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to load chats: {ex.Message}");
            }
        }

        #endregion

        #region ChatTab & Messages

        public class ChatTab : ObservableObject
        {
            private readonly HttpClient _httpClient = new();
            private readonly DiscordChatViewModel _parentVm;

            public ObservableCollection<ChatMessage> Messages { get; } = new();
            public string TabName { get; }
            public string BotToken { get; }
            public string ChannelId { get; }

            private string _messageText;
            public string MessageText { get => _messageText; set => SetProperty(ref _messageText, value); }

            private bool _someoneTyping;
            public bool SomeoneTyping { get => _someoneTyping; set => SetProperty(ref _someoneTyping, value); }

            private string _typingIndicatorText;
            public string TypingIndicatorText { get => _typingIndicatorText; set => SetProperty(ref _typingIndicatorText, value); }

            private ChatMessage _replyingTo;
            public ChatMessage ReplyingTo { get => _replyingTo; set => SetProperty(ref _replyingTo, value); }

            public IAsyncRelayCommand SendMessageCommand { get; }
            public RelayCommand<ChatMessage> ReplyCommand { get; }
            public IRelayCommand AttachImageCommand { get; }

            public ChatTab(string name, string botToken, string channelId, DiscordChatViewModel parentVm)
            {
                TabName = name;
                BotToken = botToken;
                ChannelId = channelId;
                _parentVm = parentVm;

                SendMessageCommand = new AsyncRelayCommand(SendMessage);
                ReplyCommand = new RelayCommand<ChatMessage>(m => ReplyingTo = m);
                AttachImageCommand = new RelayCommand(AttachImage);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", BotToken);
                _ = StartPolling();
            }

            private async void AttachImage()
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    var path = dialog.FileName;

                    try
                    {
                        using var fileStream = File.OpenRead(path);
                        var content = new MultipartFormDataContent();
                        var streamContent = new StreamContent(fileStream);
                        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                        content.Add(streamContent, "file", Path.GetFileName(path));

                        var response = await _httpClient.PostAsync(
                            $"https://discord.com/api/v10/channels/{ChannelId}/messages",
                            content
                        );
                        response.EnsureSuccessStatusCode();

                        var json = await response.Content.ReadAsStringAsync();
                        var msg = JsonSerializer.Deserialize<DiscordMessage>(json);

                        if (msg != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Messages.Add(new ChatMessage
                                {
                                    Id = msg.id,
                                    Author = msg.author.username,
                                    DisplayUsername = msg.author.username,
                                    Content = msg.content ?? "",
                                    TimestampDateTime = DateTime.Parse(msg.timestamp),
                                    Attachments = new ObservableCollection<string>(msg.attachments?.Select(a => a.url) ?? new string[] { path })
                                });
                            });

                            _parentVm.SaveChats();
                        }
                    }
                    catch (Exception ex)
                    {
                        Frontend.ShowMessageBox($"Failed to attach image: {ex.Message}");
                    }
                }
            }

            private async Task StartPolling()
            {
                while (true)
                {
                    try
                    {
                        await LoadMessages();
                        UpdateTypingIndicator();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }

                    await Task.Delay(1000);
                }
            }

            public async Task ReactToMessage(ChatMessage msg, string emoji)
            {
                if (string.IsNullOrWhiteSpace(emoji)) return;

                var url =
                    $"https://discord.com/api/v10/channels/{ChannelId}/messages/{msg.Id}/reactions/{Uri.EscapeDataString(emoji)}/@me";

                await _httpClient.PutAsync(url, null);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var existing = msg.Reactions.FirstOrDefault(r => r.Emoji == emoji);
                    if (existing != null)
                        existing.Count++;
                    else
                        msg.Reactions.Add(new MessageReaction { Emoji = emoji, Count = 1 });
                });

                _parentVm.SaveChats();
            }

            public async Task LoadMessages()
            {
                var response = await _httpClient.GetAsync($"https://discord.com/api/v10/channels/{ChannelId}/messages?limit=50");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var discordMessages = JsonSerializer.Deserialize<DiscordMessage[]>(json);
                if (discordMessages == null) return;

                var ordered = discordMessages.Reverse().ToList();
                var ids = ordered.Select(m => m.id).ToHashSet();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    for (int i = Messages.Count - 1; i >= 0; i--)
                        if (!ids.Contains(Messages[i].Id))
                            Messages.RemoveAt(i);

                    foreach (var msg in ordered)
                    {
                        if (Messages.Any(m => m.Id == msg.id)) continue;

                        Messages.Add(new ChatMessage
                        {
                            Id = msg.id,
                            Author = msg.author.username,
                            DisplayUsername = msg.author.username,
                            Content = msg.content,
                            TimestampDateTime = DateTime.Parse(msg.timestamp),
                            ReplyToMessageId = msg.message_reference?.message_id,
                            ReplyToAuthor = msg.message_reference?.author?.username,
                            ReplyToText = msg.message_reference != null ? $"(Replying to {msg.message_reference.author?.username})" : "",
                            Attachments = new ObservableCollection<string>(msg.attachments?.Select(a => a.url) ?? Array.Empty<string>())
                        });
                    }
                });
            }

            private async Task SendMessage()
            {
                if (string.IsNullOrWhiteSpace(MessageText) && (Messages.LastOrDefault()?.Attachments?.Count ?? 0) == 0)
                    return;

                try
                {
                    HttpContent content;
                    if (Messages.LastOrDefault()?.Attachments?.Count > 0)
                    {
                        var multipart = new MultipartFormDataContent();
                        if (!string.IsNullOrWhiteSpace(MessageText))
                        {
                            var payload = new { content = MessageText };
                            var payloadJson = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                            multipart.Add(payloadJson, "payload_json");
                        }

                        foreach (var attachmentPath in Messages.Last().Attachments)
                        {
                            if (File.Exists(attachmentPath))
                            {
                                var stream = File.OpenRead(attachmentPath);
                                var streamContent = new StreamContent(stream);
                                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                                multipart.Add(streamContent, "files[0]", Path.GetFileName(attachmentPath)); // Discord expects files[0], files[1], etc.
                            }
                        }

                        content = multipart;
                    }
                    else
                    {
                        var payload = new { content = MessageText };
                        content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    }

                    var response = await _httpClient.PostAsync($"https://discord.com/api/v10/channels/{ChannelId}/messages", content);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var msg = JsonSerializer.Deserialize<DiscordMessage>(json);

                    if (msg != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Messages.Add(new ChatMessage
                            {
                                Id = msg.id,
                                Author = msg.author.username,
                                DisplayUsername = msg.author.username,
                                Content = msg.content ?? "",
                                TimestampDateTime = DateTime.Parse(msg.timestamp),
                                Attachments = new ObservableCollection<string>(msg.attachments?.Select(a => a.url) ?? Array.Empty<string>())
                            });
                        });

                        _parentVm.SaveChats();
                    }
                }
                catch (Exception ex)
                {
                    Frontend.ShowMessageBox($"Failed to send message: {ex.Message}");
                }

                MessageText = "";
                ReplyingTo = null;
            }

            public void UpdateTypingIndicator()
            {
                var last = Messages.LastOrDefault();
                if (last == null)
                {
                    SomeoneTyping = false;
                    TypingIndicatorText = "";
                    return;
                }

                var diff = (DateTime.Now - last.TimestampDateTime).TotalSeconds;
                SomeoneTyping = diff < 6;
                TypingIndicatorText = SomeoneTyping ? $"{last.Author} is typing..." : "";
            }
        }

        public class ChatMessage : ObservableObject
        {
            public string Id { get; set; }
            public string Author { get; set; }
            public string DisplayUsername { get; set; }
            public string ReplyToText { get; set; }
            public string Content { get; set; }
            public DateTime TimestampDateTime { get; set; }
            public ObservableCollection<string> Attachments { get; set; } = new();
            public string ReplyToMessageId { get; set; }
            public string ReplyToAuthor { get; set; }
            public ObservableCollection<MessageReaction> Reactions { get; set; } = new();
        }

        public class MessageReaction
        {
            public string Emoji { get; set; }
            public int Count { get; set; }
        }

        public class DiscordMessage
        {
            public string id { get; set; }
            public string content { get; set; }
            public DiscordAuthor author { get; set; }
            public string timestamp { get; set; }
            public DiscordReference message_reference { get; set; }
            public DiscordAttachment[] attachments { get; set; }
        }

        public class DiscordAuthor { public string username { get; set; } }
        public class DiscordReference { public string message_id { get; set; } public DiscordAuthor author { get; set; } }
        public class DiscordAttachment { public string url { get; set; } }

        private class ChatTabData
        {
            public string TabName { get; set; }
            public string BotToken { get; set; }
            public string ChannelId { get; set; }
            public List<ChatMessageData> Messages { get; set; }
        }

        private class ChatMessageData
        {
            public string Id { get; set; }
            public string Author { get; set; }
            public string DisplayUsername { get; set; }
            public string ReplyToText { get; set; }
            public string Content { get; set; }
            public DateTime TimestampDateTime { get; set; }
            public List<string> Attachments { get; set; } = new();
            public string ReplyToMessageId { get; set; }
            public string ReplyToAuthor { get; set; }
            public List<MessageReaction> Reactions { get; set; } = new();
        }
    }
}
#endregion