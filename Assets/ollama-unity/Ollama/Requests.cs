using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ollama
{
    /// <summary> https://github.com/ollama/ollama/blob/main/docs/api.md </summary>
    public static partial class Ollama
    {
        private const string SERVER = "http://localhost:11434/";
        
        private static readonly HttpClient httpClient = new HttpClient();

        private static class Endpoints
        {
            public const string GENERATE = "api/generate";
            public const string CHAT = "api/chat";
            public const string LIST = "api/tags";
            public const string EMBEDDINGS = "api/embed";
        }

        private static async Task<T> PostRequest<T>(string payload, string endpoint) where T : Response.Base
        {
            HttpWebRequest httpWebRequest;

            try
            {
                httpWebRequest = (HttpWebRequest)WebRequest.Create($"{SERVER}{endpoint}");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(await httpWebRequest.GetRequestStreamAsync().ConfigureAwait(false)))
                    await streamWriter.WriteAsync(payload).ConfigureAwait(false);

                string result;

                using (var httpResponse = await httpWebRequest.GetResponseAsync().ConfigureAwait(false))
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    result = await streamReader.ReadToEndAsync().ConfigureAwait(false);

                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (WebException webEx)
            {
                string errorResponse = "";

                if (webEx.Response != null)
                {
                    using (var errorStream = webEx.Response.GetResponseStream())
                    using (var reader = new StreamReader(errorStream))
                        errorResponse = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                Debug.LogError($"HTTP Error during \"{endpoint}\" PostRequest:\n{webEx.Message}\n{errorResponse}\n{webEx.StackTrace}");
                return default;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during \"{endpoint}\" PostRequest:\n{e.Message}\n{e.StackTrace}");
                return default;
            }
        }

        private static async Task PostRequestStream<T>(string payload, string endpoint, Action<T> onChunkReceived) where T : Response.BaseResponse
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{SERVER}{endpoint}");
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                // SendAsync with HttpCompletionOption.ResponseHeadersRead is crucial for streaming data
                using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    bool isEnd = false;
                    int it = 0;

                    while (!isEnd && !reader.EndOfStream)
                    {
                        it++;
                        string result = await reader.ReadLineAsync().ConfigureAwait(false);
                
                        if (string.IsNullOrWhiteSpace(result)) continue;

                        var chunkResponse = JsonConvert.DeserializeObject<T>(result);
                
                        if (it > MaxIterations)
                        {
                            Debug.LogError($"Stream has reached {MaxIterations} iterations... Probably server error?");
                            chunkResponse.done = true;
                        }

                        onChunkReceived?.Invoke(chunkResponse);
                        isEnd = chunkResponse.done;
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.LogError($"HTTP Error during \"{endpoint}\" PostRequestStream:\n{httpEx.Message}\n{httpEx.StackTrace}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during \"{endpoint}\" PostRequestStream:\n{e.Message}\n{e.StackTrace}");
            }
        }

        private static async Task<T> GetRequest<T>(string endpoint) where T : Response.Base
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get($"{SERVER}{endpoint}"))
            {
                // To use await with UnityWebRequest, we yield until it's done
                var operation = webRequest.SendWebRequest();

                while (!operation.isDone)
                    await Task.Yield();

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Error during \"{endpoint}\" GetRequest: {webRequest.error}");
                    return default; // Still returns null, but with a clear error
                }

                try
                {
                    string result = webRequest.downloadHandler.text;
                    return JsonConvert.DeserializeObject<T>(result);
                }
                catch (Exception e)
                {
                    Debug.LogError($"JSON Parsing Error in GetRequest: {e.Message}");
                    return default;
                }
            }
        }
    }
}
