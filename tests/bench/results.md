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

## 2026-10-11 11:07

```
date     : 2026-10-11 11:02:22
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.40a4296afbd6 213
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.166      1.530      0.436       9.2x     2.6x
arith            0.249      2.084      0.616       8.4x     2.5x
func             0.217      2.331      0.595      10.7x     2.7x
loop_big         1.439     15.111      3.442      10.5x     2.4x
coreutils        0.058     15.985      0.909     275.6x    15.7x *
pipeline         0.250      0.283      0.135       1.1x     0.5x *
find             0.021      0.124      0.221       5.9x    10.5x *
startup         0.0162     0.0169     0.1162       1.0x     7.2x
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

## 2026-10-11 17:11

```
date     : 2026-10-11 17:05:43
C#Bash   : /f/TEMP/claude/F--Koliada-Tools-bash/e37e3586-0995-41b9-b101-9058f7fb5189/scratchpad/aottraps/Bash.exe   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.3b0898199c10+ 216+
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.163      1.500      0.427       9.2x     2.6x
arith            0.248      2.069      0.619       8.3x     2.5x
func             0.218      2.330      0.587      10.7x     2.7x
loop_big         1.421     15.275      3.419      10.7x     2.4x
coreutils        0.058     15.704      0.830     270.8x    14.3x *
pipeline         0.248      0.270      0.134       1.1x     0.5x *
find             0.022      0.125      0.208       5.7x     9.5x *
startup         0.0161     0.0161     0.1122       1.0x     7.0x
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

## 2026-10-11 17:16

```
date     : 2026-10-11 17:11:06
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.40a4296afbd6 213
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.164      1.489      0.427       9.1x     2.6x
arith            0.246      2.058      0.614       8.4x     2.5x
func             0.212      2.319      0.575      10.9x     2.7x
loop_big         1.434     14.997      3.387      10.5x     2.4x
coreutils        0.056     15.851      0.823     283.1x    14.7x *
pipeline         0.246      0.280      0.115       1.1x     0.5x *
find             0.021      0.126      0.225       6.0x    10.7x *
startup         0.0158     0.0165     0.1120       1.0x     7.1x
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

## 2026-10-12 07:00

```
date     : 2026-10-12 06:54:48
C#Bash   : /f/TEMP/claude/F--Koliada-Tools-bash/e37e3586-0995-41b9-b101-9058f7fb5189/scratchpad/aot229/Bash.exe   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.1eb31ba5124d 229
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.166      1.517      0.427       9.1x     2.6x
arith            0.255      2.080      0.645       8.2x     2.5x
func             0.230      2.334      0.587      10.1x     2.6x
loop_big         1.459     19.193      3.447      13.2x     2.4x
coreutils        0.063     16.426      0.850     260.7x    13.5x *
pipeline         0.248      0.286      0.117       1.2x     0.5x *
find             0.023      0.131      0.221       5.7x     9.6x *
startup         0.0161     0.0166     0.1151       1.0x     7.1x
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

## 2026-10-12 07:06

```
date     : 2026-10-12 07:00:37
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.64cd033c7de6 217
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.167      1.490      0.442       8.9x     2.6x
arith            0.244      2.056      0.629       8.4x     2.6x
func             0.213      2.287      0.590      10.7x     2.8x
loop_big         1.420     18.918      3.439      13.3x     2.4x
coreutils        0.060     16.341      0.827     272.4x    13.8x *
pipeline         0.251      0.292      0.130       1.2x     0.5x *
find             0.022      0.127      0.227       5.8x    10.3x *
startup         0.0163     0.0167     0.1160       1.0x     7.1x
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

## 2026-10-12 07:30

```
date     : 2026-10-12 07:23:00
C#Bash   : /f/Koliada/Tools/bash/tests/bench/aot/Bash.exe   [--build: rev 231, NOT deployed]
           C#Bash build 1.0.0+hg.2b6bfa4df96c 231
deployed : /f/Koliada/Tools/bash/dist/Bash.exe   [timed in alternating rounds with the build]
           C#Bash build 1.0.0+hg.64cd033c7de6 217
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark        build   deployed   change   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------------------------
loop             0.159      0.163    -2.5%      1.510      0.432       9.5x     2.7x
arith            0.248      0.245    +1.2%      2.119      0.696       8.5x     2.8x
func             0.229      0.230    -0.4%      2.331      0.609      10.2x     2.7x
loop_big         1.404      1.437    -2.3%     15.129      3.398      10.8x     2.4x
coreutils        0.058      0.059    -1.7%     16.727      0.873     288.4x    15.1x *
pipeline         0.253      0.251    +0.8%      0.280      0.124       1.1x     0.5x *
find             0.023      0.022    +4.5%      0.128      0.225       5.6x     9.8x *
startup         0.0163     0.0165    -1.2%     0.0171     0.1335       1.0x     8.2x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', median of 5 samples, each the total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = deployed, = WSL
  arith       = Git Bash, = deployed, = WSL
  func        = Git Bash, = deployed, = WSL
  coreutils   = Git Bash, = deployed, = WSL
  pipeline    = Git Bash, = deployed, = WSL
  find        = Git Bash, = deployed, = WSL
```

## 2026-10-12 07:46

```
date     : 2026-10-12 07:41:07
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.2a0678c462c5 233
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.169      1.537      0.426       9.1x     2.5x
arith            0.254      2.082      0.642       8.2x     2.5x
func             0.221      2.393      0.588      10.8x     2.7x
loop_big         1.425     15.165      3.469      10.6x     2.4x
coreutils        0.058     15.962      0.821     275.2x    14.2x *
pipeline         0.254      0.274      0.125       1.1x     0.5x *
find             0.023      0.126      0.209       5.5x     9.1x *
startup         0.0158     0.0165     0.1150       1.0x     7.3x
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

## 2026-10-12 10:16

```
date     : 2026-10-12 10:09:38
C#Bash   : /f/Koliada/Tools/bash/tests/bench/aot/Bash.exe   [--build: rev 242, NOT deployed]
           C#Bash build 1.0.0+hg.9cd123e6db0a 242
deployed : /f/Koliada/Tools/bash/dist/Bash.exe   [timed in alternating rounds with the build]
           C#Bash build 1.0.0+hg.2a0678c462c5 233
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark        build   deployed   change   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------------------------
loop             0.161      0.157    +2.5%      1.470      0.421       9.1x     2.6x
arith            0.242      0.245    -1.2%      2.048      0.619       8.5x     2.6x
func             0.212      0.214    -0.9%      2.279      0.603      10.8x     2.8x
loop_big         1.387      1.390    -0.2%     14.841      3.354      10.7x     2.4x
coreutils        0.057      0.059    -3.4%     15.830      0.820     277.7x    14.4x *
pipeline         0.245      0.246    -0.4%      0.285      0.128       1.2x     0.5x *
find             0.022      0.021    +4.8%      0.124      0.208       5.6x     9.5x *
startup         0.0158     0.0154    +2.6%     0.0164     0.1132       1.0x     7.2x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', median of 5 samples, each the total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = deployed, = WSL
  arith       = Git Bash, = deployed, = WSL
  func        = Git Bash, = deployed, = WSL
  coreutils   = Git Bash, = deployed, = WSL
  pipeline    = Git Bash, = deployed, = WSL
  find        = Git Bash, = deployed, = WSL
```

## 2026-10-12 11:31

```
date     : 2026-10-12 11:24:42
C#Bash   : /f/Koliada/Tools/bash/tests/bench/aot/Bash.exe   [--build: rev 247, NOT deployed]
           C#Bash build 1.0.0+hg.e77806c9c24c 247
deployed : /f/Koliada/Tools/bash/dist/Bash.exe   [timed in alternating rounds with the build]
           C#Bash build 1.0.0+hg.2a0678c462c5 233
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark        build   deployed   change   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------------------------
loop             0.160      0.161    -0.6%      1.473      0.429       9.2x     2.7x
arith            0.242      0.246    -1.6%      2.054      0.637       8.5x     2.6x
func             0.222      0.216    +2.8%      2.285      0.606      10.3x     2.7x
loop_big         1.392      1.391    +0.1%     18.942      3.439      13.6x     2.5x
coreutils        0.063      0.057   +10.5%     15.849      0.839     251.6x    13.3x *
pipeline         0.248      0.248    +0.0%      0.299      0.125       1.2x     0.5x *
find             0.022      0.021    +4.8%      0.127      0.224       5.8x    10.2x *
startup         0.0162     0.0160    +1.2%     0.0170     0.1132       1.0x     7.0x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', median of 5 samples, each the total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = deployed, = WSL
  arith       = Git Bash, = deployed, = WSL
  func        = Git Bash, = deployed, = WSL
  coreutils   = Git Bash, = deployed, = WSL
  pipeline    = Git Bash, = deployed, = WSL
  find        = Git Bash, = deployed, = WSL
```

## 2026-10-12 11:48

```
date     : 2026-10-12 11:41:15
C#Bash   : /f/Koliada/Tools/bash/tests/bench/aot/Bash.exe   [--build: rev 249, NOT deployed]
           C#Bash build 1.0.0+hg.17746812a69a 249
deployed : /f/Koliada/Tools/bash/dist/Bash.exe   [timed in alternating rounds with the build]
           C#Bash build 1.0.0+hg.2a0678c462c5 233
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark        build   deployed   change   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------------------------
loop             0.166      0.161    +3.1%      1.502      0.434       9.0x     2.6x
arith            0.247      0.251    -1.6%      2.062      0.650       8.3x     2.6x
func             0.224      0.215    +4.2%      2.332      0.574      10.4x     2.6x
loop_big         1.420      1.406    +1.0%     15.174      3.489      10.7x     2.5x
coreutils        0.060      0.057    +5.3%     15.988      0.830     266.5x    13.8x *
pipeline         0.248      0.250    -0.8%      0.294      0.121       1.2x     0.5x *
find             0.023      0.023    +0.0%      0.139      0.239       6.0x    10.4x *
startup         0.0171     0.0170    +0.6%     0.0173     0.1144       1.0x     6.7x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', median of 5 samples, each the total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = deployed, = WSL
  arith       = Git Bash, = deployed, = WSL
  func        = Git Bash, = deployed, = WSL
  coreutils   = Git Bash, = deployed, = WSL
  pipeline    = Git Bash, = deployed, = WSL
  find        = Git Bash, = deployed, = WSL
```

## 2026-10-13 07:47

```
date     : 2026-10-13 07:41:41
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.099fd70fc65b 257
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.169      1.515      0.452       9.0x     2.7x
arith            0.249      2.083      0.634       8.4x     2.5x
func             0.215      2.307      0.591      10.7x     2.7x
loop_big         1.398     15.034      3.425      10.8x     2.4x
coreutils        0.061     16.696      0.834     273.7x    13.7x *
pipeline         0.246      0.304      0.132       1.2x     0.5x *
find             0.022      0.128      0.209       5.8x     9.5x *
startup         0.0166     0.0172     0.1171       1.0x     7.1x
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

## 2026-10-16 05:46

```
date     : 2026-10-16 05:40:15
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.a160167e4eb9 266
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.171      1.546      0.478       9.0x     2.8x
arith            0.250      2.111      0.662       8.4x     2.6x
func             0.220      2.338      0.623      10.6x     2.8x
loop_big         1.403     15.352      3.523      10.9x     2.5x
coreutils        0.062     19.451      0.868     313.7x    14.0x *
pipeline         0.254      0.355      0.138       1.4x     0.5x *
find             0.024      0.137      0.234       5.7x     9.8x *
startup         0.0181     0.0188     0.1330       1.0x     7.3x
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

## 2026-10-16 12:12

```
date     : 2026-10-16 12:05:02
C#Bash   : /f/Koliada/Tools/bash/tests/bench/aot/Bash.exe   [--build: rev 277, NOT deployed]
           C#Bash build 1.0.0+hg.5b31237b2b44 277
deployed : /f/Koliada/Tools/bash/dist/Bash.exe   [timed in alternating rounds with the build]
           C#Bash build 1.0.0+hg.a160167e4eb9 266
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark        build   deployed   change   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------------------------
loop             0.169      0.159    +6.3%      1.526      0.450       9.0x     2.7x
arith            0.256      0.250    +2.4%      2.123      0.666       8.3x     2.6x
func             0.226      0.225    +0.4%      2.383      0.589      10.5x     2.6x
loop_big         1.464      1.410    +3.8%     19.359      3.545      13.2x     2.4x
coreutils        0.064      0.065    -1.5%     19.453      0.861     304.0x    13.5x *
pipeline         0.257      0.250    +2.8%      0.368      0.149       1.4x     0.6x *
find             0.023      0.024    -4.2%      0.138      0.230       6.0x    10.0x *
startup         0.0181     0.0185    -2.2%     0.0188     0.1357       1.0x     7.5x
  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost.
  startup = bash -c 'exit 0', median of 5 samples, each the total of 100 consecutive launches / 100.

output agreement:
  loop        = Git Bash, = deployed, = WSL
  arith       = Git Bash, = deployed, = WSL
  func        = Git Bash, = deployed, = WSL
  coreutils   = Git Bash, = deployed, = WSL
  pipeline    = Git Bash, = deployed, = WSL
  find        = Git Bash, = deployed, = WSL
```

## 2026-10-16 18:19

```
date     : 2026-10-16 18:13:03
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.0cc3c25dbada 286
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.169      1.583      0.448       9.4x     2.7x
arith            0.251      2.104      0.641       8.4x     2.6x
func             0.217      2.362      0.608      10.9x     2.8x
loop_big         1.415     15.193      3.528      10.7x     2.5x
coreutils        0.063     20.111      0.851     319.2x    13.5x *
pipeline         0.264      0.363      0.140       1.4x     0.5x *
find             0.023      0.135      0.225       5.9x     9.8x *
startup         0.0193     0.0190     0.1325       1.0x     6.9x
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

## 2026-10-18 07:18

```
date     : 2026-10-18 07:13:05
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.32784b3827fd 316
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.185      1.561      0.446       8.4x     2.4x
arith            0.254      2.148      0.646       8.5x     2.5x
func             0.229      2.391      0.597      10.4x     2.6x
loop_big         1.491     15.658      3.585      10.5x     2.4x
coreutils        0.066     15.219      0.914     230.6x    13.8x *
pipeline         0.266      0.286      0.122       1.1x     0.5x *
find             0.024      0.136      0.236       5.7x     9.8x *
startup         0.0183     0.0164     0.1114       0.9x     6.1x
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

## 2026-10-18 07:46

```
date     : 2026-10-18 07:40:53
C#Bash   : dist/Bash.exe.rev286   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.0cc3c25dbada 286
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.160      1.522      0.430       9.5x     2.7x
arith            0.246      2.101      0.631       8.5x     2.6x
func             0.213      2.340      0.587      11.0x     2.8x
loop_big         1.411     15.166      3.523      10.7x     2.5x
coreutils        0.058     13.709      0.851     236.4x    14.7x *
pipeline         0.246      0.261      0.129       1.1x     0.5x *
find             0.021      0.122      0.203       5.8x     9.7x *
startup         0.0141     0.0152     0.1124       1.1x     8.0x
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

## 2026-10-18 07:51

```
date     : 2026-10-18 07:46:38
C#Bash   : dist/Bash.exe.rev302   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.c815b7a0b05c 302
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.162      1.535      0.421       9.5x     2.6x
arith            0.248      2.087      0.642       8.4x     2.6x
func             0.213      2.342      0.585      11.0x     2.7x
loop_big         1.408     15.298      3.482      10.9x     2.5x
coreutils        0.060     14.037      0.819     234.0x    13.7x *
pipeline         0.259      0.300      0.130       1.2x     0.5x *
find             0.020      0.121      0.219       6.0x    10.9x *
startup         0.0143     0.0149     0.1153       1.0x     8.1x
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

## 2026-10-18 07:57

```
date     : 2026-10-18 07:51:54
C#Bash   : dist/Bash.exe.rev296   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.82d59ea69d63 296
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.160      1.515      0.434       9.5x     2.7x
arith            0.244      2.106      0.634       8.6x     2.6x
func             0.214      2.351      0.589      11.0x     2.8x
loop_big         1.428     15.397      3.568      10.8x     2.5x
coreutils        0.065     13.897      0.878     213.8x    13.5x *
pipeline         0.252      0.265      0.132       1.1x     0.5x *
find             0.020      0.122      0.214       6.1x    10.7x *
startup         0.0141     0.0155     0.1119       1.1x     7.9x
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

## 2026-10-18 08:02

```
date     : 2026-10-18 07:57:11
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.32784b3827fd 316
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.155      1.506      0.435       9.7x     2.8x
arith            0.240      2.104      0.632       8.8x     2.6x
func             0.212      2.357      0.589      11.1x     2.8x
loop_big         1.414     15.371      3.460      10.9x     2.4x
coreutils        0.057     13.546      0.837     237.6x    14.7x *
pipeline         0.242      0.248      0.140       1.0x     0.6x *
find             0.020      0.119      0.224       5.9x    11.2x *
startup         0.0144     0.0149     0.1128       1.0x     7.8x
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

## 2026-10-18 09:20

```
date     : 2026-10-18 09:15:43
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.8ae72c3b647d 320
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.158      1.547      0.440       9.8x     2.8x
arith            0.242      2.098      0.629       8.7x     2.6x
func             0.212      2.324      0.571      11.0x     2.7x
loop_big         1.410     15.204      3.494      10.8x     2.5x
coreutils        0.056     13.448      0.848     240.1x    15.1x *
pipeline         0.244      0.220      0.116       0.9x     0.5x *
find             0.018      0.117      0.227       6.5x    12.6x *
startup         0.0095     0.0148     0.1108       1.6x    11.7x
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

## 2026-10-18 09:26

```
date     : 2026-10-18 09:21:28
C#Bash   : F:/TEMP/claude/F--Koliada-Tools-bash/a6218fb2-86a7-48c3-bedd-2cb7a1a89778/scratchpad/rev316/Bash.exe   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.32784b3827fd 316
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.157      1.522      0.424       9.7x     2.7x
arith            0.241      2.083      0.628       8.6x     2.6x
func             0.213      2.352      0.578      11.0x     2.7x
loop_big         1.406     15.180      3.488      10.8x     2.5x
coreutils        0.058     13.362      0.836     230.4x    14.4x *
pipeline         0.244      0.223      0.120       0.9x     0.5x *
find             0.018      0.116      0.219       6.4x    12.2x *
startup         0.0106     0.0146     0.1089       1.4x    10.3x
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

## 2026-10-18 11:49

```
date     : 2026-10-18 11:44:19
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.7b2c529cd84f 324
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.169      1.530      0.419       9.1x     2.5x
arith            0.245      2.111      0.626       8.6x     2.6x
func             0.213      2.345      0.583      11.0x     2.7x
loop_big         1.458     19.983      3.536      13.7x     2.4x
coreutils        0.058     14.618      0.890     252.0x    15.3x *
pipeline         0.260      0.263      0.125       1.0x     0.5x *
find             0.018      0.135      0.205       7.5x    11.4x *
startup         0.0092     0.0167     0.1097       1.8x    11.9x
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

## 2026-10-18 11:55

```
date     : 2026-10-18 11:49:48
C#Bash   : F:/TEMP/claude/F--Koliada-Tools-bash/a6218fb2-86a7-48c3-bedd-2cb7a1a89778/scratchpad/rev320/Bash.exe   [--cs (NOT the deployed build)]
           C#Bash build 1.0.0+hg.8ae72c3b647d 320
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.155      1.502      0.421       9.7x     2.7x
arith            0.241      2.073      0.623       8.6x     2.6x
func             0.212      2.303      0.579      10.9x     2.7x
loop_big         1.395     19.471      3.534      14.0x     2.5x
coreutils        0.056     13.808      0.821     246.6x    14.7x *
pipeline         0.245      0.223      0.127       0.9x     0.5x *
find             0.018      0.117      0.208       6.5x    11.6x *
startup         0.0098     0.0147     0.1077       1.5x    11.0x
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

## 2026-10-18 12:00

```
date     : 2026-10-18 11:55:37
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.7b2c529cd84f 324
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.156      1.503      0.418       9.6x     2.7x
arith            0.239      2.082      0.635       8.7x     2.7x
func             0.209      2.296      0.582      11.0x     2.8x
loop_big         1.400     19.287      3.495      13.8x     2.5x
coreutils        0.056     13.594      0.834     242.7x    14.9x *
pipeline         0.238      0.225      0.109       0.9x     0.5x *
find             0.017      0.119      0.207       7.0x    12.2x *
startup         0.0085     0.0145     0.1058       1.7x    12.4x
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

## 2026-10-18 17:25

```
date     : 2026-10-18 17:19:46
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.7b2c529cd84f 324
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.158      1.526      0.431       9.7x     2.7x
arith            0.241      2.093      0.636       8.7x     2.6x
func             0.208      2.345      0.592      11.3x     2.8x
loop_big         1.416     19.618      3.509      13.9x     2.5x
coreutils        0.057     14.252      0.849     250.0x    14.9x *
pipeline         0.245      0.262      0.120       1.1x     0.5x *
find             0.017      0.122      0.213       7.2x    12.5x *
startup         0.0089     0.0151     0.1146       1.7x    12.9x
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

## 2026-10-18 17:30

```
date     : 2026-10-18 17:25:17
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.7b2c529cd84f 324
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.157      1.503      0.418       9.6x     2.7x
arith            0.235      2.025      0.599       8.6x     2.5x
func             0.205      2.282      0.574      11.1x     2.8x
loop_big         1.396     18.652      3.419      13.4x     2.4x
coreutils        0.055     13.664      0.811     248.4x    14.7x *
pipeline         0.243      0.231      0.116       1.0x     0.5x *
find             0.017      0.120      0.202       7.1x    11.9x *
startup         0.0089     0.0151     0.1113       1.7x    12.5x
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

## 2026-10-18 17:36

```
date     : 2026-10-18 17:30:39
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.7b2c529cd84f 324
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.156      1.489      0.417       9.5x     2.7x
arith            0.236      2.051      0.613       8.7x     2.6x
func             0.206      2.304      0.569      11.2x     2.8x
loop_big         1.389     18.946      3.404      13.6x     2.5x
coreutils        0.057     14.342      0.858     251.6x    15.1x *
pipeline         0.247      0.282      0.130       1.1x     0.5x *
find             0.017      0.117      0.209       6.9x    12.3x *
startup         0.0091     0.0150     0.1164       1.6x    12.8x
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

## 2026-10-18 17:42

```
date     : 2026-10-18 17:37:03
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.7b2c529cd84f 324
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.157      1.501      0.418       9.6x     2.7x
arith            0.238      2.064      0.626       8.7x     2.6x
func             0.204      2.289      0.574      11.2x     2.8x
loop_big         1.393     19.313      3.421      13.9x     2.5x
coreutils        0.062     14.114      0.891     227.6x    14.4x *
pipeline         0.252      0.257      0.121       1.0x     0.5x *
find             0.017      0.130      0.204       7.6x    12.0x *
startup         0.0089     0.0151     0.1116       1.7x    12.5x
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

## 2026-10-18 18:26

```
date     : 2026-10-18 18:21:23
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.7b2c529cd84f 324
Git Bash : F:/Tools/PortableGit/usr/bin/_bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100
           no LANG or LC_* passed to any shell (Git Bash as Claude Code runs it: C locale)

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.157      1.512      0.431       9.6x     2.7x
arith            0.240      2.088      0.638       8.7x     2.7x
func             0.209      2.325      0.581      11.1x     2.8x
loop_big         1.420     15.036      3.397      10.6x     2.4x
coreutils        0.056     13.814      0.851     246.7x    15.2x *
pipeline         0.243      0.230      0.121       0.9x     0.5x *
find             0.017      0.123      0.222       7.2x    13.1x *
startup         0.0088     0.0150     0.1093       1.7x    12.4x
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

## 2026-10-19 04:20

```
date     : 2026-10-19 04:15:24
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.7b2c529cd84f 324
Git Bash : F:/Tools/PortableGit/bin/bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100
           no LANG or LC_* passed to any shell (Git Bash as Claude Code runs it: C locale)

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.160      1.506      0.425       9.4x     2.7x
arith            0.239      2.067      0.613       8.6x     2.6x
func             0.207      2.302      0.577      11.1x     2.8x
loop_big         1.393     14.958      3.509      10.7x     2.5x
coreutils        0.056     14.445      0.813     257.9x    14.5x *
pipeline         0.240      0.253      0.115       1.1x     0.5x *
find             0.017      0.129      0.203       7.6x    11.9x *
startup         0.0087     0.0233     0.1105       2.7x    12.7x
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

## 2026-10-19 18:50

```
date     : 2026-10-19 18:45:04
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.7b2c529cd84f 324
Git Bash : F:/Tools/PortableGit/bin/bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.40.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100
           no LANG or LC_* passed to any shell (Git Bash as Claude Code runs it: C locale)

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.162      1.562      0.435       9.6x     2.7x
arith            0.249      2.114      0.637       8.5x     2.6x
func             0.213      2.386      0.582      11.2x     2.7x
loop_big         1.445     15.543      3.488      10.8x     2.4x
coreutils        0.060     15.561      0.856     259.4x    14.3x *
pipeline         0.243      0.294      0.134       1.2x     0.6x *
find             0.017      0.134      0.221       7.9x    13.0x *
startup         0.0089     0.0236     0.1161       2.7x    13.0x
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

## 2026-10-28 03:14

```
date     : 2026-10-28 03:09:13
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.e521ecbee024 424
Git Bash : F:/Tools/PortableGit/bin/bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.54.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100
           no LANG or LC_* passed to any shell (Git Bash as Claude Code runs it: C locale)

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.157      1.557      0.412       9.9x     2.6x
arith            0.246      2.064      0.601       8.4x     2.4x
func             0.207      2.305      0.571      11.1x     2.8x
loop_big         1.406     14.927      3.462      10.6x     2.5x
coreutils        0.052     13.309      0.822     255.9x    15.8x *
pipeline         0.156      0.322      0.119       2.1x     0.8x *
find             0.016      0.131      0.221       8.2x    13.8x *
startup         0.0089     0.0216     0.0926       2.4x    10.4x
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

## 2026-10-28 10:39

```
date     : 2026-10-28 10:34:23
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.95c54cea077f 445
Git Bash : F:/Tools/PortableGit/bin/bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.54.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100
           no LANG or LC_* passed to any shell (Git Bash as Claude Code runs it: C locale)

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.162      1.503      0.418       9.3x     2.6x
arith            0.253      2.084      0.599       8.2x     2.4x
func             0.228      2.305      0.562      10.1x     2.5x
loop_big         1.534     20.058      3.452      13.1x     2.3x
coreutils        0.052     14.225      0.775     273.6x    14.9x *
pipeline         0.080      0.236      0.110       2.9x     1.4x *
find             0.014      0.132      0.197       9.4x    14.1x *
startup         0.0084     0.0271     0.1021       3.2x    12.2x
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

## 2026-10-28 11:03

```
date     : 2026-10-28 10:57:55
C#Bash   : /f/Koliada/Tools/bash/dist/Bash.exe   [dist (deployed)]
           C#Bash build 1.0.0+hg.a65944c9ec7c 448
Git Bash : F:/Tools/PortableGit/bin/bash.exe
           bash 4.4.23(1)-release
WSL bash : C:\WINDOWS/System32/wsl.exe -e bash
           bash 5.1.16(1)-release on Linux 6.18.54.1-microsoft-standard-WSL2
method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)
           rows = best of 9 (C#Bash) / best of 3 (Git Bash, WSL); startup = median of 5 samples, each total of 100 launches / 100
           no LANG or LC_* passed to any shell (Git Bash as Claude Code runs it: C locale)

benchmark       C#Bash   Git Bash        WSL     vs Git   vs WSL
----------------------------------------------------------------------
loop             0.154      1.499      0.406       9.7x     2.6x
arith            0.244      2.212      0.629       9.1x     2.6x
func             0.216      2.353      0.586      10.9x     2.7x
loop_big         1.435     19.984      3.401      13.9x     2.4x
coreutils        0.055     15.252      0.821     277.3x    14.9x *
pipeline         0.080      0.243      0.105       3.0x     1.3x *
find             0.015      0.131      0.206       8.7x    13.7x *
startup         0.0086     0.0236     0.1035       2.7x    12.0x
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
