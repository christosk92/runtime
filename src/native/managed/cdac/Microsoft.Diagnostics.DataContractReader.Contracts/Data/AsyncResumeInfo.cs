// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.DataContractReader.Data;

// Mirrors System.Runtime.CompilerServices.AsyncHelpers.ResumeInfo
// (see src/coreclr/System.Private.CoreLib/src/System/Runtime/CompilerServices/AsyncHelpers.CoreCLR.cs).
[CdacType(nameof(DataType.AsyncResumeInfo))]
internal sealed partial class AsyncResumeInfo : IData<AsyncResumeInfo>
{
    [Field] public TargetPointer DiagnosticIP { get; }
}
