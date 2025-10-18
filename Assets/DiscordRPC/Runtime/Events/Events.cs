using DiscordRPC;
using DiscordRPC.Message;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Lachee.Discord.Events
{
    public sealed class ReadyEvent
    {
        public User user { get; }
        public Configuration configuration { get; }
        public int version { get; }

        internal ReadyEvent(ReadyMessage message)
        {
            user = new User(message.User);
            configuration = message.Configuration;
            version = message.Version;
        }
    }

    public sealed class PresenceEvent
    {
        public Presence presence { get; }
        public string name { get; }
        public string applicationID { get; }
        internal PresenceEvent(PresenceMessage message)
        {
            presence = new Presence(message.Presence);
            name = message.Name;
            applicationID = message.ApplicationID;
        }
    }

    public sealed class JoinRequestEvent
    {
        public User user { get; }
        internal JoinRequestEvent(JoinRequestMessage message)
        {
            user = new User(message.User);
        }
    }


    // By Unity 2020, you shouldn't be using these anymore
#if !UNITY_2020_OR_NEWER
    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnityReadyEvent : UnityEvent<ReadyEvent> { }

    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnityCloseEvent : UnityEvent<CloseMessage> { }

    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnityErrorEvent : UnityEvent<ErrorMessage> { }

    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnityPresenceEvent : UnityEvent<PresenceEvent> { }

    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnitySubscribeEvent : UnityEvent<SubscribeMessage> { }

    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnityUnsubscribeEvent : UnityEvent<UnsubscribeMessage> { }

    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnityJoinEvent : UnityEvent<JoinMessage> { }

    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnitySpectateEvent : UnityEvent<SpectateMessage> { }

    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnityJoinRequestEvent : UnityEvent<JoinRequestEvent> { }

    [Serializable]
    [System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnityConnectionEstablishedEvent : UnityEvent<ConnectionEstablishedMessage> { }

    [Serializable][System.Obsolete("This is a wrapper for Unity 2018")]
    public class UnityConnectionFailedEvent : UnityEvent<ConnectionFailedMessage> { }
#endif
}