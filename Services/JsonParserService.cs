using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace ToBeATopEngineer_AIConversation.Services;

public class JsonParserService : IJsonParserService
{

    // 定义请求体类
    public class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        required public string Model { get; set; }

        [JsonPropertyName("messages")]
        required public List<ChatMessage> Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("web_search")]
        required public WebSearchOptions WebSearch { get; set; }
    }

    public class WebSearchOptions
    {
        [JsonPropertyName("enable")]
        public bool Enable { get; set; }

        [JsonPropertyName("enable_citation")]
        public bool EnableCitation { get; set; }

        [JsonPropertyName("enable_trace")]
        public bool EnableTrace { get; set; }
    }

    public class ChatMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }



    public static List<string> ParseMessages(string jsonString)
        {
            List<string> messages = new List<string>();

            try
            {
                JsonDocument document = JsonDocument.Parse(jsonString);
                JsonElement root = document.RootElement;

                if (root.TryGetProperty("choices", out JsonElement choicesArray) && choicesArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement choice in choicesArray.EnumerateArray())
                    {
                        if (choice.TryGetProperty("message", out JsonElement messageObject) && messageObject.TryGetProperty("content", out JsonElement contentElement))
                        {
                            messages.Add(contentElement.GetString());
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON 解析错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
            }

            return messages;
        }
}
