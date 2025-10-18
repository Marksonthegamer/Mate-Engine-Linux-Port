using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine.UI;
using System.Collections;

namespace LLMUnitySamples
{
    public class ChatBot : MonoBehaviour
    {
        [Header("Containers")]
        public Transform chatContainer;          // ScrollRect Content: hier kommen die Chat-Bubbles rein
        public Transform inputContainer;         // Fixer Bereich außerhalb des ScrollRects: hier liegt die Input-Bubble

        [Header("Colors & Font")]
        public Color playerColor = new Color32(81, 164, 81, 255);
        public Color aiColor = new Color32(29, 29, 73, 255);
        public Color fontColor = Color.white;
        public Font font;
        public int fontSize = 16;

        [Header("Bubble Layout")]
        public int bubbleWidth = 600;
        public float textPadding = 10f;
        public float bubbleSpacing = 10f;
        public float bottomPadding = 10f;        // zusätzlicher Puffer ganz unten im Verlauf
        public Sprite sprite;
        public Sprite roundedSprite16;
        public Sprite roundedSprite32;
        public Sprite roundedSprite64;

        [Header("LLM")]
        public LLMCharacter llmCharacter;

        [Header("Input Settings")]
        public string inputPlaceholder = "Message me";

        [Header("Streaming Audio")]
        public AudioSource streamAudioSource;

        [Header("Bubble Materials")]
        public Material playerMaterial;          // Hintergrund / Image
        public Material aiMaterial;              // Hintergrund / Image

        [Header("Text Materials")]
        public Material playerTextMaterial;      // Legacy UI Text Material (z.B. Shader "UI/LegacyTextShaderFade2Way")
        public Material aiTextMaterial;          // Legacy UI Text Material

        [Header("Scroll")]
        public ScrollRect scrollRect;            // im Inspector zuweisen
        public bool autoScrollOnNewMessage = true;       // auto-zu-Unten springen bei neuen Nachrichten …
        public bool respectUserScroll = true;             // … aber nur, wenn User schon “am Ende” ist

        [Header("History")]
        [Min(0)] public int maxMessages = 100;           // konfigurierbares Limit (Standard 100)
        public bool trimOnlyWhenAtBottom = true;         // nur trimmen, wenn der User unten ist
        public bool enableOffscreenTrim = false;         // alte Logik optional aktivierbar (Kompatibilität)

        [Header("Font Colors (per side)")]
        public Color playerFontColor = Color.white;
        public Color aiFontColor = Color.white;

        [Header("Rounded Sprite Radius")]
        [Range(0, 64)]
        public int cornerRadius = 16; // wählt 9-slice Sprite
        private bool layoutDirty;

        private InputBubble inputBubble;
        private List<Bubble> chatBubbles = new List<Bubble>();
        private bool blockInput = true;
        private BubbleUI playerUI, aiUI;
        private bool warmUpDone = false;

        // Kompatibilitätsfeld: wird nur genutzt, wenn enableOffscreenTrim == true
        private int lastBubbleOutsideFOV = -1;

        void Start()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Rounded Sprite anhand Radius wählen (wie bisher)
            if (cornerRadius <= 16) sprite = roundedSprite16;
            else if (cornerRadius <= 32) sprite = roundedSprite32;
            else sprite = roundedSprite64;

            playerUI = new BubbleUI
            {
                sprite = sprite,
                font = font,
                fontSize = fontSize,
                fontColor = playerFontColor,
                bubbleColor = playerColor,
                bottomPosition = 0,
                leftPosition = 0,
                textPadding = textPadding,
                bubbleOffset = bubbleSpacing,
                bubbleWidth = bubbleWidth,
                bubbleHeight = -1
            };

            aiUI = new BubbleUI
            {
                sprite = sprite,
                font = font,
                fontSize = fontSize,
                fontColor = aiFontColor,
                bubbleColor = aiColor,
                bottomPosition = 0,
                leftPosition = 1,
                textPadding = textPadding,
                bubbleOffset = bubbleSpacing,
                bubbleWidth = bubbleWidth,
                bubbleHeight = -1
            };

            // Fallback: falls inputContainer im Inspector noch nicht gesetzt ist, nutze chatContainer (altes Verhalten)
            Transform inputParent = inputContainer != null ? inputContainer : chatContainer;

            // WICHTIG: Input-Bubble jetzt im separaten Container anlegen
            inputBubble = new InputBubble(inputParent, playerUI, "InputBubble", "Loading...", 4);
            inputBubble.AddSubmitListener(onInputFieldSubmit);
            inputBubble.AddValueChangedListener(onValueChanged);
            inputBubble.setInteractable(false);

            ShowLoadedMessages();
            _ = llmCharacter.Warmup(WarmUpCallback);
        }

        private void MarkLayoutDirty()
        {
            layoutDirty = true;
        }

        void OnDisable()
        {
            if (streamAudioSource != null && streamAudioSource.isPlaying)
            {
                streamAudioSource.Stop();
                streamAudioSource.volume = 1f; // reset
            }
        }

        Bubble AddBubble(string message, bool isPlayerMessage)
        {
            Bubble bubble = new Bubble(chatContainer, isPlayerMessage ? playerUI : aiUI, isPlayerMessage ? "PlayerBubble" : "AIBubble", message);
            chatBubbles.Add(bubble);
            bubble.OnResize(MarkLayoutDirty);


            // --- Material für Bubble-Hintergrund (Image) setzen ---
            var image = bubble.GetRectTransform().GetComponentInChildren<Image>(true);
            if (image != null)
            {
                image.material = isPlayerMessage ? playerMaterial : aiMaterial;
            }

            // --- Material für Legacy UI Text setzen ---
            // Hinweis: Das Text-Material überschreibt das vom Font vorgegebene Material.
            // Erwartet einen UI-Text-kompatiblen Shader (z.B. die von uns gebauten Legacy-Text-Shader).
            var text = bubble.GetRectTransform().GetComponentInChildren<Text>(true);
            if (text != null)
            {
                Material m = isPlayerMessage ? playerTextMaterial : aiTextMaterial;
                if (m != null)
                {
                    text.material = m;
                }
                // Optional: Font/Farbe bleiben weiterhin aus playerUI/aiUI erhalten.
            }

            // Nur auto-scrollen, wenn erlaubt und (falls gewünscht) der Nutzer aktuell am Ende ist
            if (autoScrollOnNewMessage && (!respectUserScroll || IsAtBottom()))
            {
                StartCoroutine(ScrollToBottomNextFrame());
            }

            // Neues, konfigurierbares Limit anwenden
            TrimHistoryIfNeeded();

            return bubble;
        }

        void TrimHistoryIfNeeded()
        {
            if (maxMessages <= 0) return;

            if (chatBubbles.Count > maxMessages)
            {
                // Nur am Ende trimmen, wenn gewünscht
                if (!trimOnlyWhenAtBottom || IsAtBottom())
                {
                    int removeCount = chatBubbles.Count - maxMessages;
                    for (int i = 0; i < removeCount; i++)
                    {
                        chatBubbles[i].Destroy();
                    }
                    chatBubbles.RemoveRange(0, removeCount);

                    // Nach dem Trimmen neu layouten
                    UpdateBubblePositions();
                }
            }
        }

        bool IsAtBottom(float tolerance = 0.01f)
        {
            if (scrollRect == null) return true; // kein ScrollRect: verhalte dich wie "am Ende"
            // 0 = unten, 1 = oben
            return scrollRect.verticalNormalizedPosition <= tolerance;
        }

        void ShowLoadedMessages()
        {
            // Lade nur die letzten maxMessages (falls viele vorhanden sind)
            int start = 1;
            int total = llmCharacter.chat.Count;
            if (maxMessages > 0)
                start = Mathf.Max(1, total - maxMessages);

            for (int i = start; i < total; i++)
            {
                AddBubble(llmCharacter.chat[i].content, i % 2 == 1);
            }

            // Nach dem Laden nach unten scrollen
            StartCoroutine(ScrollToBottomNextFrame());
        }

        void onInputFieldSubmit(string newText)
        {
            inputBubble.ActivateInputField();
            if (blockInput || newText.Trim() == "" || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                StartCoroutine(BlockInteraction());
                return;
            }
            blockInput = true;

            string message = inputBubble.GetText().Replace("\v", "\n");

            AddBubble(message, true);
            Bubble aiBubble = AddBubble("...", false);

            // Stream-Audio starten (optional)
            if (streamAudioSource != null)
                streamAudioSource.Play();

            Task chatTask = llmCharacter.Chat(
                message,
                (partial) => { aiBubble.SetText(partial); layoutDirty = true; },
                () =>
                {
                    aiBubble.SetText(aiBubble.GetText());
                    layoutDirty = true;

                    if (streamAudioSource != null && streamAudioSource.isPlaying)
                        StartCoroutine(FadeOutStreamAudio());

                    AllowInput();
                }
            );



            inputBubble.SetText("");
        }

        private IEnumerator FadeOutStreamAudio(float duration = 0.5f)
        {
            float startVolume = streamAudioSource.volume;

            while (streamAudioSource.volume > 0f)
            {
                streamAudioSource.volume -= startVolume * Time.deltaTime / duration;
                yield return null;
            }

            streamAudioSource.Stop();
            streamAudioSource.volume = startVolume; // reset
        }

        public void WarmUpCallback()
        {
            warmUpDone = true;
            inputBubble.SetPlaceHolderText(inputPlaceholder);
            AllowInput();
        }

        public void AllowInput()
        {
            blockInput = false;
            inputBubble.ReActivateInputField();
        }

        public void CancelRequests()
        {
            llmCharacter.CancelRequests();
            AllowInput();
        }

        IEnumerator<string> BlockInteraction()
        {
            inputBubble.setInteractable(false);
            yield return null;
            inputBubble.setInteractable(true);
            inputBubble.MoveTextEnd();
        }

        void onValueChanged(string newText)
        {
            // Enter bereinigen, wenn leer
            if (Input.GetKey(KeyCode.Return))
            {
                if (inputBubble.GetText().Trim() == "")
                    inputBubble.SetText("");
            }
        }

        public void UpdateBubblePositions()
        {
            // Neues Layout: Bubbles stehen im scrollbaren chatContainer,
            // Input-Bubble ist separat – daher kein Offset mehr von der Input-Größe nötig.
            float y = bottomPadding;

            // Von unten nach oben aufbauen (wie gehabt, nur ohne Input-Offset)
            for (int i = chatBubbles.Count - 1; i >= 0; i--)
            {
                Bubble bubble = chatBubbles[i];
                RectTransform childRect = bubble.GetRectTransform();
                childRect.anchoredPosition = new Vector2(childRect.anchoredPosition.x, y);

                // Offscreen-Trim-Unterstützung nur, wenn explizit aktiviert (Kompatibilität)
                if (enableOffscreenTrim)
                {
                    float containerHeight = chatContainer.GetComponent<RectTransform>().rect.height;
                    if (y > containerHeight && lastBubbleOutsideFOV == -1)
                    {
                        lastBubbleOutsideFOV = i;
                    }
                }

                y += bubble.GetSize().y + bubbleSpacing;
            }

            // Content-Größe aktualisieren (wichtig für ScrollRect)
            var contentRect = chatContainer.GetComponent<RectTransform>();
            // Wir erhöhen nur die Höhe nach Bedarf; Breite bleibt Layout-bedingt
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + bottomPadding);
        }

        void Update()
        {
            // Fokus-Handling wie gehabt
            if (!inputBubble.inputFocused() && warmUpDone)
            {
                inputBubble.ActivateInputField();
                StartCoroutine(BlockInteraction());
            }

            // Optional: alte Offscreen-Trim-Logik
            if (enableOffscreenTrim && lastBubbleOutsideFOV != -1)
            {
                for (int i = 0; i <= lastBubbleOutsideFOV; i++)
                {
                    chatBubbles[i].Destroy();
                }
                chatBubbles.RemoveRange(0, lastBubbleOutsideFOV + 1);
                lastBubbleOutsideFOV = -1;

                // Nach Trim neu layouten
                UpdateBubblePositions();
            }
        }

        public void ExitGame()
        {
            Debug.Log("Exit button clicked");
            Application.Quit();
        }

        IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f; // 0 = unten
        }

        bool onValidateWarning = true;
        void OnValidate()
        {
            // Sprite für Bubbles konsistent halten, falls CornerRadius geändert wurde
            if (cornerRadius <= 16) sprite = roundedSprite16;
            else if (cornerRadius <= 32) sprite = roundedSprite32;
            else sprite = roundedSprite64;

            if (onValidateWarning && llmCharacter != null && !llmCharacter.remote && llmCharacter.llm != null && llmCharacter.llm.model == "")
            {
                Debug.LogWarning($"Please select a model in the {llmCharacter.llm.gameObject.name} GameObject!");
                onValidateWarning = false;
            }
        }

        void LateUpdate()
        {
            if (!layoutDirty) return;
            layoutDirty = false;

            UpdateBubblePositions(); // jetzt 1× pro Frame
                                     // Bei Bedarf unten bleiben, wenn User bereits am Ende war:
            if (autoScrollOnNewMessage && (!respectUserScroll || IsAtBottom()))
            {
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
            }
        }

    }


}
