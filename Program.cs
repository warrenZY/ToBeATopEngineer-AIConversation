using static ToBeATopEngineer_AIConversation.Services.JsonParserService;
using System.Text.Json;
using System.Text;
using ToBeATopEngineer_AIConversation.Services;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ToBeATopEngineer_AIConversation;

internal class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly ITtsService ttsService;

    static Program()
    {
        // Initialize the appropriate TTS service based on the OS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ttsService = new WindowsTtsService();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ttsService = new LinuxTtsService();
        }
        else
        {
            ttsService = new NullTtsService(); // Fallback that does nothing
        }
    }

    public static async Task Main(string[] args)
    {
        List<ChatMessage> messages = new List<ChatMessage>();

        client.DefaultRequestHeaders.Add("appid", PrivateData.APPID);
        client.DefaultRequestHeaders.Add("Authorization", PrivateData.APIKEY);

        while (true)
        {
            Console.Write("用户：");
            string? usrContent = Console.ReadLine();

            if (string.IsNullOrEmpty(usrContent))
            {
                break;
            }

            messages.Add(new ChatMessage { Role = "user", Content = usrContent });

            var url = "https://qianfan.baidubce.com/v2/chat/completions";

            var requestBody = new ChatCompletionRequest
            {
                Model = "ernie-speed-8k",
                Messages = messages,
                Temperature = 0.42,
                WebSearch = new WebSearchOptions { Enable = true, EnableCitation = false, EnableTrace = false }
            };

            var json = JsonSerializer.Serialize(requestBody, AppJsonSerializerContext.Default.Options);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                List<string> extractedMessages = ParseMessages(responseContent);

                if (extractedMessages.Count > 0)
                {
                    string aiResponse = extractedMessages[0];
                    Console.WriteLine($"AI：{aiResponse}");
                    messages.Add(new ChatMessage { Role = "assistant", Content = aiResponse });

                    // Speak the AI response
                    await ttsService.SpeakAsync(aiResponse);
                }
                else
                {
                    Console.WriteLine("AI 没有返回消息。");
                }
            }
            else
            {
                Console.WriteLine($"请求失败，状态码: {response.StatusCode}");
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"返回内容: {responseContent}");
            }
        }
    }
}




[JsonSerializable(typeof(ChatCompletionRequest))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}