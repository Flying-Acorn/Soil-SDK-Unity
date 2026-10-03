#!/bin/sh
# Regenerates the small media files the tests load. Needs ffmpeg.
set -e
cd "$(dirname "$0")/Tests/Resources"
FFMPEG="${FFMPEG:-ffmpeg}"
# 2.5 s, 320x240, H.264 + AAC.
"$FFMPEG" -y -loglevel error -f lavfi -i testsrc=size=320x240:rate=30:duration=2.5 \
  -f lavfi -i sine=frequency=440:duration=2.5 -c:v libx264 -pix_fmt yuv420p -profile:v baseline \
  -c:a aac -b:a 64k -shortest -movflags +faststart video.mp4
# Audio only: playable but no video track.
"$FFMPEG" -y -loglevel error -f lavfi -i sine=frequency=440:duration=1 -c:a aac -b:a 64k audio_only.m4a
"$FFMPEG" -y -loglevel error -f lavfi -i testsrc=size=640x960:rate=1 -frames:v 1 image.png
"$FFMPEG" -y -loglevel error -f lavfi -i color=c=orange:size=128x128 -frames:v 1 logo.jpg
# Not media at all.
printf 'this is not a video file, just some bytes' > broken.mp4
printf 'this is not an image file, just some bytes' > broken.png
ls -l
