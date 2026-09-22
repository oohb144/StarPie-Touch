using System;
using System.Runtime.InteropServices;

namespace WinPieGestures;

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("0000010b-0000-0000-C000-000000000046")]
internal interface IPersistFile
{
	void GetClassID(out Guid pClassID);

	[PreserveSig]
	int IsDirty();

	void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

	void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);

	void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

	void GetCurFile([Out][MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
}
