using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;

namespace Snare.Interop;

public static class AdvancedColor
{
    private const uint AdvancedColorEnabledBit = 0x2;

    /// <summary>
    /// How many active displays have HDR (advanced color) turned on. GDI grabs of an HDR
    /// display look washed out, so Snare warns when this isn't zero.
    /// </summary>
    public static unsafe int CountHdrDisplays()
    {
        if (PInvoke.GetDisplayConfigBufferSizes(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != WIN32_ERROR.ERROR_SUCCESS)
        {
            return 0;
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        fixed (DISPLAYCONFIG_PATH_INFO* pathPointer = paths)
        fixed (DISPLAYCONFIG_MODE_INFO* modePointer = modes)
        {
            if (PInvoke.QueryDisplayConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, &pathCount, pathPointer, &modeCount, modePointer, null) != WIN32_ERROR.ERROR_SUCCESS)
            {
                return 0;
            }
        }

        var count = 0;
        for (var i = 0; i < pathCount; i++)
        {
            var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                    size = (uint)sizeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO),
                    adapterId = paths[i].targetInfo.adapterId,
                    id = paths[i].targetInfo.id,
                },
            };

            if (PInvoke.DisplayConfigGetDeviceInfo(&info.header) == 0 && (info.Anonymous.value & AdvancedColorEnabledBit) != 0)
            {
                count++;
            }
        }

        return count;
    }
}
