# compare3.sh results

Appended by every run of `tests/bench/compare3.sh`. Newest last. Do not edit by hand.

## 2026-10-06 04:06

```
date     : 2026-10-06 04:02:56
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.34ae06dffefa+ 136+
Git Bash : /usr/bin/_bash
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.182      1.610      0.460       8.8x     2.5x
arith            0.270      2.128      0.639       7.9x     2.4x
func             0.226      2.320      0.612      10.3x     2.7x
loop_big         1.366     14.955      3.599      10.9x     2.6x
coreutils        0.091     20.874      0.873     229.4x     9.6x *
pipeline         0.278      0.439      0.165       1.6x     0.6x *
find             0.047      0.181      0.257       3.9x     5.5x *
startup         0.0420     0.0271     0.1351       0.6x     3.2x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = WSL
  arith       = Git Bash, = WSL
  func        = Git Bash, = WSL
  coreutils   = Git Bash, = WSL
  pipeline    = Git Bash, = WSL
  find        = Git Bash, = WSL
```

## 2026-10-06 04:12

```
date     : 2026-10-06 04:08:08
C#Bash   : dist/Bash.exe.rev127   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.656eea166672+ 127+
Git Bash : /usr/bin/_bash
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.180      1.604      0.466       8.9x     2.6x
arith            0.264      2.074      0.633       7.9x     2.4x
func             0.224      2.314      0.600      10.3x     2.7x
loop_big         1.375     15.198      3.697      11.1x     2.7x
coreutils        0.081     20.911      0.851     258.2x    10.5x *
pipeline         0.284      0.437      0.152       1.5x     0.5x *
find             0.045      0.175      0.265       3.9x     5.9x *
startup         0.0412     0.0271     0.1310       0.7x     3.2x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = WSL
  arith       = Git Bash, = WSL
  func        = Git Bash, = WSL
  coreutils   = Git Bash, = WSL
  pipeline    = Git Bash, = WSL
  find        = Git Bash, = WSL
```

## 2026-10-06 19:04

```
date     : 2026-10-06 19:00:15
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.34ae06dffefa+ 136+
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.163      1.562      0.436       9.6x     2.7x
arith            0.244      2.051      0.606       8.4x     2.5x
func             0.201      2.278      0.572      11.3x     2.8x
loop_big         1.339     14.888      3.478      11.1x     2.6x
coreutils        0.055     13.742      0.845     249.9x    15.4x *
pipeline         0.265      0.310      0.123       1.2x     0.5x *
find             0.020      0.123      0.239       6.1x    11.9x *
startup         0.0152     0.0162     0.1036       1.1x     6.8x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = WSL
  arith       = Git Bash, = WSL
  func        = Git Bash, = WSL
  coreutils   = Git Bash, = WSL
  pipeline    = Git Bash, = WSL
  find        = Git Bash, = WSL
```
