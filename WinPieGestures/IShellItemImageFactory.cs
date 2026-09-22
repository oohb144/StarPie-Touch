using System.Runtime.InteropServices;

namespace WinPieGestures;

[ComImport]
[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory
{
	[PreserveSig]
	int GetImage([In][MarshalAs(UnmanagedType.Struct)] SIZE size, [In] int flags, out nint phbm);
}
