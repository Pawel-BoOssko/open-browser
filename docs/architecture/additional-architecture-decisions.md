# OpenBridge / Open Browser: Additional Architecture Decisions

## 4. LLM Identity

4a. Each LLM instance receives a minimal identity.

4b. The system prompt contains:

- name, for example Stefan;
- own ID;
- short information that it operates in OpenBridge / Open Browser;
- basic startup commands, for example `HELP`, `STATUS`, `CAPABILITIES`.

4c. System details are discovered through self-discovery commands.

4d. The LLM should read available capabilities itself through status/help/capabilities.

## 5. Tab Process

5a. The tab process handles one conversation / one model tab.

5b. Its responsibilities include:

- stream and delta handling;
- response assembly;
- local conversation state;
- page message handling;
- communication with the Host;
- returning Host responses back to the conversation.

5c. The tab runtime is separated from the application UI.

5d. UI shell and tab runtime must be separate responsibilities.

## 6. Host

6a. The Host is the central, stable process of the system.

6b. The Host provides infrastructure, routing and connection to executors.

6c. The Host maintains the tab registry.

6d. The Host will be the integration point for cross-tab communication.

6e. The Host provides controlled system responses to the layer above: result, error or timeout.

## 7. Errors and Timeouts

7a. Each layer is responsible for controlled error inversion to the layer above.

7b. The tab process returns an error to the LLM when it cannot communicate with the Host.

7c. The Host returns an error when it cannot communicate with an executor.

7d. The executor returns an error to the Host when it cannot execute an operation.

7e. Each operation must end with one of these outcomes:

- success;
- error;
- timeout;
- fallback response.

## 8. Executors

8a. Executors will follow the execution model previously verified in the helper.

8b. First, the exact helper execution model must be established.

8c. Then this execution pattern will be recreated in the new Host architecture.

8d. Executor categories follow from function, for example diagnostics, filesystem, shell, Python, cross-tab and API integrations.

8e. The separation between filesystem executor and shell executor still requires exact functional clarification.

## 9. DOM, Stream and Source of Truth

9a. The source of truth for model responses is stream / deltas.

9b. Responses are assembled from the stream/deltas.

9c. The DOM is a visual surface for the user.

9d. Operationally, the system works on current stream data and possibly a short tail of recent messages.

9e. The assumed operational tail is on the order of:

```text
10-40 most recent messages
```

9f. Older conversation history is not a runtime resource.

## 10. Cross-tab Communication

10a. Communication between tabs is synchronous from the point of view of the asking tab.

10b. Tab A sends a question to tab B through the Host.

10c. The Host forwards the question to tab B in a controlled envelope.

10d. Tab B answers tab A through the Host.

10e. If tab B is in the middle of its own work, it may answer that it is not a good time to handle the question.

10f. If the question is simple, tab B may answer and return to its own task.

10g. After delivering the answer, the Host informs tab B that the answer has been delivered and that it may continue its work.

## 11. Watchdog

11a. The watchdog is a mechanism for supervising an active workflow.

11b. Its meaning requires further analysis.

11c. Working definition: detecting lack of progress in an active flow, such as no new deltas, no result, no error, no timeout or no stage change.

11d. This point is not yet approved as final architecture.

## 12. UI and Tab Runtime Boundary

12a. The UI shell is responsible for the window, layout, controls, buttons and statuses.

12b. The tab runtime is responsible for WebView lifecycle, PageTap, JS modules, message handling, stream and local conversation state.

12c. The future tab process should emerge from the tab runtime logic.

12d. Current refactors are intentionally moving toward gradually slimming down `MainForm`.
