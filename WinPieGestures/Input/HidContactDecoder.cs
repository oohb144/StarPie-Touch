using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures.Input;

/// <summary>
/// Descriptor-driven diagnostic decoder. HID data indices are resolved through
/// the device's preparsed data, so a device's report layout is never assumed.
/// </summary>
internal sealed class HidContactDecoder : IDisposable
{
    private const uint RidiPreparsedData = 0x20000005;
    private const int CapSize = 72;
    private readonly nint _preparsed;
    private readonly nint _data;
    private readonly uint _capacity;
    private readonly Dictionary<ushort, Field> _values = new();
    private readonly Dictionary<ushort, Field> _buttons = new();
    private readonly ushort _reportLength;
    private readonly ushort _deviceUsage;

    private readonly record struct Field(ushort Page, ushort Usage, ushort Link, int LogicalMax);

    private sealed class Contact
    {
        public uint? Id;
        public uint? X;
        public uint? Y;
        public int XMax;
        public int YMax;
        public bool Tip;
        public bool Confidence;
        public bool InRange;
    }

    internal sealed record ContactSample(uint Id, uint X, uint Y, int XMax, int YMax, bool Confidence);
    internal sealed record ReportSnapshot(ushort DeviceUsage, uint? ContactCount,
        IReadOnlyList<ContactSample> Contacts, bool PenTip, bool PenInRange, string? Error)
    {
        public bool IsTouch => DeviceUsage == 0x04;
        public bool IsPen => DeviceUsage is 0x01 or 0x02;

        public override string ToString()
        {
            if (Error != null) return Error;
            var output = new StringBuilder();
            if (IsTouch)
            {
                output.Append($"touch active={Contacts.Count} frameCount={ContactCount?.ToString() ?? "?"}");
                foreach (ContactSample contact in Contacts)
                    output.Append($" [id={contact.Id} x={contact.X}/{contact.XMax} "
                        + $"y={contact.Y}/{contact.YMax} confidence={contact.Confidence}]");
            }
            else
            {
                output.Append($"pen tip={PenTip} inRange={PenInRange}");
            }
            return output.ToString();
        }
    }

    private HidContactDecoder(nint preparsed, nint data, uint capacity, ushort reportLength, ushort deviceUsage)
        => (_preparsed, _data, _capacity, _reportLength, _deviceUsage) =
            (preparsed, data, capacity, reportLength, deviceUsage);

    public static HidContactDecoder? Create(nint device)
    {
        uint size = 0;
        GetRawInputDeviceInfo(device, RidiPreparsedData, 0, ref size);
        if (size == 0 || size > 65536) return null;
        nint preparsed = Marshal.AllocHGlobal((int)size);
        nint caps = Marshal.AllocHGlobal(64);
        nint data = 0;
        try
        {
            if (GetRawInputDeviceInfo(device, RidiPreparsedData, preparsed, ref size) == uint.MaxValue ||
                HidP_GetCaps(preparsed, caps) < 0) return null;

            ushort reportLength = (ushort)Marshal.ReadInt16(caps, 4);
            ushort deviceUsage = (ushort)Marshal.ReadInt16(caps, 0);
            uint capacity = HidP_MaxDataListLength(0, preparsed);
            if (reportLength == 0 || capacity == 0 || capacity > 512) return null;
            data = Marshal.AllocHGlobal((int)capacity * 8);
            var decoder = new HidContactDecoder(preparsed, data, capacity, reportLength, deviceUsage);
            data = 0; // Ownership transferred.
            preparsed = 0;
            try
            {
                if (!decoder.LoadCaps((ushort)Marshal.ReadInt16(caps, 48), false) ||
                    !decoder.LoadCaps((ushort)Marshal.ReadInt16(caps, 46), true))
                {
                    decoder.Dispose();
                    return null;
                }
                return decoder;
            }
            catch
            {
                decoder.Dispose();
                throw;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(caps);
            if (data != 0) Marshal.FreeHGlobal(data);
            if (preparsed != 0) Marshal.FreeHGlobal(preparsed);
        }
    }

    private bool LoadCaps(ushort count, bool buttons)
    {
        if (count > 128) return false;
        if (count == 0) return true;
        nint caps = Marshal.AllocHGlobal(count * CapSize);
        try
        {
            ushort length = count;
            int status = buttons
                ? HidP_GetButtonCaps(0, caps, ref length, _preparsed)
                : HidP_GetValueCaps(0, caps, ref length, _preparsed);
            if (status < 0) return false;
            Dictionary<ushort, Field> target = buttons ? _buttons : _values;
            for (int i = 0; i < length; i++)
            {
                nint cap = caps + i * CapSize;
                ushort page = (ushort)Marshal.ReadInt16(cap, 0);
                ushort link = (ushort)Marshal.ReadInt16(cap, 6);
                bool range = Marshal.ReadByte(cap, 12) != 0;
                ushort usageMin = (ushort)Marshal.ReadInt16(cap, 56);
                ushort dataMin = (ushort)Marshal.ReadInt16(cap, 68);
                ushort dataMax = range ? (ushort)Marshal.ReadInt16(cap, 70) : dataMin;
                int logicalMax = buttons ? 1 : Marshal.ReadInt32(cap, 44);
                if (dataMax < dataMin || dataMax - dataMin > 512) return false;
                for (int index = dataMin; index <= dataMax; index++)
                    target[(ushort)index] = new Field(page, (ushort)(usageMin + index - dataMin), link, logicalMax);
            }
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(caps);
        }
    }

    public ReportSnapshot Decode(nint report, uint reportLength)
    {
        if (reportLength != _reportLength)
            return new ReportSnapshot(_deviceUsage, null, Array.Empty<ContactSample>(), false, false,
                $"report-length={reportLength} expected={_reportLength}");
        uint count = _capacity;
        int status = HidP_GetData(0, _data, ref count, _preparsed, report, reportLength);
        if (status < 0)
            return new ReportSnapshot(_deviceUsage, null, Array.Empty<ContactSample>(), false, false,
                $"HidP_GetData=0x{status:X8}");

        var contacts = new Dictionary<ushort, Contact>();
        uint? reportedCount = null;
        for (int i = 0; i < count; i++)
        {
            nint item = _data + i * 8;
            ushort index = (ushort)Marshal.ReadInt16(item);
            uint value = (uint)Marshal.ReadInt32(item, 4);
            if (_values.TryGetValue(index, out Field field))
            {
                if (field.Page == 0x0D && field.Usage == 0x54 && field.Link == 0)
                    reportedCount = value;
                if (field.Link == 0) continue;
                Contact contact = GetContact(contacts, field.Link);
                if (field.Page == 0x0D && field.Usage == 0x51) contact.Id = value;
                if (field.Page == 0x01 && field.Usage == 0x30) { contact.X = value; contact.XMax = field.LogicalMax; }
                if (field.Page == 0x01 && field.Usage == 0x31) { contact.Y = value; contact.YMax = field.LogicalMax; }
            }
            else if (_buttons.TryGetValue(index, out Field button) && button.Link != 0)
            {
                Contact contact = GetContact(contacts, button.Link);
                if (button.Page != 0x0D) continue;
                if (button.Usage == 0x42) contact.Tip = true;
                if (button.Usage == 0x47) contact.Confidence = true;
                if (button.Usage == 0x32) contact.InRange = true;
            }
        }

        ContactSample[] active = contacts.Values
            .Where(contact => contact.Tip && contact.Id.HasValue && contact.X.HasValue && contact.Y.HasValue)
            .Select(contact => new ContactSample(contact.Id!.Value, contact.X!.Value, contact.Y!.Value,
                contact.XMax, contact.YMax, contact.Confidence)).ToArray();
        if (_deviceUsage == 0x04)
            return new ReportSnapshot(_deviceUsage, reportedCount, active, false, false, null);
        return new ReportSnapshot(_deviceUsage, null, Array.Empty<ContactSample>(),
            contacts.Values.Any(contact => contact.Tip), contacts.Values.Any(contact => contact.InRange), null);
    }

    private static Contact GetContact(Dictionary<ushort, Contact> contacts, ushort link)
    {
        if (!contacts.TryGetValue(link, out Contact? contact))
            contacts[link] = contact = new Contact();
        return contact;
    }

    public void Dispose()
    {
        Marshal.FreeHGlobal(_data);
        Marshal.FreeHGlobal(_preparsed);
    }

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(nint device, uint command, nint data, ref uint size);
    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(nint preparsed, nint caps);
    [DllImport("hid.dll")]
    private static extern int HidP_GetValueCaps(int type, nint caps, ref ushort length, nint preparsed);
    [DllImport("hid.dll")]
    private static extern int HidP_GetButtonCaps(int type, nint caps, ref ushort length, nint preparsed);
    [DllImport("hid.dll")]
    private static extern uint HidP_MaxDataListLength(int type, nint preparsed);
    [DllImport("hid.dll")]
    private static extern int HidP_GetData(int type, nint data, ref uint length,
        nint preparsed, nint report, uint reportLength);
}
