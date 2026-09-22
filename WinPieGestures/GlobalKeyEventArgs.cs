using System;
using System.Windows.Input;

namespace WinPieGestures;

public class GlobalKeyEventArgs : EventArgs
{
	public uint VkCode { get; }

	public Key Key { get; set; }

	public ModifierKeys Modifiers { get; set; }

	public bool Handled { get; set; }

	public GlobalKeyEventArgs(uint vkCode, ModifierKeys modifiers)
	{
		VkCode = vkCode;
		Key = KeyInterop.KeyFromVirtualKey((int)vkCode);
		Modifiers = modifiers;
		Handled = false;
	}
}
