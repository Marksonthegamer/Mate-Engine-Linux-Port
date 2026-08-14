using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine;
using Application = UnityEngine.Application;

namespace ollama
{
    public static partial class Ollama
    {
        public static List<ChatMessage> ChatHistory;
        private static int HistoryLimit;
        
        public static string playerName = "user";
        
        public static string AIName = "assistant";

        /// <summary>Start a brand new chat</summary>
        /// <param name="historyLimit">Number of messages to keep in memory <i>(includes both prompt and response, but <b>not</b> system)</i></param>
        /// <param name="system">Add a <b>System</b> prompt that is always active</param>
        public static void InitChat(int historyLimit = 8, string system = null)
        {
            if (ChatHistory == null)
                ChatHistory = new List<ChatMessage>();
            else
                ChatHistory.Clear();

            HistoryLimit = historyLimit;

            if (!string.IsNullOrEmpty(system))
                SetSystemPrompt(system);
        }

        /// <summary>Save the current Chat History</summary>
        /// <param name="fileName">If not provided, defaults to <see href="https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html">Application.persistentDataPath</see></param>
        public static void SaveChatHistory(string fileName = null)
        {
            if (string.IsNullOrEmpty(fileName))
                fileName = Path.Combine(Application.persistentDataPath, "ZomeAI.json");

            string json = JsonUtility.ToJson(new ChatListWrapper { chat = ChatHistory.GetRange(1, ChatHistory.Count - 1) });
            File.WriteAllText(fileName, json);

            Debug.Log($"Saved Chat History to \"{fileName}\"");
        }

        /// <summary>Load a Chat History</summary>
        /// <param name="fileName">If not provided, defaults to <see href="https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html">Application.persistentDataPath</see></param>
        /// <param name="historyLimit">Number of messages to keep in memory <i>(includes both prompt and response, but <b>not</b> system)</i></param>
        public static int LoadChatHistory(string fileName = null, int historyLimit = 100)
        {
            if (string.IsNullOrEmpty(fileName))
                fileName = Path.Combine(Application.persistentDataPath, "ZomeAI.json");

            if (!File.Exists(fileName))
            {
                InitChat(historyLimit);
                Debug.LogWarning($"Chat History \"{fileName}\" does not exist...");
                return 0;
            }
            
            string json = File.ReadAllText(fileName);
            ChatHistory = JsonUtility.FromJson<ChatListWrapper>(json).chat;
            HistoryLimit = historyLimit;

            bool system = HasSystemPrompt();

            int _limit = HistoryLimit + (system ? 1 : 0);
            while (ChatHistory.Count > _limit)
            {
                if (system)
                    ChatHistory.RemoveAt(1);
                else
                    ChatHistory.RemoveAt(0);
            }

            Debug.Log($"Loaded Chat History from \"{fileName}\"");

            return ChatHistory.Count;
        }

        /// <summary>Generate a response from prompt, with chat context/history</summary>
        /// <param name="model">Ollama Model Syntax (<b>eg.</b> gemma3:4b)</param>
        /// <param name="keep_alive">The duration <i>(in seconds)</i> to keep the model in memory</param>
        /// <returns>response string from the LLM</returns>
        public static async Task<string> Chat(string model, string prompt, int keep_alive = 300, Texture2D image = null)
        {
            ChatHistory.Add(new ChatMessage{ role = playerName, content = prompt });

            var request = new Request.Chat(model, ChatHistory, false, keep_alive, null);
            string payload = JsonConvert.SerializeObject(request);
            var response = await PostRequest<Response.Chat>(payload, Endpoints.CHAT);

            ChatHistory.Add(response.message);

            bool system = HasSystemPrompt();

            int _limit = HistoryLimit + (system ? 1 : 0);
            while (ChatHistory.Count > _limit)
            {
                if (system)
                    ChatHistory.RemoveAt(1);
                else
                    ChatHistory.RemoveAt(0);
            }

            return response.message.content;
        }

        /// <summary>Stream a response from prompt, with chat context/history</summary>
        /// <param name="onTextReceived">The callback to handle the streaming chunks</param>
        /// <param name="model">Ollama Model Syntax (<b>eg.</b> gemma3:4b)</param>
        /// <param name="keep_alive">The duration <i>(in seconds)</i> to keep the model in memory</param>
        public static async Task ChatStream(Action<string> onTextReceived, string model, string prompt,
            int keep_alive = 300, Texture2D image = null)
        {
            ChatHistory.Add(new ChatMessage { role = playerName, content = prompt });

            var request = new Request.Chat(model, ChatHistory, true, keep_alive, null);
            string payload = JsonConvert.SerializeObject(request);
            StringBuilder reply = new StringBuilder();

            await PostRequestStream(payload, Endpoints.CHAT, (Response.Chat response) =>
            {
                if (!response.done)
                {
                    onTextReceived?.Invoke(response.message.content);
                    reply.Append(response.message.content);
                }
            });

            ChatHistory.Add(new ChatMessage { role = AIName, content = reply.ToString()});

        bool system = HasSystemPrompt();

            int _limit = HistoryLimit + (system ? 1 : 0);
            while (ChatHistory.Count > _limit)
            {
                if (system)
                    ChatHistory.RemoveAt(1);
                else
                    ChatHistory.RemoveAt(0);
            }

            OnStreamFinished?.Invoke();
        }

        public static bool HasSystemPrompt() => (ChatHistory != null) && (ChatHistory.Count > 0) && (ChatHistory[0].role == "system");

        public static void SetSystemPrompt(string system)
        {
            if (HasSystemPrompt())
                ChatHistory[0] = new ChatMessage{role = "system", content = system};
            else
                ChatHistory.Insert(0, new ChatMessage { role = "system", content = system });
        }

        public static string GetSystemPrompt()
        {
            return HasSystemPrompt() ? ChatHistory[0].content : null;
        }

        public static void RemoveSystemPrompt()
        {
            if (HasSystemPrompt()) ChatHistory.RemoveAt(0);
        }
    }
}
