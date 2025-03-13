using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ToBeATopEngineer_AIConversation.Services
{
    public interface IJsonParserService
    {
        public class ChatCompletionRequest;
        public class WebSearchOptions;
        public class ChatMessage;


        public static List<string> ParseMessages;

    }
}
