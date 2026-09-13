# It's moosh all the way down…

I've wanted one piece of software for years: a bash that *fits* a Windows machine. Not WSL, not msys2, not Cygwin, not a whole Unix ecosystem bolted on sideways so Windows can cosplay as Linux. A small, native bash that does the job and gets out of the way.

It exists now. One static executable, 5.7 MB, nothing to install underneath it. It starts in 30 milliseconds, and on interpreter throughput it runs 8 to 11 times faster than the bash I'd been using.

Claude wrote it. All of it. I have never read a line of it.

So it's mush, obviously: tokens and patterns and probabilities shoved into a shape that resembles code, with no comprehension under it. That's the process, not an insult. *Slop* is the different thing: output that's wrong, bloated, unmaintainable, with nobody in control of it. Mush is the medium; slop is a failure of the person holding it. More on that at the end. Mostly I want to talk about the three things that surprised me: the speed, the cost of maintaining it, and how nearly free it became to try something.

## Speed

Same scripts, three shells, wall clock, best of three, on one machine. The outputs were diffed, so a fast wrong answer can't pass as a win.

| benchmark | C#Bash | Git Bash 4.4 | WSL bash 5.1 |
|---|---|---|---|
| loop, 200k iterations | 0.169 | 1.499 | 0.433 |
| arithmetic, 200k | 0.259 | 2.093 | 0.632 |
| function calls, 100k | 0.229 | 2.319 | 0.589 |
| loop, 2M iterations | 1.389 | 15.082 | 3.481 |
| 900 coreutil calls | 0.078 | 20.303 | 0.777 |
| text pipeline, 50k lines | 0.260 | 0.353 | 0.136 |
| find over 400 files | 0.055 | 0.226 | 0.218 |
| startup (`bash -c 'exit 0'`) | 0.030 | 0.027 | 0.096 |

Eight to eleven times Git Bash on interpreter throughput, and two and a half times *native Linux* bash. The 260× row is the honest headline and the honest cheat at once: it calls `cat`, `wc` and `grep` 900 times, which under Git Bash is 900 Windows process creations and here is none, because the coreutils live inside the process. That isn't a clever optimisation, it's what you get for not emulating `fork` on an operating system that hasn't got one.

One row loses, and it stays in the table for that reason: streaming text through a pipeline, where native C tools beat managed ones per line, and where WSL additionally reads its scratch file from ext4 inside the VM while the other two are on NTFS.

The row I care about most is the last. As a shell for an AI coding agent, startup is paid on every tool call, and 30 ms against Git Bash's 27 ms is the difference between a novelty and something you can work in.

## Maintenance

Everybody asks the same question, and it's the right one: who maintains it?

Since the first computers the answer was a human who had to hold every layer in their head, forever. Now the thing that wrote it maintains it, on contact, at 2am, already fluent in all of it. To whatever standard you hold it to, and *that* is the whole job. Here the standard is mechanical rather than moral: test cases whose expected outputs were written from real bash semantics, **not** captured from this interpreter's own output, so a regression fails instead of being quietly blessed; and behind them 237 probes run differentially against real bash, on a ratcheted baseline where the count of known divergences may go down and may not go up.

Then the interesting part. Other Claude sessions, on several machines, started using this thing as their real shell, and filing defect reports against it, steadily, for a week. Every one was reproduced from a script file against real bash before any code was touched, and the ones that didn't reproduce were answered with the evidence rather than with a fix. One had `echo` mangling parenthesised text; run properly it was byte-identical to Git Bash, and the culprit was the reporter's own quoting. It earned its keep anyway, because underneath it sat a real defect: the shell had been accepting that malformed command as three commands with a clean exit status, where bash raises a syntax error. Another report, of a hard crash, turned out to be Windows Defender deleting a file belonging to the tool that filed the report; the shell was never in it. An eager maintainer with no bar fixes the first and the third, and never finds the second.

The dividend shows up in shape rather than effort. A bug in `printf`'s byte escapes could have been patched where it surfaced; instead it became one encoding class that every read, write, pipe and argument now passes through, and a whole category of latent bugs went with it. The AI will cheerfully write the local patch instead; holding out for the structure is the human's contribution, and it costs about a sentence.

## Experimentation

This is the part I really wanted to exercise on a big(ish) project and I was very pleasantly surprised: the cost of trying something collapsed to nearly nothing, which changes what you bother to find out.

Shipping it, for instance. There are four plausible ways to publish a .NET program, and rather than reason about which was best, all four got built and measured in an afternoon. Native AOT won: 30 ms startup against 68–74 ms for the managed builds, one static file, no runtime on the target.

But measuring also produced the finding that no amount of reasoning would have. AOT compiles ahead of time and can never re-optimise, so past roughly 1.2 million shell operations in a single invocation the just-in-time build overtakes it by about 11%. I had first derived that crossover arithmetically and published a number wrong by a factor of three; measuring at four loop sizes fixed it. Nothing an agent does comes within two orders of magnitude of the threshold, so the trade is free. But it's *documented*, rather than discovered later by somebody else.

And while re-measuring, two bugs surfaced in the benchmark harness itself, each of which had already published a wrong number. Fixing them reversed an entire row, from "WSL is fastest here" to "WSL is slowest here". Cheap experiments are only worth something if you distrust your own arithmetic harder than you distrust the tool.

## The part that doesn't automate

None of that is an argument that the mush got smart. It's an argument about steering, and the evidence there is better than the vibes. Epoch AI ran the *same* model through different harnesses on a standard benchmark and watched it score anywhere from 2.7% to 28.3% (Epoch AI, 2026); the scaffolding moved the result tenfold. And the tests being the target is not a metaphor: the best published result for autonomous bug-fixing, 94.3%, only holds when a *human* writes the tests; hand the same system model-written tests and it falls to 68% (TDFlow, 2026). "The AI did it" is, underneath, "the human specified it".

Compilers mooshed too, and we stopped reading their assembly once they got good enough to trust. Capability matures on its own; *judgment* doesn't. Two years of larger models barely moved the security of the code they write, still around 45% shipping a known class of vulnerability (Veracode, 2025–2026).

One caveat, because the screenshot crowd is early rather than wrong: unsteered, A.I. genuinely misfires. A controlled trial found experienced developers 19% *slower* with early-2025 AI while believing they were 20% faster (METR, 2025). Twice on this project Claude ran ahead and did things I hadn't agreed to; I caught it and pulled it back. That isn't a footnote to the method, it *is* the method.

## So

One last layer, since we're being honest about mush: Claude researched and drafted this article. I framed it, cut it, argued with it, steered it. Moosh all the way down, including this sentence.

But money talks and bullshit walks, and the table above is the money. *Moosh* and *slop* can both be true at once. Only the second was ever in question.

---

The full bash project is available here: https://github.com/klinkenbecker/csharpBash

### Sources

- **Epoch AI**, *SWE-bench Verified* (2026): the same model under different scaffolding scored 2.7%–28.3%; measured capability tracks the harness, not just the model. https://epoch.ai/benchmarks/swe-bench-verified
- **TDFlow** (Han et al., EACL 2026): 94.3% on SWE-bench Verified with human-written tests, falling to 68% with model-generated tests. https://arxiv.org/abs/2510.23761
- **Veracode**, *GenAI Code Security Report* (2025, reaffirmed 2026): ~45% of generated code introduced an OWASP Top-10 weakness, and the rate stayed flat across two years of model releases. (Veracode is an application-security vendor; corroborated by peer-reviewed work, e.g. BaxBench, ICML 2025.) https://www.veracode.com/blog/genai-code-security-report/
- **METR**, *Measuring the Impact of Early-2025 AI on Experienced Open-Source Developers* (July 2025): a randomized trial in which devs were 19% slower yet believed they were 20% faster; scoped to early-2025 tooling. https://metr.org/blog/2025-07-10-early-2025-ai-experienced-os-dev-study/
