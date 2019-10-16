﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nekara
{
    public abstract class JsonP2P
    {
        private string id;
        private Dictionary<string, RemoteMethodAsync> remoteMethods;
        private Dictionary<string, (TaskCompletionSource<JToken>, CancellationTokenSource)> requests;
        private Dictionary<string, JsonPeer> peers;

        public JsonP2P()
        {
            this.id = Helpers.RandomString(16);
            this.remoteMethods = new Dictionary<string, RemoteMethodAsync>();
            this.requests = new Dictionary<string, (TaskCompletionSource<JToken>, CancellationTokenSource)>();
            this.peers = new Dictionary<string, JsonPeer>();
        }

        public abstract Task Send(string recipient, string payload);

        public void RegisterRemoteMethod(string name, RemoteMethodAsync handler)
        {
            // RemoteMethodAsync method = new RemoteMethodAsync(handler);
            this.remoteMethods[name] = handler;
        }

        public void HandleMessage(string payload)
        {
            // Console.WriteLine("    Trying to handle message: {0}", payload);
            try
            {
                HandleRequest(payload);
            }
            catch (UnexpectedRequestException e1)
            {
                try
                {
                    HandleResponse(payload);
                }
                catch (UnexpectedResponseException e2)
                {
                    throw new UnexpectedMessageException("Unexpected Message : " + payload);
                }
            }
        }

        public void HandleRequest(string payload)
        {
            RequestMessage message = JsonConvert.DeserializeObject<RequestMessage>(payload);
            if (message.func != null && this.remoteMethods.ContainsKey(message.func))
            {
                this.remoteMethods[message.func](message.args.ToArray());
            }
            else
            {
                throw new UnexpectedRequestException("Unexpected Request : " + payload);
            }
        }

        public void HandleResponse(string payload)
        {
            ResponseMessage message = JsonConvert.DeserializeObject<ResponseMessage>(payload);
            Console.WriteLine("\n--> Got Response to {0} {1}", message.responseTo, message.error);
            if (message.responseTo != null && this.requests.ContainsKey(message.responseTo))
            {
                var (tcs, cts) = this.requests[message.responseTo];
                if (cts.IsCancellationRequested) {
                    Console.WriteLine("  ! Response {0} was cancelled", message.responseTo);
                    tcs.SetCanceled();
                }
                else
                {
                    if (message.error) tcs.SetException(Exceptions.DeserializeServerSideException(message.data));
                    // if (message.error) this.requests[message.responseTo].SetException(new ServerThrownException(message.data));
                    else tcs.SetResult(message.data);
                    // Console.WriteLine("    ... resolved response to {0} {1}", message.responseTo, message.error);
                }
            }
            else
            {
                throw new UnexpectedResponseException("Unexpected Response : " + payload);
            }
        }

        public (Task<JToken>, CancellationTokenSource) Request(string recipient, string func, JToken[] args, int timeout = 300000)
        {
            Console.WriteLine("\n<-- Requesting {0} ({1})", func, String.Join(", ", args.Select(arg => arg.ToString())));
            var tcs = new TaskCompletionSource<JToken>();   // This tcs will be settled when the response comes back
            var cts = new CancellationTokenSource();

            var message = new RequestMessage(this.id, recipient, func, args);
            var serialized = JsonConvert.SerializeObject(message);
            this.requests.Add(message.id, (tcs, cts));
            this.Send(recipient, serialized);

            var timer = new Timer(_ => {
                if (!cts.IsCancellationRequested) tcs.SetException(new RequestTimeoutException($"Request [{func}] timed out"));
            }, null, timeout, Timeout.Infinite);   // Set a timeout for the request
            tcs.Task.ContinueWith(prev => {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
                this.requests.Remove(message.id);
                timer.Dispose();
                // Console.WriteLine("  Request {0} was fulfilled with error={1}, cancelled={2}", message.id, prev.IsFaulted, prev.IsCanceled);
            });
            return (tcs.Task, cts);
        }

        public Task Respond(string recipient, string requestId, JToken data)
        {
            // Console.WriteLine("    Responding to {0} ({1})", requestId, data);
            var message = new ResponseMessage(this.id, recipient, requestId, data);
            var serialized = JsonConvert.SerializeObject(message);
            return this.Send(recipient, serialized);
        }

        public void AddPeer(string peerId, JsonPeer peer)
        {
            this.peers.Add(peerId, peer);
        }

        public JsonPeer GetPeer(string peerId)
        {
            return this.peers[peerId];
        }

        public void RemovePeer(string peerId)
        {
            this.peers.Remove(peerId);
        }
    }

    public delegate Task<JToken> RemoteMethodAsync(params JToken[] args);

    public class RemoteMethodAttribute : Attribute
    {
        public string name;
        public string description;
    }

    // This Message construct is 1 layer above the communication layer
    // This extra level of abstraction is useful for remaining protocol-agnostic,
    // so that we can plug-in application layer transport protocols
    [DataContract]
    public class RequestMessage
    {
        [DataMember]
        internal string id;

        [DataMember]
        internal string sender;

        [DataMember]
        internal string recipient;

        [DataMember]
        internal string func;

        [DataMember]
        internal JToken[] args;

        public RequestMessage(string sender, string recipient, string func, JToken[] args)
        {
            this.id = "req-" + Helpers.RandomString(16);
            this.sender = sender;
            this.recipient = recipient;
            this.func = func;
            this.args = args;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static RequestMessage Deserialize(string payload)
        {
            return JsonConvert.DeserializeObject<RequestMessage>(payload);
        }

        public ResponseMessage CreateResponse(string sender, JToken data)
        {
            return new ResponseMessage(sender, this.sender, this.id, data);
        }

        public ResponseMessage CreateErrorResponse(string sender, JToken data)
        {
            return new ResponseMessage(sender, this.sender, this.id, data, true);
        }
    }

    // This Message construct is 1 layer above the communication layer
    // This extra level of abstraction is useful for remaining protocol-agnostic,
    // so that we can plug-in application layer transport protocols
    [DataContract]
    public class ResponseMessage
    {
        [DataMember]
        internal string id;

        [DataMember]
        internal string sender;

        [DataMember]
        internal string recipient;

        [DataMember]
        internal string responseTo;

        [DataMember]
        internal JToken data;

        [DataMember]
        internal bool error;

        public ResponseMessage(string sender, string recipient, string requestId, JToken data, bool isError = false)
        {
            this.id = "res-" + Helpers.RandomString(16);
            this.sender = sender;
            this.recipient = recipient;
            this.responseTo = requestId;
            this.data = data;
            this.error = isError;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static ResponseMessage Deserialize(string payload)
        {
            return JsonConvert.DeserializeObject<ResponseMessage>(payload);
        }
    }

    public class JsonPeer
    {
        private string id;
        private JsonP2P host;

        public JsonPeer (string id, JsonP2P host)
        {
            this.id = id;
            this.host = host;
        }

        public Task Send(string payload)
        {
            return this.host.Send(this.id, payload);
        }
    }
}