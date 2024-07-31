#if !NET9_0_OR_GREATER
using Microsoft.AspNetCore.WebUtilities;

namespace System.Buffers.Text;

static class Base64Url
{
    public static string EncodeToString(byte[] data) =>
        WebEncoders.Base64UrlEncode(data);
}
#endif
