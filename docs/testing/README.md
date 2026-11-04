# docs/testing/

Model-driven test plans and their recorded results. The model (a chat LLM running inside Open Browser) is given a plan file, executes every test through execution envelopes, and writes the results back to disk. This is how the runtime was exercised end to end: command execution, UTF-8 handling, the n8n, GitHub and Python toolkits, and RAW blocks.

- `test-plan-v3.md`, `test-plan-v4.md`: plans for the runtime protocol.
- `test-results-v3.md`: results of the v3 run.
- `tools-test-plan.md`, `tools-test-results.md`: plan and results for the bundled toolkits.

Paths inside the plans (for example `D:\projects\open-browser`) are the author's local layout at the time of the run. Treat the files as historical records, not as portable scripts.