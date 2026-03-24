#!/bin/bash
# PostToolUse hook: runs dotnet build after Claude edits a .cs or .xaml file.
# Gives Claude immediate feedback on compile errors so it can self-correct.
# No external dependencies (no jq).

INPUT=$(cat)

# Extract file_path from hook JSON using pure bash/sed
FILE_PATH=$(echo "$INPUT" | grep -o '"file_path":"[^"]*"' | head -1 | sed 's/"file_path":"//;s/"$//')

[ -z "$FILE_PATH" ] && exit 0

case "$FILE_PATH" in
  *.cs|*.xaml) ;;
  *) exit 0 ;;
esac

cd "$CLAUDE_PROJECT_DIR" || exit 0

# Build the solution quietly — only show errors/warnings
OUTPUT=$(dotnet build --nologo -v q 2>&1)
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
  # Feed build errors to stdout — Claude sees stdout as context and can self-correct
  echo "dotnet build FAILED after editing $FILE_PATH:"
  echo "$OUTPUT" | grep -E "(error |warning )" | head -20
fi

# Always exit 0 — don't block the edit, just inform Claude
exit 0
