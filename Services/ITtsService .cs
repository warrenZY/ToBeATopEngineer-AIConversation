using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToBeATopEngineer_AIConversation.Services;

// TTS Service Interfaces and Implementations
public interface ITtsService
{
    Task SpeakAsync(string text);
}
