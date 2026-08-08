using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine;

namespace ollama
{
    public static partial class Ollama
    {
        /// <summary>
        /// 1 <b>Token</b> is roughly 1 English <b>word</b> or 1 Chinese <b>character</b> <br/>
        /// * <see href="https://ollama.com/library/nomic-embed-text">nomic-embed-text</see> has a context window of <b>8192</b> tokens<br/>
        /// * <see href="https://ollama.com/library/gemma3">gemma3</see> has a context window of <b>128k</b> tokens<br/>
        /// So, a limit of 8192 should be fine nowadays <i>(if your document is longer than this, better to split it yourself...)</i>
        /// </summary>
        private const int CONTEXT_LIMIT = 8192;

        private static List<float[]> VectorsDatabase;
        private static List<string> ContextDatabase;

        private static class EmbedConfig
        {
            public static string model;
            public static int keep_alive;
        }

        /// <summary>Initialize the database to store the vectors</summary>
        /// <param name="model">
        /// Ollama Model Syntax (<b>eg.</b> nomic-embed-text) <br/>
        /// <b>Important:</b> Not all models can generate embeddings
        /// </param>
        /// <param name="keep_alive">The duration <i>(in seconds)</i> to keep the model in memory</param>
        public static void InitRAG(string model, int keep_alive = 300)
        {
            VectorsDatabase = new List<float[]>();
            ContextDatabase = new List<string>();

            EmbedConfig.model = model;
            EmbedConfig.keep_alive = keep_alive;
        }

        /// <summary>Add a context into the database and calculate its vector</summary>
        /// <param name="data"><b>Note:</b> Ensure longer texts contain newline character, in order to be split</param>
        public static async Task AppendData(string data)
        {
            int len = data.Length;
            if (len <= CONTEXT_LIMIT)
            {
                float[] vector = await Embeddings(EmbedConfig.model, data, EmbedConfig.keep_alive);
                VectorsDatabase.Add(vector);
                ContextDatabase.Add(data);
                return;
            }

            string[] sentences = Regex.Split(data, @"(?<=[.?!""])\s+");
            StringBuilder chunk = new StringBuilder();

            foreach (string sentence in sentences)
            {
                if (sentence.Length > CONTEXT_LIMIT)
                {
                    string paragraph = chunk.ToString();
                    float[] vector = await Embeddings(EmbedConfig.model, paragraph, EmbedConfig.keep_alive);
                    VectorsDatabase.Add(vector);
                    ContextDatabase.Add(paragraph);
                    chunk.Clear();
                }

                chunk.Append(" ");
                chunk.Append(sentence);

                if (chunk.Length > CONTEXT_LIMIT)
                {
                    string paragraph = chunk.ToString();
                    float[] vector = await Embeddings(EmbedConfig.model, paragraph, EmbedConfig.keep_alive);
                    VectorsDatabase.Add(vector);
                    ContextDatabase.Add(paragraph);
                    chunk.Clear();
                }
            }

            if (chunk.Length > 0)
            {
                string paragraph = chunk.ToString();
                float[] vector = await Embeddings(EmbedConfig.model, paragraph, EmbedConfig.keep_alive);
                VectorsDatabase.Add(vector);
                ContextDatabase.Add(paragraph);
                chunk.Clear();
            }
        }

        private static async Task<ChatMessage> Analyze(string prompt)
        {
            var vector = await Embeddings(EmbedConfig.model, prompt, EmbedConfig.keep_alive);
            int l = ContextDatabase.Count;

            if (l == 1)
                return Context2Message(ContextDatabase[0]);

            if (l == 0)
            {
                Debug.LogError("Database is Empty...");
                return default;
            }

            float max = -1.0f;
            int index = -1;

            for (int i = 0; i < l; i++)
            {
                float score = CosineSimilarity(vector, VectorsDatabase[i]);
#if UNITY_EDITOR
                Debug.Log($"[{score}]: {ContextDatabase[i].Trim().Substring(0, 128)}");
#endif
                if (score > max)
                {
                    max = score;
                    index = i;
                }
            }

            return Context2Message(ContextDatabase[index]);
        }

        /// <summary>Ask a question based on the given context</summary>
        /// <param name="model">Ollama Model Syntax (<b>eg.</b> gemma3:4b)</param>
        /// <param name="keep_alive">The duration <i>(in seconds)</i> to keep the model in memory</param>
        /// <returns>response string from the LLM</returns>
        public static async Task<string> Ask(string model, string prompt, int keep_alive = 300)
        {
            var system = await Analyze(prompt);
            var query = new ChatMessage("user", $"Question:\n{prompt}");
            var messages = new List<ChatMessage>() { system, query };

            var request = new Request.Chat(model, messages, false, keep_alive, null);
            string payload = JsonConvert.SerializeObject(request);
            var response = await PostRequest<Response.Chat>(payload, Endpoints.CHAT);
            return response.message.content;
        }

        /// <summary>Stream an answer based on the given context</summary>
        /// <param name="onTextReceived">The callback to handle the streaming chunks</param>
        /// <param name="model">Ollama Model Syntax (<b>eg.</b> gemma3:4b)</param>
        /// <param name="keep_alive">The duration <i>(in seconds)</i> to keep the model in memory</param>
        public static async Task AskStream(Action<string> onTextReceived, string model, string prompt, int keep_alive = 300)
        {
            var system = await Analyze(prompt);
            var query = new ChatMessage("user", $"Question:\n{prompt}");
            var messages = new List<ChatMessage> { system, query };

            var request = new Request.Chat(model, messages, true, keep_alive, null);
            string payload = JsonConvert.SerializeObject(request);

            await PostRequestStream(payload, Endpoints.CHAT, (Response.Chat response) =>
            {
                if (!response.done)
                    onTextReceived?.Invoke(response.message.content);
            });

            OnStreamFinished?.Invoke();
        }

        private static ChatMessage Context2Message(string context)
        {
            return new ChatMessage("system",
                "You are an assistant for question-answering tasks. Use only the following piece of context to answer the question. " +
                "If no answer can be found within the context, simply say so. Do **NOT** answer outside of context. (Do not mention what the context is about either)\n" +
                $"Context:\n\"\"\"\n{context}\n\"\"\""
            );
        }

        public static void DebugContext()
        {
            foreach (var text in ContextDatabase)
                Debug.Log($"[{text.Length}]: {text.Trim()}");
        }
    }
}
