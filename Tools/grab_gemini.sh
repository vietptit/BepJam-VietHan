#!/bin/bash
# Wait for the next Gemini download in ~/Downloads and move it into Tools/gemini/raw/<name>.png
NAME="$1"
DL="/c/Users/DELL/Downloads"
RAW="$(dirname "$0")/gemini/raw"
mkdir -p "$RAW"
for i in $(seq 1 90); do
  F=$(ls -t "$DL"/Gemini_Generated_Image_*.png 2>/dev/null | head -1)
  if [ -n "$F" ] && [ ! -f "$F.crdownload" ]; then
    sleep 1; mv "$F" "$RAW/$NAME.png"; echo "saved $RAW/$NAME.png"; exit 0
  fi
  sleep 2
done
echo "timeout waiting for download"; exit 1
