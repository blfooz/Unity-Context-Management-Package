using System.Collections.Generic;
using System.Text;
using System.Threading;
using ContextManagement;
using Unity.Properties;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument), typeof(ChatRequestManager))]
public class ChatInterface : MonoBehaviour
{
    public Color userColor, defaultColor;
    public VisualTreeAsset messageBubbleTemplate;

    [CreateProperty]
    public string input;
    public InputAction submitAction;

    [Tooltip("When enabled, the assistant's reply appears token-by-token as it streams in.")]
    public bool streamResponse = false;

    List<MessageBubble> messageBubbles = new();

    ListView list;
    CancellationTokenSource _streamCts;

    void OnEnable()
    {
        submitAction.Enable();
    }

    private void Awake()
    {
        submitAction.performed += (_) => Submit();
        var root = GetComponent<UIDocument>().rootVisualElement;
        list = root.Q<ListView>("bubbles");
        list.itemsSource = messageBubbles;
        root.Q<TextField>("input").dataSource = this;
        root.Q<Button>("sendButton").clicked += Submit;

        MessageBubble.userColor = userColor;
        MessageBubble.defaultColor = defaultColor;
    }

    void OnDestroy()
    {
        CancelStream();
        _streamCts?.Dispose();
    }

    void CancelStream()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
    }

    void ScrollToBottom()
    {
        list.ScrollToItem(-1);
    }

    void Submit()
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            // Cancel any in-flight streaming before starting a new request
            CancelStream();

            AddMessage(Role.user, input);
            Message(input);

            input = "";
            ScrollToBottom();
        }
    }

    public MessageBubble AddMessage(Role role, string message, string reasoningContent = null)
    {
        MessageBubble msg = new() { role = role, message = message, reasoningContent = reasoningContent };
        messageBubbles.Add(msg);
        ScrollToBottom();
        return msg;
    }

    public async void Message(string msg)
    {
        if (streamResponse)
        {
            await SendMessageStreaming(msg);
        }
        else
        {
            var (reply, reasoning) = await GetComponent<ChatRequestManager>().Message(msg);
            AddMessage(Role.assistant, reply, reasoning);
        }
    }

    async System.Threading.Tasks.Task SendMessageStreaming(string msg)
    {
        var chat = GetComponent<ChatRequestManager>();

        // Create an empty assistant bubble — tokens will fill it in
        var bubble = AddMessage(Role.assistant, "");
        StringBuilder fullReply = new(), fullReasoning = new();

        _streamCts = new CancellationTokenSource();
        try
        {
            await foreach ((var reply, var reasoning) in chat.MessageStreaming(msg, _streamCts.Token))
            {
                fullReply.Append(reply);
                fullReasoning.Append(reasoning);
                bubble.message = fullReply.ToString();
                bubble.reasoningContent = fullReasoning.ToString();
                ScrollToBottom();
            }
        }
        catch (System.OperationCanceledException)
        {
            if (fullReply.Length == 0)
                bubble.message = "[cancelled]";
            else
                bubble.message = fullReply.ToString() + "…";
        }
        finally
        {
            _streamCts?.Dispose();
            _streamCts = null;
        }
    }

    public class MessageBubble
    {
        public static Color userColor, defaultColor;
        public Role role = Role.assistant;
        [CreateProperty]
        public string reasoningContent;
        [CreateProperty]
        public string message;
        [CreateProperty]
        public StyleEnum<Align> Alignment => role == Role.user ? Align.FlexEnd : Align.FlexStart;

        [CreateProperty]
        public StyleColor BubbleColor => (role == Role.user) ? userColor : defaultColor;

        [CreateProperty]
        public StyleEnum<DisplayStyle> Visible => string.IsNullOrWhiteSpace(reasoningContent) ? DisplayStyle.None : DisplayStyle.Flex;

        [CreateProperty]
        public object image;
    }
}
