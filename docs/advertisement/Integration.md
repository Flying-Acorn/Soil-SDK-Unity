# Advertisement Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

## Video Ad Optimization

Use these FFmpeg commands to optimize video ads:

```bash
ffmpeg -i input_video.mov -c:v libx264 -crf 25 -c:a aac -b:a 128k -movflags +faststart -fs 15000000 -y output_video_ad.mp4
```

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.

## Usage

```csharp
// Load and show ad
await Advertisement.ShowAdAsync();
```