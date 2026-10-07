using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DdukDduk.Shop.AI
{
    public sealed class DialogueGenerationResult
    {
        public string Text { get; private set; }
        public bool UsedAi { get; private set; }
        public string Diagnostic { get; private set; }

        internal DialogueGenerationResult(string text, bool usedAi, string diagnostic)
        {
            Text = text ?? string.Empty;
            UsedAi = usedAi;
            Diagnostic = diagnostic ?? string.Empty;
        }
    }

    /// <summary>
    /// Uses only the model and Windows CPU helper shipped in StreamingAssets.
    /// The game chooses the answer; this service only writes the customer's words.
    /// </summary>
    public sealed class LocalCustomerDialogue : MonoBehaviour
    {
        private const int StartupTimeoutSeconds = 60;
        private const int GenerationTimeoutSeconds = 45;
        private readonly SemaphoreSlim requestGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly object processLock = new object();
        private readonly object logLock = new object();
        private readonly List<string>[] recentLines = {
            new List<string>(), new List<string>(), new List<string>()
        };
        private readonly System.Random random = new System.Random();
        private HttpClient client;
        private Process server;
        private string aiDirectory;
        private string logPath;
        private string baseUrl;
        private bool windowsSupported;
        private bool stopping;
        private bool ready;

        public string LastDiagnostic { get; private set; } = string.Empty;
        public bool IsReady { get { return ready; } }

        private void Awake()
        {
            aiDirectory = Path.Combine(Application.streamingAssetsPath, "LocalAi");
            logPath = Path.Combine(Application.persistentDataPath, "local-ai.log");
            windowsSupported = Application.platform == RuntimePlatform.WindowsEditor ||
                               Application.platform == RuntimePlatform.WindowsPlayer;
            // Loopback must work even if the player's system uses a web proxy.
            client = new HttpClient(new HttpClientHandler { UseProxy = false });
            client.Timeout = TimeSpan.FromSeconds(GenerationTimeoutSeconds);
        }

        public async Task<bool> WarmupAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken))
            {
                bool entered = false;
                try
                {
                    await requestGate.WaitAsync(linked.Token).ConfigureAwait(false);
                    entered = true;
                    await EnsureServerAsync(linked.Token).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception exception)
                {
                    LastDiagnostic = exception.Message;
                    return false;
                }
                finally { if (entered) requestGate.Release(); }
            }
        }

        public async Task<DialogueGenerationResult> GenerateAsync(
            int answer, int day, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (answer < 1 || answer > 3)
                throw new ArgumentOutOfRangeException(nameof(answer), "맛 번호는 1, 2, 3 중 하나여야 합니다.");
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken))
            {
                bool entered = false;
                try
                {
                    await requestGate.WaitAsync(linked.Token).ConfigureAwait(false);
                    entered = true;
                    await EnsureServerAsync(linked.Token).ConfigureAwait(false);
                    string requestJson = JsonUtility.ToJson(BuildRequest(answer, day));
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(linked.Token))
                    using (var payload = new StringContent(requestJson, Encoding.UTF8, "application/json"))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(GenerationTimeoutSeconds));
                        using (var response = await client.PostAsync(baseUrl + "/v1/chat/completions", payload,
                                                                     timeout.Token).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var completion = JsonUtility.FromJson<ChatResponse>(json);
                            string text = completion != null && completion.choices != null && completion.choices.Length > 0 &&
                                          completion.choices[0].message != null
                                ? completion.choices[0].message.content : null;
                            // Deliberately no semantic filter: the player asked to show unusual AI lines too.
                            text = (text ?? string.Empty).Trim();
                            if (text.Length == 0) throw new InvalidDataException("모델이 빈 대사를 반환했습니다.");
                            var history = recentLines[answer - 1];
                            history.Add(text);
                            if (history.Count > 5) history.RemoveAt(0);
                            LastDiagnostic = string.Empty;
                            return new DialogueGenerationResult(text, true, string.Empty);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (linked.IsCancellationRequested) throw;
                    LastDiagnostic = "로컬 AI 생성 시간이 초과되었습니다.";
                    return new DialogueGenerationResult(string.Empty, false, LastDiagnostic);
                }
                catch (Exception exception)
                {
                    LastDiagnostic = exception.Message;
                    return new DialogueGenerationResult(string.Empty, false, LastDiagnostic);
                }
                finally { if (entered) requestGate.Release(); }
            }
        }

        private ChatRequest BuildRequest(int answer, int day)
        {
            string[] conditions = {
                "많이 달지 않고 순한 쌀맛이 느껴지는 맛",
                "꿀처럼 입안이 달달해지는 맛",
                "볶은 콩의 향이 구수하게 느껴지는 맛"
            };
            string[] situations = {
                "산책을 마치고 간식으로", "차와 함께 먹으려고", "가족과 나누려고",
                "오후에 잠깐 쉬면서", "친구에게 선물하려고", "점심 뒤에 먹으려고",
                "어릴 때 먹던 간식이 생각나서", "책을 읽으며 먹으려고"
            };
            string situation = situations[random.Next(situations.Length)];
            string[] examples = {
                "너무 달지 않고 쌀맛이 은은한 떡을 추천해 주시겠어요?",
                "달달한 간식이 먹고 싶어요. 입안이 달콤해지는 떡이 있을까요?",
                "콩가루처럼 구수한 향이 나는 떡이 좋아요. 하나 골라 주실래요?"
            };
            var history = recentLines[answer - 1];
            string prior = history.Count == 0 ? string.Empty :
                "\n최근 대사와 문장을 다르게 쓰세요: " + string.Join(" / ", history.ToArray());
            return new ChatRequest {
                model = "rice-cake-customer",
                messages = new[] {
                    new ChatMessage {
                        role = "system",
                        content = "당신은 떡집에 찾아온 손님입니다. 한국어 한두 문장으로 점원에게 " +
                                  "떡 추천을 부탁하세요. 손님이 말하는 대사만 출력하세요."
                    },
                    // One matching example avoids confusing this small model with the other answers.
                    new ChatMessage { role = "user", content = "원하는 맛: " + conditions[answer - 1] + ". 떡 추천을 부탁하세요." },
                    new ChatMessage { role = "assistant", content = examples[answer - 1] },
                    new ChatMessage {
                        role = "user",
                        content = "원하는 맛은 " + conditions[answer - 1] +
                                  "입니다. " + situation + " 합니다. 다른 문장으로 맛을 묘사하며 " +
                                  "추천을 부탁하세요." + prior + " /no_think"
                    }
                },
                max_tokens = 90,
                temperature = 0.7f,
                top_p = 0.8f,
                top_k = 20,
                min_p = 0,
                repeat_penalty = 1.05f,
                seed = random.Next() ^ Math.Max(0, day),
                stream = false,
                chat_template_kwargs = new TemplateOptions { enable_thinking = false }
            };
        }

        private async Task EnsureServerAsync(CancellationToken cancellationToken)
        {
            if (!windowsSupported) throw new PlatformNotSupportedException("이 AI 실행 파일은 Windows 64비트용입니다.");
            if (stopping) throw new OperationCanceledException(cancellationToken);
            if (ready && IsServerAlive()) return;
            StopServer();
            string executable = Path.Combine(aiDirectory, "Runtime", "win-x64", "llama-server.exe");
            string model = Path.Combine(aiDirectory, "Models", "model.gguf");
            if (!File.Exists(executable)) throw new FileNotFoundException("동봉된 로컬 AI 실행 파일이 없습니다.", executable);
            if (!File.Exists(model)) throw new FileNotFoundException("동봉된 로컬 AI 모델이 없습니다.", model);
            int port = FindAvailablePort();
            baseUrl = "http://127.0.0.1:" + port;
            int threads = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));
            var helper = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = executable,
                    WorkingDirectory = Path.GetDirectoryName(executable),
                    Arguments = "-m " + QuoteArgument(model) + " --host 127.0.0.1 --port " + port +
                                " -c 2048 --parallel 1 --threads " + threads +
                                " --gpu-layers 0 --cache-ram 0 --reasoning-budget 0 --no-webui --offline " +
                                "--alias rice-cake-customer --log-colors off",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };
            helper.OutputDataReceived += OnServerLog;
            helper.ErrorDataReceived += OnServerLog;
            lock (processLock)
            {
                if (stopping) { helper.Dispose(); throw new OperationCanceledException(cancellationToken); }
                server = helper;
                helper.Start();
                helper.BeginOutputReadLine();
                helper.BeginErrorReadLine();
            }
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed.TotalSeconds < StartupTimeoutSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsServerAlive()) throw new InvalidOperationException("로컬 AI 실행 엔진을 시작하지 못했습니다. local-ai.log를 확인하세요.");
                try
                {
                    using (var healthTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        healthTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                        using (var response = await client.GetAsync(baseUrl + "/health", healthTimeout.Token).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode) { ready = true; LastDiagnostic = string.Empty; return; }
                        }
                    }
                }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) { cancellationToken.ThrowIfCancellationRequested(); }
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
            StopServer();
            throw new TimeoutException("로컬 AI 모델을 준비하는 시간이 초과되었습니다.");
        }

        private bool IsServerAlive()
        {
            lock (processLock)
            {
                try { return server != null && !server.HasExited; }
                catch (InvalidOperationException) { return false; }
            }
        }

        private static int FindAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string QuoteArgument(string value)
        {
            // Paths come from Unity, never from a player's dialogue.
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void OnServerLog(object sender, DataReceivedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.Data)) return;
            try
            {
                lock (logLock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    if (File.Exists(logPath) && new FileInfo(logPath).Length > 4 * 1024 * 1024)
                        File.WriteAllText(logPath, string.Empty);
                    File.AppendAllText(logPath, args.Data + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void StopServer()
        {
            ready = false;
            lock (processLock)
            {
                if (server == null) return;
                // Only the process started by this component is stopped.
                try { if (!server.HasExited) server.Kill(); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                server.Dispose();
                server = null;
            }
        }

        private void OnApplicationQuit() { Shutdown(); }
        private void OnDestroy() { Shutdown(); }

        private void Shutdown()
        {
            if (stopping) return;
            stopping = true;
            lifetime.Cancel();
            StopServer();
            if (client != null) client.Dispose();
        }

        [Serializable] private sealed class ChatRequest
        {
            public string model;
            public ChatMessage[] messages;
            public int max_tokens;
            public float temperature;
            public float top_p;
            public int top_k;
            public float min_p;
            public float repeat_penalty;
            public int seed;
            public bool stream;
            public TemplateOptions chat_template_kwargs;
        }
        [Serializable] private sealed class TemplateOptions { public bool enable_thinking; }
        [Serializable] private sealed class ChatMessage { public string role; public string content; }
        [Serializable] private sealed class ChatResponse { public ChatChoice[] choices; }
        [Serializable] private sealed class ChatChoice { public ChatMessage message; }
    }
}
