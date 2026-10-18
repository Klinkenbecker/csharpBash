
## 2026-10-16 11:13 claudecall.sh

```
date     : 2026-10-16 11:12:08
C#Bash   : F:/Koliada/Tools/bash/tests/bench/aot/Bash.exe   [--build: rev 273, NOT deployed]
           C#Bash build 1.0.0+hg.249f6c4626a1 273
deployed : F:/Koliada/Tools/bash/dist/Bash.exe
           C#Bash build 1.0.0+hg.a160167e4eb9 266
method   : Claude Code's -c wrapper with the snapshot fixture, launched by tools/benchtimer from the repo root;
           each row the median of 5 samples of 20 launches (mean per launch), builds in alternating rounds

call           C#Bash  deployed   change
------------------------------------------
bare            15.1ms     14.8ms    +1.8%
true            31.5ms     31.2ms    +1.1%
ls              31.4ms     31.2ms    +0.6%
cat-head        31.1ms     31.1ms    -0.3%
grep-rn         38.7ms     38.6ms    +0.2%
git-status      49.6ms     48.8ms    +1.5%
cd-pwd          30.7ms     30.8ms    -0.5%

anatomy (ms, mean of 20 launches with BASH_TIMING_LOG set):
segment                                  true         ls   cat-head    grep-rn git-status     cd-pwd
---------------------------------------------------------------------------------------------
runtime start (creation to Main)         7.16       7.13       7.23       7.24       7.37       7.62
console set-up                           0.63       0.67       0.64       0.64       0.69       0.69
evaluator (environment, PATH)            1.58       1.63       1.57       1.66       1.58       1.68
options, startup files                   0.18       0.18       0.17       0.19       0.18       0.19
parse the call, reach source             0.16       0.16       0.15       0.17       0.16       0.17
snapshot (source)                       20.35      15.24      14.57      14.96      15.77      15.48
wrapper builtins                         0.02       0.01       0.02       0.01       0.02       0.01
the command (eval)                       0.00       0.15       0.32       7.40      16.18       0.04
  of which external programs             0.00       0.00       0.00       0.00      15.79       0.00
cwd write-back (pwd -P >|)               0.66       0.64       0.67       0.64       0.63       0.60
exit handler                             0.00       0.00       0.00       0.00       0.00       0.00
= the exit mark                         30.74      25.81      25.33      32.91      42.59      26.49
process exit (after the mark)            7.28       6.94       7.21       7.38       7.11       6.98
whole call (switch on)                  38.01      32.74      32.54      40.28      49.70      33.47
criterion: exit <= 10%            UNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTED
```

## 2026-10-16 12:14 claudecall.sh

```
date     : 2026-10-16 12:13:47
C#Bash   : F:/Koliada/Tools/bash/tests/bench/aot/Bash.exe   [--build: rev 278, NOT deployed]
           C#Bash build 1.0.0+hg.4e57edb1df12 278
deployed : F:/Koliada/Tools/bash/dist/Bash.exe
           C#Bash build 1.0.0+hg.a160167e4eb9 266
method   : Claude Code's -c wrapper with the snapshot fixture, launched by tools/benchtimer from the repo root;
           each row the median of 5 samples of 20 launches (mean per launch), builds in alternating rounds

call           C#Bash  deployed   change
------------------------------------------
bare            15.0ms     14.6ms    +2.8%
true            30.2ms     31.0ms    -2.7%
ls              31.5ms     31.0ms    +1.6%
cat-head        30.7ms     32.3ms    -4.8%
grep-rn         39.7ms     39.1ms    +1.6%
git-status      48.0ms     49.4ms    -2.8%
cd-pwd          30.4ms     31.0ms    -2.1%

anatomy (ms, mean of 20 launches with BASH_TIMING_LOG set):
segment                                  true         ls   cat-head    grep-rn git-status     cd-pwd
---------------------------------------------------------------------------------------------
runtime start (creation to Main)         6.74       6.90       6.92       6.95       7.06       6.77
console set-up                           0.66       0.70       0.66       0.68       0.69       0.66
evaluator (environment, PATH)            1.58       1.64       1.65       1.62       1.67       1.64
options, startup files                   0.19       0.18       0.18       0.18       0.19       0.18
parse the call, reach source             0.17       0.15       0.15       0.16       0.16       0.16
snapshot (source)                       14.80      14.55      14.69      14.84      16.88      15.59
wrapper builtins                         0.01       0.01       0.02       0.02       0.02       0.01
the command (eval)                       0.00       0.14       0.32       7.42      16.36       0.04
  of which external programs             0.00       0.00       0.00       0.00      16.00       0.00
cwd write-back (pwd -P >|)               0.65       0.64       0.60       0.68       0.65       0.64
exit handler                             0.00       0.00       0.00       0.00       0.00       0.00
= the exit mark                         24.80      24.90      25.18      32.55      43.68      25.71
process exit (after the mark)            7.09       7.01       7.02       7.68       7.08       7.12
whole call (switch on)                  31.90      31.91      32.19      40.23      50.76      32.83
criterion: exit <= 10%            UNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTED
```

## 2026-10-16 17:18 claudecall.sh

```
date     : 2026-10-16 17:17:59
C#Bash   : F:/Koliada/Tools/bash/tests/bench/aot/Bash.exe   [--build: rev 282, NOT deployed]
           C#Bash build 1.0.0+hg.b347f700b323 282
deployed : F:/Koliada/Tools/bash/dist/Bash.exe
           C#Bash build 1.0.0+hg.a160167e4eb9 266
method   : Claude Code's -c wrapper with the snapshot fixture, launched by tools/benchtimer from the repo root;
           each row the median of 5 samples of 20 launches (mean per launch), builds in alternating rounds

call           C#Bash  deployed   change
------------------------------------------
bare            14.5ms     15.0ms    -3.1%
true            16.4ms     31.1ms   -47.3%
ls              16.1ms     31.5ms   -49.1%
cat-head        16.4ms     31.5ms   -47.8%
grep-rn         24.2ms     39.1ms   -38.2%
git-status      33.4ms     49.6ms   -32.7%
cd-pwd          16.0ms     31.8ms   -49.8%

anatomy (ms, mean of 20 launches with BASH_TIMING_LOG set):
segment                                  true         ls   cat-head    grep-rn git-status     cd-pwd
---------------------------------------------------------------------------------------------
runtime start (creation to Main)         6.95       6.91       6.95       6.83       7.00       7.09
console set-up                           0.68       0.64       0.68       0.66       0.70       0.69
evaluator (environment, PATH)            1.67       1.63       1.68       1.61       1.80       1.68
options, startup files                   0.19       0.18       0.19       0.17       0.19       0.18
parse the call, reach source             0.16       0.15       0.16       0.15       0.16       0.16
snapshot (source)                        0.57       0.55       0.60       0.55       0.58       0.57
wrapper builtins                         0.01       0.01       0.01       0.01       0.01       0.01
the command (eval)                       0.00       0.13       0.33       7.66      16.92       0.04
  of which external programs             0.00       0.00       0.00       0.00      16.66       0.00
cwd write-back (pwd -P >|)               0.62       0.60       0.62       0.57       0.66       0.62
exit handler                             0.00       0.00       0.00       0.00       0.00       0.00
= the exit mark                         10.85      10.81      11.22      18.22      28.03      11.05
process exit (after the mark)            7.09       7.08       7.61       7.66       7.58       7.28
whole call (switch on)                  17.94      17.89      18.82      25.88      35.61      18.33
criterion: exit <= 10%            UNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTED
```

## 2026-10-18 09:14 claudecall.sh

```
date     : 2026-10-18 09:14:08
C#Bash   : F:/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.8ae72c3b647d 320
method   : Claude Code's -c wrapper with the snapshot fixture, launched by tools/benchtimer from the repo root;
           each row the median of 5 samples of 20 launches (mean per launch), builds in alternating rounds

call           C#Bash
------------------------------------------
bare            10.2ms
true            11.4ms
ls              11.5ms
cat-head        11.9ms
grep-rn         20.2ms
git-status      25.1ms
cd-pwd          11.0ms

anatomy (ms, mean of 20 launches with BASH_TIMING_LOG set):
segment                                  true         ls   cat-head    grep-rn git-status     cd-pwd
---------------------------------------------------------------------------------------------
runtime start (creation to Main)         7.10       6.65       6.66       6.97       6.78       6.82
console set-up                           0.29       0.28       0.26       0.28       0.27       0.28
evaluator (environment, PATH)            1.33       1.23       1.21       1.28       1.24       1.24
options, startup files                   0.14       0.13       0.13       0.13       0.14       0.13
parse the call, reach source             0.11       0.12       0.11       0.12       0.12       0.11
snapshot (source)                        0.56       0.53       0.55       0.54       0.57       0.54
wrapper builtins                         0.01       0.01       0.01       0.01       0.01       0.01
the command (eval)                       0.00       0.12       0.29       8.00      12.80       0.04
  of which external programs             0.00       0.00       0.00       0.00      12.65       0.00
cwd write-back (pwd -P >|)               0.62       0.62       0.62       0.60       0.62       0.59
exit handler                             0.00       0.00       0.00       0.00       0.00       0.00
= the exit mark                         10.16       9.69       9.85      17.92      22.56       9.77
process exit (after the mark)            3.97       3.73       3.72       4.29       3.86       3.79
whole call (switch on)                  14.14      13.43      13.58      22.22      26.42      13.57
criterion: exit <= 10%            UNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTED
```

## 2026-10-18 09:15 claudecall.sh

```
date     : 2026-10-18 09:14:55
C#Bash   : F:/TEMP/claude/F--Koliada-Tools-bash/a6218fb2-86a7-48c3-bedd-2cb7a1a89778/scratchpad/rev316/Bash.exe   [--cs F:/TEMP/claude/F--Koliada-Tools-bash/a6218fb2-86a7-48c3-bedd-2cb7a1a89778/scratchpad/rev316/Bash.exe]
           C#Bash build 1.0.0+hg.32784b3827fd 316
method   : Claude Code's -c wrapper with the snapshot fixture, launched by tools/benchtimer from the repo root;
           each row the median of 5 samples of 20 launches (mean per launch), builds in alternating rounds

call           C#Bash
------------------------------------------
bare            10.6ms
true            13.0ms
ls              13.1ms
cat-head        13.1ms
grep-rn         21.6ms
git-status      26.1ms
cd-pwd          12.9ms

anatomy (ms, mean of 20 launches with BASH_TIMING_LOG set):
segment                                  true         ls   cat-head    grep-rn git-status     cd-pwd
---------------------------------------------------------------------------------------------
runtime start (creation to Main)         6.99       6.86       7.12       7.16       7.08       6.91
console set-up                           0.66       0.64       0.65       0.69       0.70       0.64
evaluator (environment, PATH)            1.64       1.63       1.64       1.65       1.78       1.60
options, startup files                   0.18       0.17       0.17       0.17       0.18       0.17
parse the call, reach source             0.16       0.15       0.15       0.15       0.17       0.15
snapshot (source)                        1.41       1.41       1.38       1.40       1.47       1.43
wrapper builtins                         0.01       0.01       0.02       0.01       0.02       0.01
the command (eval)                       0.00       0.14       0.35       7.83      12.40       0.04
  of which external programs             0.00       0.00       0.00       0.00      12.13       0.00
cwd write-back (pwd -P >|)               0.58       0.58       0.62       0.61       0.64       0.59
exit handler                             0.00       0.00       0.00       0.00       0.00       0.00
= the exit mark                         11.63      11.59      12.09      19.66      24.44      11.56
process exit (after the mark)            2.29       2.20       2.24       2.68       2.35       2.19
whole call (switch on)                  13.92      13.79      14.33      22.34      26.80      13.75
criterion: exit <= 10%            UNACCOUNTEDUNACCOUNTEDUNACCOUNTEDUNACCOUNTED        metUNACCOUNTED
```
