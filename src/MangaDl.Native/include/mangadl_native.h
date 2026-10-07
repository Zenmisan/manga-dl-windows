// manga-dl native layer (C++). Placeholder: put image decoding, archive
// extraction (CBZ/ZIP/RAR), page cropping and other heavy work here.
// Everything is exported with a plain C ABI so C# can P/Invoke it.
#pragma once

#include <cstdint>

#ifdef MANGADL_NATIVE_EXPORTS
#define MANGADL_API extern "C" __declspec(dllexport)
#else
#define MANGADL_API extern "C" __declspec(dllimport)
#endif

// Returns the native library version, e.g. 0x00010000 for 1.0.0.
MANGADL_API std::uint32_t mdl_version();

// Example of the intended shape: crop uniform borders from a 32-bit BGRA image.
// Writes the crop rectangle to out_x/out_y/out_w/out_h. Returns 0 on success.
// TODO(backend): implement.
MANGADL_API std::int32_t mdl_detect_borders(const std::uint8_t* pixels, std::int32_t width, std::int32_t height,
                                           std::int32_t stride, std::int32_t* out_x, std::int32_t* out_y,
                                           std::int32_t* out_w, std::int32_t* out_h);
