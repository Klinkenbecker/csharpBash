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

## 2026-10-10 15:21

```
date     : 2026-10-10 15:17:16
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.e6a9a866b65a+ 179+
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.161      1.496      0.414       9.3x     2.6x
arith            0.244      2.076      0.625       8.5x     2.6x
func             0.213      2.332      0.576      10.9x     2.7x
loop_big         1.405     15.243      3.430      10.8x     2.4x
coreutils        0.057     14.755      0.861     258.9x    15.1x *
pipeline         0.250      0.271      0.122       1.1x     0.5x *
find             0.021      0.125      0.205       6.0x     9.8x *
startup         0.0182     0.0160     0.1130       0.9x     6.2x
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

## 2026-10-10 15:26

```
date     : 2026-10-10 15:22:14
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe.rev156   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.6e935b12b8c7+ 156+
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.166      1.507      0.440       9.1x     2.7x
arith            0.239      2.062      0.634       8.6x     2.7x
func             0.220      2.304      0.573      10.5x     2.6x
loop_big         1.377     15.056      3.409      10.9x     2.5x
coreutils        0.058     14.724      0.828     253.9x    14.3x *
pipeline         0.249      0.265      0.121       1.1x     0.5x *
find             0.021      0.125      0.213       6.0x    10.1x *
startup         0.0159     0.0157     0.1133       1.0x     7.1x
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

## 2026-10-10 15:30

```
date     : 2026-10-10 15:26:48
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.e6a9a866b65a+ 179+
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.158      1.480      0.421       9.4x     2.7x
arith            0.239      2.050      0.621       8.6x     2.6x
func             0.204      2.289      0.572      11.2x     2.8x
loop_big         1.367     14.956      3.392      10.9x     2.5x
coreutils        0.056     14.579      0.827     260.3x    14.8x *
pipeline         0.242      0.259      0.118       1.1x     0.5x *
find             0.021      0.123      0.210       5.9x    10.0x *
startup         0.0160     0.0157     0.1098       1.0x     6.9x
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

## 2026-10-10 15:35

```
date     : 2026-10-10 15:31:05
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe.rev156   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.6e935b12b8c7+ 156+
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.153      1.472      0.411       9.6x     2.7x
arith            0.235      2.051      0.622       8.7x     2.6x
func             0.213      2.284      0.572      10.7x     2.7x
loop_big         1.361     14.930      3.398      11.0x     2.5x
coreutils        0.055     14.425      0.827     262.3x    15.0x *
pipeline         0.247      0.259      0.128       1.0x     0.5x *
find             0.021      0.122      0.214       5.8x    10.2x *
startup         0.0148     0.0158     0.1077       1.1x     7.3x
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

## 2026-10-11 07:38

```
date     : 2026-10-11 07:32:34
C#Bash   : /f/TEMP/claude/F--Koliada-Tools-bash/e37e3586-0995-41b9-b101-9058f7fb5189/scratchpad/aot200/Bash.exe   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.165      1.512      0.418       9.2x     2.5x
arith            0.258      2.098      0.630       8.1x     2.4x
func             0.222      2.343      0.595      10.6x     2.7x
loop_big         1.441     15.267      3.483      10.6x     2.4x
coreutils        0.060     15.586      0.945     259.8x    15.8x *
pipeline         0.250      0.312      0.150       1.2x     0.6x *
find             0.021      0.122      0.208       5.8x     9.9x *
startup         0.0164     0.0165     0.1248       1.0x     7.6x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', median of 5 samples, each the total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = WSL
  arith       = Git Bash, = WSL
  func        = Git Bash, = WSL
  coreutils   = Git Bash, = WSL
  pipeline    = Git Bash, = WSL
  find        = Git Bash, = WSL
```

## 2026-10-11 07:43

```
date     : 2026-10-11 07:38:21
C#Bash   : /f/TEMP/claude/F--Koliada-Tools-bash/e37e3586-0995-41b9-b101-9058f7fb5189/scratchpad/aot204/Bash.exe   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.c0e44d8209ca 204
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.157      1.486      0.427       9.5x     2.7x
arith            0.254      2.089      0.628       8.2x     2.5x
func             0.210      2.314      0.595      11.0x     2.8x
loop_big         1.421     15.642      3.414      11.0x     2.4x
coreutils        0.059     16.041      0.851     271.9x    14.4x *
pipeline         0.253      0.279      0.117       1.1x     0.5x *
find             0.023      0.124      0.219       5.4x     9.5x *
startup         0.0166     0.0171     0.1186       1.0x     7.1x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', median of 5 samples, each the total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = WSL
  arith       = Git Bash, = WSL
  func        = Git Bash, = WSL
  coreutils   = Git Bash, = WSL
  pipeline    = Git Bash, = WSL
  find        = Git Bash, = WSL
```

## 2026-10-11 08:33

```
date     : 2026-10-11 08:27:48
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.4fa037d780c8 209
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.162      1.493      0.422       9.2x     2.6x
arith            0.243      2.051      0.618       8.4x     2.5x
func             0.210      2.293      0.573      10.9x     2.7x
loop_big         1.406     14.926      3.419      10.6x     2.4x
coreutils        0.059     14.956      0.857     253.5x    14.5x *
pipeline         0.245      0.267      0.122       1.1x     0.5x *
find             0.021      0.122      0.214       5.8x    10.2x *
startup         0.0153     0.0159     0.1100       1.0x     7.2x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', median of 5 samples, each the total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = WSL
  arith       = Git Bash, = WSL
  func        = Git Bash, = WSL
  coreutils   = Git Bash, = WSL
  pipeline    = Git Bash, = WSL
  find        = Git Bash, = WSL
```
