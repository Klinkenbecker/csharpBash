// Per-node attribution of a loop iteration: which piece of `while [ $i -lt N ]; do i=$((i+1)); done`
// costs what. Companion to bench.csx (whole scripts) — run this when bench.csx moves and you
// need to know WHERE. Same rules: Release dll, `dotnet script --no-cache tools/attrib.csx`.
// To compare against another build, copy the file and point the #r at that build's Bash.dll
// (a scratch `hg clone -u REV` built in Release is how the 2026-09-04 bisect was done).
#r "F:/Koliada/Tools/bash/Bash/bin/Release/net8.0/Bash.dll"
using System;
using System.Diagnostics;
using System.IO;
using Bash.Lexer;
using Bash.Parser;
using Bash.Evaluator;

const int Rounds = 5;
void Bench(string name, Action body, long iters)
{
    body(); body();
    double best = double.MaxValue; long bytes = 0;
    for (int r = 0; r < Rounds; r++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long a0 = GC.GetTotalAllocatedBytes(precise: true);
        var sw = Stopwatch.StartNew();
        body();
        sw.Stop();
        if (sw.Elapsed.TotalSeconds < best) { best = sw.Elapsed.TotalSeconds; bytes = GC.GetTotalAllocatedBytes(precise: true) - a0; }
    }
    Console.Error.WriteLine($"{name,-22} {best * 1e9 / iters,8:F0} ns/it {(double)bytes / iters,8:F0} B/it");
}

SimpleCommand FindCmd(Node n) => n switch
{
    Script s            => FindCmd(s.Nodes[0]),
    Bash.Parser.List l  => FindCmd(l.Items[0].Pipeline),
    Pipeline p          => FindCmd(p.Commands[0].Command),
    SimpleCommand c     => c,
    _                   => null
};
Node Cmd(string src) => FindCmd(new Parser(new Lexer(src).Tokenize()).Parse());

var prev = Console.Out;
Console.SetOut(TextWriter.Null);
var ev = new Evaluator();
ev.Execute(Cmd("i=5"));
const int N = 200000;
var tVar   = Cmd("[ $i -lt 200000 ]");
var tLit   = Cmd("[ 5 -lt 200000 ]");
var colon  = Cmd(":");
var tru    = Cmd("true");
var asg    = Cmd("i=$((i + 1))");
var asgLit = Cmd("j=5");
var asgVar = Cmd("j=$i");
Bench("[ $i -lt N ]",      () => { for (int k = 0; k < N; k++) ev.Execute(tVar); }, N);
Bench("[ 5 -lt N ]",       () => { for (int k = 0; k < N; k++) ev.Execute(tLit); }, N);
Bench(":",                 () => { for (int k = 0; k < N; k++) ev.Execute(colon); }, N);
Bench("true",              () => { for (int k = 0; k < N; k++) ev.Execute(tru); }, N);
Bench("i=$((i + 1))",      () => { for (int k = 0; k < N; k++) ev.Execute(asg); }, N);
Bench("j=5",               () => { for (int k = 0; k < N; k++) ev.Execute(asgLit); }, N);
Bench("j=$i",              () => { for (int k = 0; k < N; k++) ev.Execute(asgVar); }, N);
// Whole-loop rows here run AFTER ~1.4M executions in this process and read ~2x slower than
// bench.csx's fresh-process figure (observed 2026-09-04, cause not chased); compare rows
// within one run of this file, and use bench.csx for the headline number.
string loop = "i=0\nwhile [ $i -lt 200000 ]; do\n\ti=$((i + 1))\ndone\n";
Bench("whole loop", () => { var e2 = new Evaluator(); e2.Execute(new Parser(new Lexer(loop).Tokenize()).Parse()); }, N);
Console.SetOut(prev);
