using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.Core.Models;

public class HardwareCapabilities
{
    public string GpuName { get; set; } = "Unknown";
    public bool HasNvidia { get; set; }
    public bool HasAmd { get; set; }
    public bool HasIntel { get; set; }

    public bool SupportsNvencH264 { get; set; }
    public bool SupportsNvencHevc { get; set; }
    public bool SupportsMediaFoundationH264 { get; set; }
    public bool SupportsAmfH264 { get; set; }
    public bool SupportsAmfHevc { get; set; }
    public bool SupportsQsvH264 { get; set; }
    public bool SupportsQsvHevc { get; set; }

    public string NvencProbeStatus { get; set; } = "Chưa kiểm tra";
    public string GpuAccelerationName { get; set; } = "CPU";

    public int LogicalCores { get; set; } = Environment.ProcessorCount;
    public long TotalSystemMemoryMb { get; set; }

    public string RecommendedEncoderH264
    {
        get
        {
            if (SupportsNvencH264) return "h264_nvenc";
            if (SupportsMediaFoundationH264) return "h264_mf";
            if (SupportsQsvH264) return "h264_qsv";
            if (SupportsAmfH264) return "h264_amf";
            return "libx264";
        }
    }

    public string RecommendedEncoderHevc
    {
        get
        {
            if (SupportsNvencHevc) return "hevc_nvenc";
            if (SupportsQsvHevc) return "hevc_qsv";
            if (SupportsAmfHevc) return "hevc_amf";
            return "libx265";
        }
    }

    public string GetEncoderName(HardwareEncoderType preference, VideoCodecType codec)
    {
        if (preference == HardwareEncoderType.CPU)
        {
            return codec == VideoCodecType.H264 ? "libx264" : "libx265";
        }

        if (preference == HardwareEncoderType.NvidiaNvenc)
        {
            if (codec == VideoCodecType.H264)
            {
                if (SupportsNvencH264) return "h264_nvenc";
                if (SupportsMediaFoundationH264) return "h264_mf";
            }
            else
            {
                if (SupportsNvencHevc) return "hevc_nvenc";
            }
        }

        if (preference == HardwareEncoderType.IntelQsv && (codec == VideoCodecType.H264 ? SupportsQsvH264 : SupportsQsvHevc))
        {
            return codec == VideoCodecType.H264 ? "h264_qsv" : "hevc_qsv";
        }

        if (preference == HardwareEncoderType.AmdAmf && (codec == VideoCodecType.H264 ? SupportsAmfH264 : SupportsAmfHevc))
        {
            return codec == VideoCodecType.H264 ? "h264_amf" : "hevc_amf";
        }

        // Auto fallback
        return codec == VideoCodecType.H264 ? RecommendedEncoderH264 : RecommendedEncoderHevc;
    }
}
