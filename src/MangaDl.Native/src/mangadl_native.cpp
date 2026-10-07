#include "mangadl_native.h"

#include <windows.h>

MANGADL_API std::uint32_t mdl_version() { return 0x00010000; }

MANGADL_API std::int32_t mdl_detect_borders(const std::uint8_t* pixels, std::int32_t width, std::int32_t height,
                                           std::int32_t /*stride*/, std::int32_t* out_x, std::int32_t* out_y,
                                           std::int32_t* out_w, std::int32_t* out_h)
{
    if (!pixels || !out_x || !out_y || !out_w || !out_h || width <= 0 || height <= 0) return -1;
    // Placeholder: no cropping yet, returns the full image.
    *out_x = 0;
    *out_y = 0;
    *out_w = width;
    *out_h = height;
    return 0;
}

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID) { return TRUE; }
