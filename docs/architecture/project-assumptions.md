# Open Browser / OpenBridge Project Assumptions

## 1. Core idea

Open Browser is a local working environment for an external LLM operating through a real browser session.

The basic idea is simple: modern LLMs can reason, plan, write code and coordinate complex work, but the ordinary chat interface gives them only a narrow text window. They do not naturally have durable local state, reliable diagnostics, controlled access to files, awareness of other model sessions or a stable execution path for structured actions.

OpenBridge is the architecture that adds this missing local operating layer around the browser session.

The LLM remains the reasoning engine. The browser remains the place where the model conversation happens. Open Browser provides the controlled local environment around that conversation: stream capture, message handling, local state, diagnostics, future tab coordination and future tool execution through explicit boundaries.

The project is therefore about turning model work from isolated chat into supervised technical operation inside a real, inspectable workspace.

## 2. Problem being solved

A normal model conversation is powerful but operationally weak.

The model can produce plans and code, but the surrounding environment is mostly passive. It cannot reliably know what happened outside the text shown in the chat, whether a local action succeeded, whether another model tab has answered, whether a process is stuck, whether a command timed out or whether the local state changed between turns.

This creates a practical gap between reasoning and execution.

Open Browser is intended to close that gap by giving the model an explicit local runtime boundary. The model should receive structured signals from the browser and eventually from local tools. It should get results, errors and timeouts instead of silence. The local system should make work observable, repeatable and recoverable.

The value is not in automating clicks. The value is in creating a stable operational layer that lets an LLM work with browser-based model sessions and local resources in a controlled way.

## 3. Why a browser-based system

The project starts from the fact that many useful model capabilities are accessed through web interfaces, not only through APIs. Web sessions contain provider-specific behavior, model UI features, account context, conversation state and interaction patterns that are not always equivalent to API calls.

Open Browser treats the browser session as a real operating surface rather than a temporary inconvenience.

The browser is where the model is visible, where the conversation happens and where multiple model sessions can later coexist. The local application can observe this environment, capture structured signals and route information without pretending that the browser is just a disposable frontend.

This also keeps the design close to how people actually use models today: through real tabs, real conversations and real iterative work.

## 4. Operating model

The intended operating model is a supervised loop between three things:

1. the external LLM, which reasons and decides;
2. the browser tab, where the conversation and visible interaction happen;
3. the local OpenBridge runtime, which observes, routes, records and later executes bounded operations.

The LLM should not depend on hidden magic. It should see explicit results from the local system. When something works, it gets a result. When something fails, it gets an error. When something hangs, it gets a timeout or fallback response.

The local runtime should stay inspectable. It should not grow by accumulating accidental scripts, copied helper logic or unclear side channels. Each new capability should have a clear owner, clear boundary and clear failure mode.

## 5. Target architecture

The target architecture has five layers:

| Layer | Role |
| --- | --- |
| 1. LLM | External model that generates reasoning, text, decisions and future commands. |
| 2. Browser tab | Visible browser tab or model conversation surface. |
| 3. Tab process | Per-tab process responsible for stream handling, message capture, local conversation state, frame parsing and Host communication. |
| 4. Host with executors | Stable central process responsible for routing, tab registry, executors, diagnostics, timeouts and safe responses. |
| 5. Executor systems | Diagnostics, filesystem, shell, Python, cross-tab, API integrations and other future execution backends. |

Expected command flow:

```text
LLM
-> browser tab
-> tab process
-> Host
-> executor
-> Host
-> tab process
-> browser tab
-> LLM
```

The five-layer model exists to keep responsibilities separated. The model reasons. The tab exposes the conversation. The tab process handles one conversation. The Host coordinates global runtime concerns. Executors perform bounded operations behind the Host boundary.

## 6. Design logic

The architecture is built around one main principle: the model should get stronger local capabilities without making the system opaque or uncontrolled.

That leads to several design choices:

1. Stream and message capture should be preferred over full DOM dependence, because full conversation DOM is heavy and fragile.
2. Tab-local state should live near the tab, because each conversation has its own stream, timing and local loop.
3. Global routing should live in the Host, because cross-tab communication, executor routing and shared diagnostics need one stable coordination point.
4. Executors should sit behind explicit boundaries, because file access, shell execution, API calls and diagnostics have different risk profiles.
5. The Host should be stable and minimal, because complex reasoning belongs to the LLM while the Host provides reliable plumbing.
6. Every operation should return a result, error or timeout, because silence breaks model-driven operation.
7. Legacy or accidental mechanisms should not define the architecture. Capabilities should be added because the design needs them, not because old code already exists.

This is why the next engineering priority is to separate the current UI shell from tab-runtime responsibilities before adding Host, registry, executors, frame protocol or cross-tab behavior.

## 7. Tab process responsibility

The future tab process owns logic local to one browser tab or one model conversation.

Expected responsibilities:

1. Attach to one browser tab.
2. Capture model response streams and deltas.
3. Assemble response text.
4. Maintain local conversation loop state.
5. Parse future frames and commands from the model response.
6. Forward validated commands to the Host.
7. Return Host responses back into the conversation.
8. Track whether the tab is idle, thinking or sleeping.

The tab process should not own global routing, executor implementations, cross-tab registry or global recovery policy.

## 8. Host responsibility

The Host is a central, minimal and stable process. It is not the brain of the system.

Expected responsibilities:

1. Start and supervise tab processes.
2. Maintain tab registry.
3. Route commands to executors.
4. Own executors or supervise executor subprocesses.
5. Provide diagnostics and status.
6. Enforce timeouts.
7. Return errors and fallback responses.
8. Provide a future self-discovery surface.
9. Provide the execution point for cross-tab communication.

The Host should provide tools and reliable plumbing. Complex planning should remain with the LLM.

## 9. Executors

Executors live behind the Host boundary.

Potential executor categories:

| Executor | Purpose |
| --- | --- |
| Diagnostics executor | Status, logs, health checks, versions. |
| Filesystem executor | Controlled file operations inside approved workspace boundaries. |
| Shell executor | Carefully scoped local commands, if explicitly designed. |
| Python executor | Local Python execution for bounded tasks, if explicitly designed. |
| Cross-tab executor | Synchronous communication between model tabs. |
| API integration executors | GitHub, n8n, Supabase and other future integrations. |

Executor design is not yet finalized. Open questions include whether executors should be Host modules, subprocesses or a mixed model.
