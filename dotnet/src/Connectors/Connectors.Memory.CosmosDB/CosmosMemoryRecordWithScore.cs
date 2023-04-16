﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.SemanticKernel.Connectors.Memory.Cosmos;
internal class CosmosMemoryRecordWithScore:CosmosMemoryRecord
{
    public double Score { get; set; }
}
