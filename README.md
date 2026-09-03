# Auto Video Editor (tool-edit-cc) 🎬⚡

Ứng dụng Desktop Windows native bằng **C# / .NET 8 WPF** có khả năng tự động dựng video hàng loạt từ **Video Footage + Giọng đọc (Voiceover)**, với workflow tự động hóa thông minh theo tư duy CapCut.

---

## ✨ Tính Năng Nổi Bật

- 🎙️ **Phát hiện & Cắt khoảng lặng thông minh (Silence Removal)**: Tự động phân tích voice bằng thuật toán RMS & silenceremove, loại bỏ đoạn ngắt nghỉ dư thừa.
- 📐 **Căn khung hình chuẩn TikTok / Reels / Shorts**: Hỗ trợ 9:16, 16:9, 1:1, 4:5 với hiệu ứng nền mờ tự động (Fit with Blur) siêu nhẹ tối ưu theo CapCut.
- 🔁 **Tự động căn khớp thời lượng (Smart Looping / Trimming)**: Video tự động lặp lại mượt mà nếu ngắn hơn voice, hoặc cắt bớt nếu dài hơn.
- ✂️ **Cắt đầu / Cắt đuôi độc lập cho từng file (Intro/Outro Trimming)**: Cấu hình số giây cắt bỏ ở đầu và cuối cho từng file video/giọng đọc.
- ⏱️ **Dư cuối video (Outro Hold)**: Cho phép video chạy thêm X giây sau khi giọng đọc kết thúc giúp đoạn kết tự nhiên.
- 🚀 **Tăng tốc phần cứng GPU NVIDIA / MediaFoundation**: Hỗ trợ NVIDIA NVENC (`h264_nvenc`), GPU DirectX MediaFoundation (`h264_mf`), Intel QSV, AMD AMF và CPU fallback (`libx264`).
- 📋 **Quản lý danh sách trực quan (Dark Mode UI)**: Hiển thị toàn bộ video ngay khi import, hỗ trợ thêm/sửa/xóa dòng linh hoạt.
- 📝 **Hệ thống Log thời gian thực**: Theo dõi từng bước phân tích và hiển thị đầy đủ lệnh FFmpeg.

---

## 🛠️ Cấu Trúc Dự Án (Clean Architecture)

- `AutoVideoEditor.Core`: Models, Interfaces, Enums, Presets cấu hình.
- `AutoVideoEditor.AudioEngine`: Phân tích âm thanh, phát hiện khoảng lặng (ISilenceDetector, IAudioAnalyzer).
- `AutoVideoEditor.VideoEngine`: Xây dựng timeline (ITimelineBuilder), FilterGraph, render FFmpeg (IVideoRenderer).
- `AutoVideoEditor.Infrastructure`: Nhận diện phần cứng GPU, FFprobe metadata, quản lý hàng đợi (IJobManager), Log service.
- `AutoVideoEditor.App`: Giao diện người dùng WPF (MVVM Toolkit, Dark Modern Theme).
- `AutoVideoEditor.Tests`: Bộ kiểm thử tự động xUnit (14/14 tests).

---

## 🚀 Hướng Dẫn Cài Đặt & Build

### Yêu Cầu
- Windows 10 / 11 (64-bit)
- .NET 8.0 SDK
- FFmpeg (đã cài đặt trong PATH hoặc đặt trong thư mục ứng dụng)

### Build & Chạy Ứng Dụng
```bash
# Clone repository
git clone https://github.com/Huy20102002/tool-edit-cc.git
cd tool-edit-cc

# Chạy test
dotnet test tests/AutoVideoEditor.Tests/AutoVideoEditor.Tests.csproj

# Build và xuất bản (Release x64)
dotnet publish src/AutoVideoEditor.App/AutoVideoEditor.App.csproj -c Release -r win-x64 --self-contained false -o bin/publish/AutoVideoEditor
```

---

## 📄 Bản Quyền
Dự án được phát triển cho mục đích tự động hóa quy trình dựng video hàng loạt chất lượng cao.
