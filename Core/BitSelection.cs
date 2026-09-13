using System.Collections;

namespace BitExplorer.Core;

public enum SelectionNavigation { Left, Right, Up, Down, Home, End, PageUp, PageDown }

/// <summary>
/// Selected physical file bits and navigation anchors, independent of their interpreted value order.
/// Changes exceeding the inspection limit leave membership and anchors unchanged.
/// </summary>
public sealed class BitSelection
{
    public const int MaximumSelectedBits = 65_536;
    private readonly SortedSet<long> _bits = [];
    private readonly IReadOnlyCollection<long> _view;
    private long _bitLength;

    public BitSelection() => _view = new ReadOnlyBits(_bits);

    public IReadOnlyCollection<long> Bits => _view;
    public long Anchor { get; private set; } = -1;
    public long ActiveBit { get; private set; } = -1;
    public int ActiveByteOffset => ActiveBit >= 0 ? checked((int)(ActiveBit / 8)) : -1;
    public event EventHandler? Changed;
    public event EventHandler? SelectionLimitReached;

    public bool Contains(long bit) => _bits.Contains(bit);

    public bool ByteHasSelection(int offset)
    {
        long first = (long)offset * 8;
        for (int bit = 0; bit < 8; bit++) if (_bits.Contains(first + bit)) return true;
        return false;
    }

    public void SetDocumentLength(long bitLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bitLength);
        _bitLength = bitLength;
        bool changed = _bits.RemoveWhere(bit => bit >= bitLength) > 0;
        long active = ActiveBit >= bitLength ? (_bits.Count > 0 ? _bits.Max : -1) : ActiveBit;
        long anchor = Anchor >= bitLength ? active : Anchor;
        if (changed || active != ActiveBit || anchor != Anchor)
        {
            ActiveBit = active;
            Anchor = anchor;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear() => Commit([], -1, -1);

    public bool SetSelection(IEnumerable<long> bits)
    {
        ArgumentNullException.ThrowIfNull(bits);
        var replacement = ReadBounded(bits);
        if (replacement is null) return Reject();
        Commit(replacement, replacement.Count > 0 ? replacement.Min : -1, replacement.Count > 0 ? replacement.Max : -1);
        return true;
    }

    /// <summary>Selects an inclusive range and optionally retains a previous selection for Ctrl-drag.</summary>
    public bool SelectRange(long start, long end, bool wholeBytes, IEnumerable<long>? preserve = null)
    {
        if (_bitLength == 0) return false;
        start = Math.Clamp(start, 0, _bitLength - 1);
        end = Math.Clamp(end, 0, _bitLength - 1);
        long first = Math.Min(start, end);
        long last = Math.Max(start, end);
        if (wholeBytes)
        {
            first = first / 8 * 8;
            last = Math.Min(last / 8 * 8 + 7, _bitLength - 1);
        }
        if (last - first + 1 > MaximumSelectedBits) return Reject();
        var replacement = preserve is null ? [] : ReadBounded(preserve);
        if (replacement is null) return Reject();
        for (long bit = first; bit <= last; bit++)
        {
            replacement.Add(bit);
            if (replacement.Count > MaximumSelectedBits) return Reject();
        }
        Commit(replacement, start, end);
        return true;
    }

    /// <summary>Applies pointer or navigation selection without exposing mutable anchor state.</summary>
    public bool SelectAt(long bit, bool wholeByte, bool extend = false, bool toggle = false)
    {
        if (bit < 0 || bit >= _bitLength) return false;
        long anchor = extend && Anchor >= 0 ? Anchor : bit;
        if (extend) return SelectRange(anchor, bit, wholeByte, toggle ? _bits : null);
        if (!toggle) return SelectRange(bit, bit, wholeByte);

        long first = wholeByte ? bit / 8 * 8 : bit;
        long last = wholeByte ? Math.Min(first + 7, _bitLength - 1) : bit;
        var replacement = new SortedSet<long>(_bits);
        bool allSelected = true;
        for (long address = first; address <= last; address++) allSelected &= _bits.Contains(address);
        for (long address = first; address <= last; address++)
            if (allSelected) replacement.Remove(address); else replacement.Add(address);
        if (replacement.Count > MaximumSelectedBits) return Reject();
        Commit(replacement, anchor, bit);
        return true;
    }

    public void SetActiveBit(long bit)
    {
        if (bit < 0 || bit >= _bitLength || bit == ActiveBit) return;
        ActiveBit = bit;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Navigate(SelectionNavigation direction, bool individualBits, int bytesPerRow, int visibleRows, bool extend = false, bool documentBoundary = false)
    {
        if (_bitLength == 0) return false;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerRow);
        long current = Math.Max(0, ActiveBit);
        long step = individualBits ? 1 : 8;
        long rowBits = bytesPerRow * 8L;
        long next = direction switch
        {
            SelectionNavigation.Left => current - step,
            SelectionNavigation.Right => current + step,
            SelectionNavigation.Up => current - rowBits,
            SelectionNavigation.Down => current + rowBits,
            SelectionNavigation.Home => documentBoundary ? 0 : current / rowBits * rowBits,
            SelectionNavigation.End => documentBoundary ? _bitLength - 1 : current / rowBits * rowBits + rowBits - step,
            SelectionNavigation.PageUp => current - Math.Max(1, visibleRows) * rowBits,
            SelectionNavigation.PageDown => current + Math.Max(1, visibleRows) * rowBits,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
        next = Math.Clamp(next, 0, _bitLength - 1);
        if (!individualBits) next = next / 8 * 8;
        return SelectAt(next, !individualBits, extend);
    }

    private SortedSet<long>? ReadBounded(IEnumerable<long> bits)
    {
        var result = new SortedSet<long>();
        foreach (long bit in bits)
        {
            if (bit >= 0 && bit < _bitLength) result.Add(bit);
            if (result.Count > MaximumSelectedBits) return null;
        }
        return result;
    }

    private void Commit(SortedSet<long> bits, long anchor, long active)
    {
        _bits.Clear();
        _bits.UnionWith(bits);
        Anchor = anchor;
        ActiveBit = active;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool Reject()
    {
        SelectionLimitReached?.Invoke(this, EventArgs.Empty);
        return false;
    }

    // Expose a live view, but never a collection callers could cast back and mutate.
    private sealed class ReadOnlyBits(SortedSet<long> bits) : IReadOnlyCollection<long>
    {
        public int Count => bits.Count;
        public IEnumerator<long> GetEnumerator() => bits.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
