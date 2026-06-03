// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.DataContractReader.Legacy.Helpers;

/// <summary>
/// Helper for writing <c>DacDbiArrayList&lt;T&gt;</c> output parameters from cDAC managed code.
///
/// Native layout (src/coreclr/debug/inc/dacdbistructures.h):
/// <code>
///   template &lt;class T&gt; struct DacDbiArrayList { T* m_pList; int m_nEntries; };
/// </code>
///
/// The native DBI allocates via <c>new(forDbi) T[n]</c>, which reduces to
/// <c>new(nothrow) BYTE[n*sizeof(T)]</c> (see <c>rspriv.h:220-238</c>), and frees via
/// <c>delete[] p</c> (see <c>DacDbiArrayList&lt;T&gt;::Dealloc</c>).
///
/// For trivially-destructible element types (such as <c>AsyncLocalData</c>), the C++
/// compiler emits no array cookie, so <c>delete[]</c> on a <c>BYTE[]</c> allocation
/// reduces to a plain <c>operator delete[]</c> / <c>free()</c> call on MSVC, clang and
/// gcc. Therefore allocations from managed code must come from <c>malloc</c> -- i.e.
/// <see cref="NativeMemory.Alloc(nuint)"/> -- and NOT from <c>LocalAlloc</c>
/// (<see cref="Marshal.AllocHGlobal(int)"/>) or <c>CoTaskMemAlloc</c>
/// (<see cref="Marshal.AllocCoTaskMem(int)"/>), which would mismatch <c>delete[]</c>.
/// </summary>
internal static unsafe class DacDbiArrayListMarshal
{
    /// <summary>
    /// Allocate <paramref name="count"/> elements of <typeparamref name="T"/> via
    /// <see cref="NativeMemory.Alloc(nuint, nuint)"/> and write the resulting pointer
    /// and count into the <c>DacDbiArrayList&lt;T&gt;</c> pointed to by <paramref name="pList"/>.
    /// Returns the allocated buffer pointer (or null if <paramref name="count"/> &lt;= 0).
    /// </summary>
    /// <remarks>
    /// <typeparamref name="T"/> must be an unmanaged, trivially-destructible struct whose
    /// managed layout exactly matches the native struct (use <see cref="StructLayoutAttribute"/>
    /// with <see cref="LayoutKind.Sequential"/>).
    /// </remarks>
    internal static T* AllocAndAssign<T>(nint pList, int count) where T : unmanaged
    {
        // DacDbiArrayList<T> layout: { T* m_pList; int m_nEntries; }
        T** ppList = (T**)pList;
        int* pEntries = (int*)((byte*)pList + sizeof(nint));

        if (count <= 0)
        {
            *ppList = null;
            *pEntries = 0;
            return null;
        }

        T* buffer = (T*)NativeMemory.Alloc((nuint)count, (nuint)sizeof(T));
        *ppList = buffer;
        *pEntries = count;
        return buffer;
    }
}

/// <summary>
/// Managed mirror of native <c>AsyncLocalData</c> from
/// src/coreclr/debug/inc/dacdbistructures.h.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AsyncLocalData
{
    /// <summary>Offset within a continuation object where the variable is stored.</summary>
    public uint Offset;
    /// <summary>IL variable number corresponding to the async local.</summary>
    public uint IlVarNum;
}
