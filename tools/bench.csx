// In-process micro-benchmark harness for the Bash interpreter.
// Run with the (already-installed) dotnet-script global tool:
//     dotnet-script F:/Koliada/Tools/bash/tools/bench.csx
//
// References the Release Bash.dll directly so there's no process-startup/JIT noise,
// and reports time AND allocation per iteration (GC.GetTotalAllocatedBytes) plus
// gen0/1/2 collection counts — so we can see whether the cost is CPU or allocation.

#r "F:/Koliada/Tools/bash/Bash/bin/Release/net8.0/Bash.dll"

using System;
using System.Diagnostics;
using System.IO;
using Bash.Lexer;
using Bash.Parser;
using Bash.Evaluator;

string benchDir = "F:/Koliada/Tools/bash/tests/bench/";

const int Rounds = 6;       // best-of-N for time (min = least GC/scheduling noise)

void Bench(string name, Action body, long iters)
{
    body(); body();                                    // warm-up (JIT + caches)
    double best = double.MaxValue;
    long   bytes = 0; int g0 = 0, g1 = 0, g2 = 0;
    for (int r = 0; r < Rounds; r++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long a0 = GC.GetTotalAllocatedBytes(precise: true);
        int  c0 = GC.CollectionCount(0), c1 = GC.CollectionCount(1), c2 = GC.CollectionCount(2);
        var  sw = Stopwatch.StartNew();
        body();
        sw.Stop();
        if (sw.Elapsed.TotalSeconds < best)
        {
            best  = sw.Elapsed.TotalSeconds;
            bytes = GC.GetTotalAllocatedBytes(precise: true) - a0;   // deterministic per run
            g0 = GC.CollectionCount(0) - c0; g1 = GC.CollectionCount(1) - c1; g2 = GC.CollectionCount(2) - c2;
        }
    }
    double nsPer = best * 1e9 / iters;
    double bPer  = (double)bytes / iters;
    Console.WriteLine($"{name,-12} {iters,8} it  {best,7:F3}s  {nsPer,8:F0} ns/it  {bPer,8:F0} B/it  (gc {g0}/{g1}/{g2})");
}

// Run a whole script source once (its internal loop does the iterations).
// Fresh Evaluator each call so loop counters reset; stdout muted.
void RunScript(string src)
{
    var ev   = new Evaluator();
    var ast  = new Parser(new Lexer(src).Tokenize()).Parse();
    var prev = Console.Out;
    Console.SetOut(TextWriter.Null);
    try { ev.Execute(ast); } finally { Console.SetOut(prev); }
}

string loopSrc  = File.ReadAllText(benchDir + "loop.sh");
string arithSrc = File.ReadAllText(benchDir + "arith.sh");
string funcSrc  = File.ReadAllText(benchDir + "func.sh");

Console.WriteLine("== full scripts (cost per internal loop iteration) ==");
Bench("loop",  () => RunScript(loopSrc),  200000);
Bench("arith", () => RunScript(arithSrc), 200000);
Bench("func",  () => RunScript(funcSrc),  100000);

Console.WriteLine("== components (per call) ==");
string line = "i=$((i + 1))";
Bench("lex",        () => { for (int k = 0; k < 200000; k++) new Lexer(line).Tokenize(); }, 200000);
Bench("lex+parse",  () => { for (int k = 0; k < 200000; k++) new Parser(new Lexer(line).Tokenize()).Parse(); }, 200000);

// Arithmetic expansion in isolation — the suspected hot path ($((…)) re-parsed each time).
var env = new ShellEnvironment();
var we  = new WordExpander(env, new Evaluator(env));
env.Set("i", "41");
Bench("arith-eval", () => { for (int k = 0; k < 200000; k++) we.EvalArithmetic("i + 1"); }, 200000);

// Allocation attribution: cost of expanding the individual words a loop iteration touches.
// Loop iter = `[ $i -lt 200000 ]` (3x ExpandToFields) + `i=$((i+1))` (ExpandToString).
SimpleCommand FindCmd(Node n) => n switch
{
    Script s            => FindCmd(s.Nodes[0]),
    Bash.Parser.List l  => FindCmd(l.Items[0].Pipeline),
    Pipeline p          => FindCmd(p.Commands[0].Command),
    SimpleCommand c     => c,
    _                   => null
};
Word Arg(string src) => FindCmd(new Parser(new Lexer(src).Tokenize()).Parse()).Args[0];

var wVar = Arg("x $i"); var wLit = Arg("x 200000"); var wArith = Arg("x $((i + 1))");
Console.WriteLine("== per-word expansion (allocation attribution) ==");
Bench("EtoF $i",     () => { for (int k=0;k<200000;k++) we.ExpandToFields(wVar);   }, 200000);
Bench("EtoF literal",() => { for (int k=0;k<200000;k++) we.ExpandToFields(wLit);   }, 200000);
Bench("EtoStr arith",() => { for (int k=0;k<200000;k++) we.ExpandToString(wArith); }, 200000);
