﻿using System;
using Nekara.Networking;
using Nekara.Core;

namespace Nekara
{
    // NuGet Dependencies:
    //  Newtonsoft.Json  (JSON.NET)
    //  grpc
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Nekara Server...");

            // Initialize a tester server
            OmniServer socket = new OmniServer(new OmniServerConfiguration());
            NekaraServer service = new NekaraServer(socket);

            socket.RegisterService(service);

            Console.ReadLine(); // TesterServer is an asynchronous object so we block the thread to prevent the program from exiting.
            Console.WriteLine("... Bye");
        }
    }
}
