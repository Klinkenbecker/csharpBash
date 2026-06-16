Bash startup sequence before the first prompt

  What runs depends on how bash was invoked:

  Interactive login shell (bash -l, console login, ssh user@host):
  1. /etc/profile — typically sources /etc/profile.d/*.sh and, on Debian/Arch-family distros, /etc/bash.bashrc.
  2. The first existing of ~/.bash_profile → ~/.bash_login → ~/.profile (only the first one found is read). Convention:
  ~/.bash_profile sources ~/.bashrc.

  Interactive non-login shell (opening a new terminal, plain bash):
  1. /etc/bash.bashrc — distro addition, not vanilla bash.
  2. ~/.bashrc.

  Non-interactive (running a script):
  - If $BASH_ENV is set, expand it and source that file. Nothing else — no profile, no bashrc.
  - Special case: if bash detects stdin is a network connection (run by sshd/rshd), it reads ~/.bashrc even though
  non-interactive.

  Invoked as sh: POSIX mode — /etc/profile + ~/.profile if login; if interactive, the file named by $ENV.

  Then, common to every interactive shell, just before the prompt appears:
  1. Readline initializes (lazily, on first read): reads $INPUTRC, else /etc/inputrc, then ~/.inputrc. ← this is where
  your ESC binding takes effect.
  2. Prompt assembly: if $PROMPT_COMMAND is set it runs first, then PS1 is expanded (prompt \-escapes, then
  parameter/command/arithmetic expansion).
  3. PS1 is written to stderr and bash blocks on readline for input.

  Flags that alter the above: --norc, --noprofile, --rcfile FILE, --login.
