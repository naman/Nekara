# TestingService

## Architecture

The system is split into the server-side and client-side.

**Server Side:** The server side exposes an API (HTTP for network clients and IPC for local clients; this is configurable) to its internal *Scheduler*. The *Scheduler* essentially manages the interleavings of different asynchronous tasks. The program under test will send signals to the server -- e.g., "I just started a new asynchronous task" -- but it will not make progress on its own (even if can) unless it receives a signal from the server. 

**Client Side:** The client side library/program is a thin proxy to the actual testing service. The program under test makes local calls to this proxy service, which relays the calls to the server-side and mediates the responses.

## API

* `CreateTask`: 
* `StartTask`:
* `EndTask`:
* `ContextSwitch`: 
* `CreateResource`:
* `DeleteResource`:
* `BlockedOnResource`:
* `SignalUpdateResource`:

## Current State

There are currently 2 C# Solutions in this project due to merging of 2 different repositories.

* `TestingService/TestingService.sln` is the original solution file containing `TestingService` and `ProgramUnderTest`. This solution file is left there for reference.
* `AsyncTester.sln` was copied into this repository and it points to `AsyncTester` and `ClientProgram`. This project contains the split-up version of the system.

We will clean up the repository soon, once the bare architecture is in place. For now, leaving all the code where they are.