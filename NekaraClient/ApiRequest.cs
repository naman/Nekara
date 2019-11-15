﻿using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nekara.Client
{
    public class ApiRequest
    {
        public Task<JToken> Task { get; private set; }
        public CancellationTokenSource Cts { get; private set; }
        public string Label { get; set; }

        public ApiRequest(Task<JToken> task, CancellationTokenSource cts, string label = "Anonymous")
        {
            this.Task = task;
            this.Cts = cts;
            this.Label = label;
        }

        public void Cancel()
        {
            this.Cts.Cancel();
        }
    }
}
