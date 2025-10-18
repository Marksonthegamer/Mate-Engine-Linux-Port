using DiscordRPC;
using DiscordRPC.Message;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Lachee.Discord.Control
{
    [Serializable]
    public sealed class MessageEvents
    {
        [Serializable]
        public sealed class ReadyMessageEvent : UnityEvent<ReadyMessage> { }

        [Serializable]
        public sealed class CloseMessageEvent : UnityEvent<CloseMessage> { }

        [Serializable]
        public sealed class ErrorMessageEvent : UnityEvent<ErrorMessage> { }

        [Serializable]
        public sealed class PresenceMessageEvent : UnityEvent<PresenceMessage> { }

        [Serializable]
        public sealed class SubscribeMessageEvent : UnityEvent<SubscribeMessage> { }

        [Serializable]
        public sealed class UnsubscribeMessageEvent : UnityEvent<UnsubscribeMessage> { }

        [Serializable]
        public sealed class JoinMessageEvent : UnityEvent<JoinMessage> { }

        [Serializable]
        public sealed class SpectateMessageEvent : UnityEvent<SpectateMessage> { }

        [Serializable]
        public sealed class JoinRequestMessageEvent : UnityEvent<JoinRequestMessage> { }

        [Serializable]
        public sealed class ConnectionEstablishedMessageEvent : UnityEvent<ConnectionEstablishedMessage> { }

        [Serializable]
        public sealed class ConnectionFailedMessageEvent : UnityEvent<ConnectionFailedMessage> { }

        public ReadyMessageEvent OnReady = new ReadyMessageEvent();
        public CloseMessageEvent OnClose = new CloseMessageEvent();
        public ErrorMessageEvent OnError = new ErrorMessageEvent();
        public PresenceMessageEvent OnPresenceUpdate = new PresenceMessageEvent();
        public SubscribeMessageEvent OnSubscribe = new SubscribeMessageEvent();
        public UnsubscribeMessageEvent OnUnsubscribe = new UnsubscribeMessageEvent();
        public JoinMessageEvent OnJoin = new JoinMessageEvent();
        public SpectateMessageEvent OnSpectate = new SpectateMessageEvent();
        public JoinRequestMessageEvent OnJoinRequest = new JoinRequestMessageEvent();
        public ConnectionEstablishedMessageEvent OnConnectionEstablished = new ConnectionEstablishedMessageEvent();
        public ConnectionFailedMessageEvent OnConnectionFailed = new ConnectionFailedMessageEvent();

        public void RegisterEvents(DiscordRpcClient client)
        {
            client.OnReady += (s, args) => OnReady.Invoke(args);
            client.OnClose += (s, args) => OnClose.Invoke(args);
            client.OnError += (s, args) => OnError.Invoke(args);

            client.OnPresenceUpdate += (s, args) => OnPresenceUpdate.Invoke(args);
            client.OnSubscribe += (s, args) => OnSubscribe.Invoke(args);
            client.OnUnsubscribe += (s, args) => OnUnsubscribe.Invoke(args);

            client.OnJoin += (s, args) => OnJoin.Invoke(args);
            client.OnSpectate += (s, args) => OnSpectate.Invoke(args);
            client.OnJoinRequested += (s, args) => OnJoinRequest.Invoke(args);

            client.OnConnectionEstablished += (s, args) => OnConnectionEstablished.Invoke(args);
            client.OnConnectionFailed += (s, args) => OnConnectionFailed.Invoke(args);
        }
    }
}