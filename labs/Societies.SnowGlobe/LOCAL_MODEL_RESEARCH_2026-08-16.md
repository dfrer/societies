# Local Model Research for Societies Snow Globe Lab

Date: 2026-08-18 (status reconciliation; research baseline dated 2026-08-16)
Scope: local, loopback-only model assistance for a deterministic Snow Globe simulation

## Executive decision

The first separately authorized live compatibility candidate is **Ollama `qwen3.5:4b`**, now paired with an offline local-premium comparison contract. `LocalPremiumComparison.Evaluate(ReadOnlyMemory<byte>)` is a one-entry deep Module whose registry binds artifact SHA-256 `961B54B7D8CFB2AEAD566579499ADB3AA21F1D85BFBE0B7C6FC504A8ADC40E0D` and frozen plan/workload/prompt/schema/context/output/sample identities. Strict 16 KiB/depth-8 parsing reuses `ValidateBenchmarkEvidence`; absent/offline fixture premium Adapters cannot count as live. Status is `insufficient_live_premium_evidence`; premium, premium_cost, and performance_delta are null. The 2,015 B comparison report has SHA-256 `845c429f3d1f90da13111affb2adf5480e6bbb72aa8a95e04de07730080dadce`; contract hash `5ca8f57d8dd4fb5de18a1179c1a8acf25eef944ac7350f30514f097932d95227`. No winner, quality/intelligence, or local-cost-zero claim is made. No file/network/provider/credential/payment/model/journal Apply/world mutation occurred. Focused validation 7/7, full lab 374/374, build 0 warnings/errors, independent CODE GO with no P0-P2 findings. This does not alter the measured local-cell claim: it remains local compatibility/fit/latency only, not general intelligence, quality, or production readiness.

The current machine is an RTX 2070 SUPER with 8,192 MiB VRAM and the pinned E: Ollama v0.32.14 runtime; the default PATH Ollama 0.18.2 remains unchanged. The lab preflight remains: literal loopback, no credentials, redirects, retries, or proxy; one shared server; bounded bytes, depth, queue, and time; an 8,192 MiB maximum; metrics-only evidence.

The practical control candidates are `granite4.1:3b-q4_K_M` (about 2.1 GB; Apache 2.0; structured/tool-oriented positioning) and `phi4-mini:3.8b-q4_K_M` (about 2.5 GB; MIT; tool-call control), with official publisher details linked in the shortlist below. **Qwen3 4B official GGUF Q4_K_M** is the longer-term llama.cpp compatibility baseline. **LFM2.5 8B-A1B Q4** is a later quality/agentic ceiling only after freeing GPU memory and measuring a 10–15% safety margin: its listed artifact is about 5.2 GB and conflicts with the current 5.2 GB free snapshot. Its official card uses an LFM1.0/custom license; it must not be described as Apache or unrestricted ([Liquid AI announcement](https://www.liquid.ai/blog/lfm2-5-8b-a1b), [model card](https://huggingface.co/LiquidAI/LFM2.5-8B-A1B)). Models with 6.6 GB or larger artifacts are excluded on this current boundary.

This is a two-track recommendation: use the installed Ollama path only for a separately approved first compatibility benchmark; target a pinned llama.cpp CUDA build plus an official GGUF, exact binary/model hashes, offline operation, loopback binding, and no web/tools for long-term reproducibility ([llama.cpp server](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md)). Do not recommend Gemma 4 without substantiating a released, obtainable official local artifact as of this date. Gemma 3 4B remains an independent challenger under Gemma terms ([Ollama Gemma 3 listing](https://ollama.com/library/gemma3:4b)).

## Evidence and constraints

The machine snapshot and frozen preflight above are local evidence. Model sizes, licenses, context windows, and capability descriptions are publisher evidence. No local model output, throughput, VRAM peak, schema rate, or semantic quality has been measured. Advertised context or leaderboard position is not a fit criterion. Any number below that is a UX or acceptance threshold is a hypothesis until the lab measures it.

## Ranked shortlist

| Rank / use | Candidate | Publisher/runtime evidence | Decision |
| --- | --- | --- | --- |
| 1 / first compatibility cell | Ollama `qwen3.5:4b` | 3.4 GB, 4.66B, Q4_K_M, Apache 2.0, advertised 256K context; [Ollama](https://ollama.com/library/qwen3.5:4b), [Hugging Face](https://huggingface.co/Qwen/Qwen3.5-4B) | Separately authorize and measure; lock the exact `/api/tags` digest and metadata; not yet proven to fit or behave well. |
| 2 / structured control | `granite4.1:3b-q4_K_M` | About 2.1 GB; IBM positions Granite 4.1 for tool/structured use; [Ollama tags](https://ollama.com/library/granite4.1/tags), [IBM documentation](https://www.ibm.com/granite/docs/models/granite4-1) | Strong lower-memory control candidate. |
| 3 / control challenger | `phi4-mini:3.8b-q4_K_M` | About 2.5 GB, MIT; [Ollama](https://ollama.com/library/phi4-mini), [Microsoft card](https://huggingface.co/microsoft/Phi-4-mini-instruct) | Measure against the first cell; no quality assumption. |
| 4 / reproducibility baseline | Qwen3 4B official GGUF Q4_K_M | Official GGUF and llama.cpp path; [Ollama Qwen3](https://ollama.com/library/qwen3:4b-instruct), [Qwen GGUF](https://huggingface.co/Qwen/Qwen3-4B-GGUF) | Long-term pinned-runtime baseline. |
| Later / ceiling candidate | LFM2.5 8B-A1B Q4 | About 5.2 GB; custom LFM1.0 terms; [Ollama tags](https://ollama.com/library/lfm2.5/tags) | Defer until memory is freed and 10–15% headroom is measured. |
| Conditional second-stage ceiling | Qwen3 8B | 5.2 GB, 8.19B, Q4_K_M, Apache 2.0; [Ollama](https://ollama.com/library/qwen3:8b) | Only after freeing GPU memory and measuring transient headroom; not a first-cell candidate. |
| Stretch/offload characterization | Qwen3.5 9B | 6.6 GB Q4_K_M; [Ollama](https://ollama.com/library/qwen3.5:9b) | Weight artifact alone exceeds 80% of total VRAM and exceeds current ~5.2 GB free; characterize only with explicit offload approval, never default. |

## Runtime decision and safety boundary

Ollama is the installed compatibility path. The first path is client-serialized and locally configured: request one shared loaded model, one parallel slot, bounded request/response bytes, and a bounded queue/time budget. No-cache, continuous-batching, and speculation are desired startup assertions for the first cell, but the current API runner cannot prove every server-wide setting; report only what the harness measures. Ollama supports structured outputs through JSON schema and documents local API generation and GPU behavior ([structured outputs](https://docs.ollama.com/capabilities/structured-outputs), [API](https://docs.ollama.com/api/generate), [GPU FAQ](https://docs.ollama.com/gpu), [FAQ](https://docs.ollama.com/faq)). A schema-conforming response is still an untrusted proposal: the deterministic validator decides whether it can enter the simulation.

The reproducibility target is a pinned llama.cpp CUDA build with an official GGUF, exact hashes for executable and model, offline operation, literal loopback binding, and no web/tools. llama.cpp supplies server, grammar, and benchmark tooling ([server](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md), [grammars](https://github.com/ggml-org/llama.cpp/blob/master/grammars/README.md), [llama-bench](https://github.com/ggml-org/llama.cpp/tree/master/tools/llama-bench), [speed bench](https://github.com/ggml-org/llama.cpp/tree/master/tools/server/bench/speed-bench)). This is a future reproducibility target, not an instruction to install it now.

## Qwen3.8 and the intelligence ceiling

Qwen3.8 is an official release as of August 2026. The [official collection](https://huggingface.co/collections/Qwen/qwen38) contains the open-weight `Qwen3.8-27B` and FP8 variant plus the much larger 2.4T-A95B family. The [27B model card](https://huggingface.co/Qwen/Qwen3.8-27B) describes an Apache-2.0 dense 27.78B model with 64 layers, native 262K context, vision support, thinking enabled by default, `low`/`medium`/`xhigh` reasoning effort, and a non-thinking mode. Qwen reports large gains over Qwen3.6 on agentic and professional benchmarks; those are publisher results and do not establish Societies behavior.

It is not a credible full-GPU model for the RTX 2070 SUPER. The official BF16 repository occupies about 55.6 GB and the official [FP8 repository](https://huggingface.co/Qwen/Qwen3.8-27B-FP8) about 30.9 GB, before runtime and KV memory. No Qwen-published GGUF or sub-FP8 checkpoint was verified. Aggressive community conversions may run with heavy CPU/RAM offload, but their fit, speed, and quality loss are separate experiments; they cannot be treated as an 8 GB operating configuration.

| Role | Model direction | Decision |
| --- | --- | --- |
| Always-on local tactical proposals | Qwen3.5 4B first; Granite 4.1 3B and Phi-4 mini controls | Practical 8 GB lane; benchmark before promotion. |
| Later local intelligence ceiling | Qwen3 8B (5.2 GB Q4_K_M) or LFM2.5 8B-A1B after freeing VRAM | Measure short-context fit and 10–15% headroom; no quality assumption. |
| Rare deep reflection | Qwen3.8-27B with CPU/GPU offload only if system RAM and background latency are measured | Opt-in, non-authoritative, one shared server at a time; not a per-tick path. |
| Frontier comparison | Separately authorized hosted Qwen3.8 shadow evaluation | Adds privacy, cost, credential, and network gates; never writes state and is not authorized here. |

Qwen3 8B remains a conditional local ceiling: the official Ollama artifact is 5.2 GB, 8.19B parameters, Q4_K_M, and Apache 2.0 ([Ollama](https://ollama.com/library/qwen3:8b)). Qwen3.5 9B is a stretch/offload characterization only: its Q4_K_M artifact is 6.6 GB ([Ollama](https://ollama.com/library/qwen3.5:9b)), already more than 80% of total VRAM and larger than the current free snapshot. Any intelligence gain must be demonstrated on the 200+ frozen Snow Globe corpus through proposal validity, social and player consequence, latency, fallback, and replay—not inferred from size or vendor scores.

## Target architecture and practices

* The immutable event ledger is truth. Each agent receives a deterministic information projection from ledger state; hidden facts never enter a prompt. Calls are sparse and salience-triggered, with frozen requests and one fair bounded queue feeding one loaded shared model.
* Decode produces a typed proposal. A schema decoder is followed by a strict deterministic validator. Completion order never determines outcome: proposals commit in a stable order (for example, event sequence, agent ID, request ID). Record prompts, model/runtime hashes, proposals, validation results, repairs, fallbacks, and commit order; replay recorded proposals and never re-infer.
* Deterministic reflexes and work execute every tick. Tactical, conversation, planning, reflection, and governance use distinct simulated cadences. Language assists interpretation and proposals; it is not simulation authority.
* Memory has explicit truth classes: authoritative event references; deterministic semantic projections; and untrusted reflections that cite visible event IDs and carry model/prompt provenance. Start with deterministic lexical/entity retrieval. Add embeddings or a vector service only after an A/B result justifies their cost and complexity.
* Speech, messages, contracts, votes, and laws are typed events/state machines. Conversation never directly enacts state. Information asymmetry is mandatory. Evaluate player consequence and replay safety, not only conversational believability.
* Treat player, NPC, and mod text as prompt-injection material. The model has no tools, files, or network. Inputs are typed and bounded; do not store chain-of-thought. Allow one repair attempt, then deterministic fallback.

These practices align with agent-simulation, memory, social-interaction, structured-output, and agent-hijacking research, but papers and benchmarks are design evidence—not proof that this lab’s citizens will be coherent or safe ([Generative Agents](https://arxiv.org/abs/2304.03442), [Concordia](https://arxiv.org/abs/2312.03664), [structured generation](https://arxiv.org/abs/2501.10868), [agent hijacking evaluation](https://www.nist.gov/news-events/news/2025/01/technical-blog-strengthening-ai-agent-hijacking-evaluations)).

## Evaluation matrix

| Phase | Purpose and frozen evidence | Gate |
| --- | --- | --- |
| 0 | Artifact, hardware, runtime, and hash inventory; explicit approval gate | No live traffic until separately authorized. |
| 1 | Synthetic server conformance: loopback, bounds, schema, timeout, queue, fallback, metrics | Pass isolation and deterministic error handling. |
| 2 | After download authorization: fit and latency; cold versus warm process | Maintain 10–15% VRAM headroom; record p50/p95/p99. |
| 3 | At least 200 frozen Snow Globe action/social/memory/adversarial scenarios | Measure parse/schema/validator/repair/fallback and consequence. |
| 4 | Shadow mode against deterministic citizens | Zero committed model effects; compare proposals and cost. |
| 5 | Opt-in committed lab: one citizen, then eight | Replay and commit-order equivalence before expansion. |

Use a frozen corpus at 4, 8, and 16 agents; every-tick and every-other-tick cadence; sequential and controlled-parallel profiles; at least three trials. Separate process-cold from warm. Include 100 fixed-repeat and 100 varied cases. Report p50/p95/p99, queue wait, TTFT/prefill/decode, tokens/sec, peak VRAM, parse/schema/validator/repair/fallback rates, commit order, and replay hash. Distinguish engine-only repeatability from semantic repeatability and byte repeatability.

The first live cell, if approved, is 4K context, 96 output tokens, one client-serialized slot, with temperature 0 and fixed settings. No prompt cache, continuous batching, and speculation are desired assertions to record if the runner can verify them, not established server-wide facts. Then sweep one variable at a time. UX thresholds are hypotheses until measured. The snapshot's VRAM figure is inventory only, not model-run evidence: transient VRAM, headroom, and queue behavior require harness measurement.

## Staged roadmap and decision gates

1. Freeze the preflight and inventory hashes. Obtain explicit download approval for exactly one candidate.
2. Run synthetic conformance without a model. Fail closed on endpoint, bounds, queue, timeout, and schema violations.
3. Measure the first compatibility cell, then compare Granite and Phi under the same corpus and settings. Consider Qwen3 8B only as a second-stage ceiling after the same corpus shows a player-relevant gain and measured headroom; characterize Qwen3.5 9B only as explicitly approved offload work. Stop if memory headroom, isolation, or replay evidence fails.
4. Run the 200+ scenario corpus, then shadow mode. Inspect failure classes and player-relevant consequences.
5. Commit one citizen only after replay equality and deterministic fallback pass; expand to eight only after the same evidence holds.
6. Pin the long-term llama.cpp/GGUF runtime only after the compatibility behavior is understood; do not confuse this reproducibility work with current live authorization.

## Risks and claims not made

This report does not claim that any model fits, is fast enough, follows schemas reliably, produces good social behavior, resists all prompt injection, improves player experience, or preserves semantic/byte replay. It does not claim that Ollama and llama.cpp produce equivalent outputs. It does not claim that a license permits every intended use; review each model’s terms. It does not claim that 4K context is sufficient for a simulation, that a leaderboard predicts Snow Globe performance, or that a larger model is better under an 8 GB GPU boundary. It does not authorize downloads, servers, external calls, deployment, or model-generated state changes.

Main risks are VRAM contention and context growth; queue starvation and unfairness; schema-valid but semantically invalid proposals; prompt injection through world text; memory contamination; nondeterministic completion order; repair loops; and measuring prose quality while missing player consequence. Mitigations are bounded projections, one fair queue, strict validation, typed state machines, provenance, stable commit order, one repair then fallback, and replay-based acceptance.

## Primary references

Reference labels: [LongMemEval](https://arxiv.org/abs/2410.10813), [LongMemEval-V2](https://arxiv.org/abs/2605.12493), [A-MEM](https://arxiv.org/abs/2502.12110), and [Mem0](https://arxiv.org/abs/2504.19413). The [Gorilla function-calling leaderboard](https://gorilla.cs.berkeley.edu/leaderboard) is directional evidence only; vendor results can vary by model/version, prompt, hardware, and harness and do not substitute for Snow Globe measurements.

Model/runtime references are linked in the decision and shortlist sections. Additional evaluation references: [Gorilla function-calling leaderboard](https://gorilla.cs.berkeley.edu/leaderboard), [Concordia game-master architecture](https://arxiv.org/abs/2312.03664), [OASIS large-scale social simulation](https://arxiv.org/abs/2411.11581), [Project Sid and its PIANO many-agent architecture](https://arxiv.org/abs/2411.00114), [security and agent hijacking](https://www.nist.gov/news-events/news/2025/01/technical-blog-strengthening-ai-agent-hijacking-evaluations), and the supplied research set ([AgentSociety](https://arxiv.org/abs/2502.08691), [SOTOPIA](https://arxiv.org/abs/2310.11667), [information-asymmetry critique](https://aclanthology.org/2024.emnlp-main.1208/), [LongMemEval](https://arxiv.org/abs/2410.10813), [LongMemEval-V2](https://arxiv.org/abs/2605.12493), [A-MEM](https://arxiv.org/abs/2502.12110), [Mem0](https://arxiv.org/abs/2504.19413), and [indirect prompt injection](https://arxiv.org/abs/2302.12173)).

## Adopted and rejected practices

Adopted: deterministic ledger authority; typed, bounded projections; sparse salience-triggered calls; one fair client-serialized queue; strict schema and semantic validation; stable commit order; provenance; one repair then fallback; lexical/entity retrieval before embeddings; and replay from recorded proposals rather than re-inference.

Rejected for this scope: treating model output, memory reflections, context windows, vendor leaderboards, snapshot VRAM, or advertised batching/cache/speculation behavior as acceptance evidence; allowing tools, files, network, hidden facts, chain-of-thought, or direct state mutation; and claiming cross-runtime output equivalence.
